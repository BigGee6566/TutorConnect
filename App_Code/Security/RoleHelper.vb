Option Strict On
Option Explicit On

Imports System.Security.Principal
Imports System.Web
Imports System.Web.Security
Imports TutorConnect.Enums
Imports TutorConnect.Utilities

Namespace Security

    ''' <summary>
    ''' Issues and reads the Forms Authentication ticket.
    ''' </summary>
    ''' <remarks>
    ''' The ticket's UserData carries "PersonId|Role". Global.asax rebuilds a
    ''' GenericPrincipal from it on every request, which is what makes the
    ''' &lt;location&gt; role rules in Web.config work (SEC-04).
    '''
    ''' UserData is signed and encrypted as part of the ticket, so a user cannot edit
    ''' their own role - but it is still only as trustworthy as machineKey. Anything that
    ''' changes data re-reads the account from the database rather than trusting the
    ''' ticket alone.
    ''' </remarks>
    Public NotInheritable Class RoleHelper

        Private Const UserDataSeparator As Char = "|"c

        ''' <summary>Sliding expiry, in minutes. Matches &lt;forms timeout&gt; in Web.config.</summary>
        Public Const TicketTimeoutMinutes As Integer = 30

        Private Sub New()
            ' Static helper; never instantiated.
        End Sub

        ''' <summary>
        ''' Issues the authentication cookie for a signed-in account.
        ''' </summary>
        Public Shared Sub IssueTicket(context As HttpContextBase,
                                      personId As Integer,
                                      userName As String,
                                      role As UserRole,
                                      persistent As Boolean)
            If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))

            ' CultureInfo is fully qualified because this file imports System.Web, where a
            ' bare "Globalization" binds to System.Web.Globalization instead.
            Dim userData As String =
                personId.ToString(System.Globalization.CultureInfo.InvariantCulture) &
                UserDataSeparator & role.ToString()

            Dim ticket As New FormsAuthenticationTicket(
                version:=2,
                name:=userName,
                issueDate:=Date.Now,
                expiration:=Date.Now.AddMinutes(TicketTimeoutMinutes),
                isPersistent:=persistent,
                userData:=userData,
                cookiePath:=FormsAuthentication.FormsCookiePath)

            Dim cookie As New HttpCookie(FormsAuthentication.FormsCookieName,
                                         FormsAuthentication.Encrypt(ticket)) With {
                .HttpOnly = True,
                .Secure = FormsAuthentication.RequireSSL,
                .Path = FormsAuthentication.FormsCookiePath
            }

            If persistent Then cookie.Expires = ticket.Expiration

            context.Response.Cookies.Add(cookie)
        End Sub

        ''' <summary>
        ''' Issues a brand-new session identifier, discarding the old one (SEC-07).
        ''' </summary>
        ''' <remarks>
        ''' Session.Clear() empties the session but keeps its id, so an identifier an
        ''' attacker planted in the victim's browser before sign-in would still be valid
        ''' afterwards - that is session fixation. Rotating the id is what actually closes
        ''' it.
        '''
        ''' Call this immediately before issuing the authentication ticket, and redirect
        ''' straight after: the current request keeps the old session object, and only the
        ''' next request picks up the new id.
        ''' </remarks>
        Public Shared Sub RegenerateSessionId(context As HttpContext)
            If context Is Nothing OrElse context.Session Is Nothing Then Return

            ' Cookieless sessions carry the id in the URL, where replacing it mid-request
            ' would break the redirect. Not a configuration this application uses
            ' (sessionState cookieless="UseCookies"), but worth refusing rather than
            ' corrupting.
            If context.Session.IsCookieless Then Return

            Try
                Dim manager As New SessionIDManager()
                Dim newId As String = manager.CreateSessionID(context)

                Dim redirected As Boolean
                Dim cookieAdded As Boolean
                manager.SaveSessionID(context, newId, redirected, cookieAdded)

            Catch ex As HttpException
                ' The session module is not available for this request. The ticket is
                ' still reissued, so sign-in is unaffected; only the rotation is skipped.
            End Try
        End Sub

        ''' <summary>Signs the user out and abandons the session (SEC-07).</summary>
        Public Shared Sub SignOut(context As HttpContextBase)
            FormsAuthentication.SignOut()

            If context IsNot Nothing AndAlso context.Session IsNot Nothing Then
                context.Session.Abandon()
            End If
        End Sub

        ''' <summary>
        ''' Rebuilds a principal carrying the role, for Application_PostAuthenticateRequest.
        ''' Returns Nothing when the ticket's UserData cannot be parsed.
        ''' </summary>
        Public Shared Function BuildPrincipal(ticket As FormsAuthenticationTicket) As IPrincipal
            If ticket Is Nothing Then Return Nothing

            Dim personId As Integer
            Dim role As UserRole
            If Not TryParseUserData(ticket.UserData, personId, role) Then Return Nothing

            Dim identity As New TutorConnectIdentity(ticket.Name, personId, role)
            Return New GenericPrincipal(identity, New String() {role.ToString()})
        End Function

        ''' <summary>Parses "PersonId|Role" out of the ticket's UserData.</summary>
        Public Shared Function TryParseUserData(userData As String,
                                                <Runtime.InteropServices.Out> ByRef personId As Integer,
                                                <Runtime.InteropServices.Out> ByRef role As UserRole) As Boolean
            personId = 0
            role = UserRole.Student

            If String.IsNullOrWhiteSpace(userData) Then Return False

            Dim parts As String() = userData.Split(UserDataSeparator)
            If parts.Length <> 2 Then Return False

            If Not Integer.TryParse(parts(0), personId) Then Return False
            If personId <= 0 Then Return False

            Try
                role = EnumHelper.Parse(Of UserRole)(parts(1))
            Catch ex As InvalidOperationException
                ' A ticket carrying a role this build does not know about is not usable.
                Return False
            End Try

            Return True
        End Function

        ''' <summary>The landing page for a role, used after sign-in.</summary>
        Public Shared Function GetHomePage(role As UserRole) As String
            Select Case role
                Case UserRole.Student : Return "~/Student/Dashboard.aspx"
                Case UserRole.Tutor : Return "~/Tutor/Dashboard.aspx"
                Case UserRole.Administrator : Return "~/Admin/Dashboard.aspx"
                Case Else : Return "~/Account/Login.aspx"
            End Select
        End Function

    End Class

    ''' <summary>
    ''' An identity that carries the signed-in PersonId, so pages never have to read it
    ''' from a query string (BR-24).
    ''' </summary>
    Public NotInheritable Class TutorConnectIdentity
        Implements IIdentity

        Public Sub New(name As String, personId As Integer, role As UserRole)
            _name = name
            Me.PersonId = personId
            Me.Role = role
        End Sub

        Private ReadOnly _name As String

        Public ReadOnly Property Name As String Implements IIdentity.Name
            Get
                Return _name
            End Get
        End Property

        Public ReadOnly Property AuthenticationType As String Implements IIdentity.AuthenticationType
            Get
                Return "Forms"
            End Get
        End Property

        Public ReadOnly Property IsAuthenticated As Boolean Implements IIdentity.IsAuthenticated
            Get
                Return True
            End Get
        End Property

        ''' <summary>The authoritative identifier for the signed-in user.</summary>
        Public ReadOnly Property PersonId As Integer

        Public ReadOnly Property Role As UserRole

    End Class

End Namespace
