Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI
Imports TutorConnect.Enums
Imports TutorConnect.Services

Namespace Security

    ''' <summary>
    ''' Base class for every page in the application.
    ''' </summary>
    ''' <remarks>
    ''' Carries the CSRF defence (SEC-06) and gives pages a safe way to read the signed-in
    ''' user's identity. Pages must take the acting PersonId from
    ''' <see cref="CurrentPersonId"/>, never from a query string or a form field (BR-24).
    ''' </remarks>
    Public Class BasePage
        Inherits Page

        ''' <summary>
        ''' Key used to tie the ViewState MAC to one user's session (SEC-06).
        ''' </summary>
        Private Const ViewStateUserKeySlot As String = "__ViewStateUserKey"

        ''' <summary>
        ''' Ties the ViewState MAC to this session (SEC-06).
        ''' </summary>
        ''' <remarks>
        ''' <para>
        ''' Must happen in OnInit: by Page_Load the ViewState has already been loaded and
        ''' setting the key then throws.
        ''' </para>
        ''' <para>
        ''' The key is a GUID stored IN the session, not Session.SessionID. ASP.NET does
        ''' not persist a session until something is written to it, and merely reading
        ''' SessionID does not count - so with an empty session no ASP.NET_SessionId cookie
        ''' is issued and every request receives a different identifier. Using SessionID
        ''' directly therefore MAC'd the ViewState with one value on the GET and validated
        ''' it against another on the POST, failing every postback with
        ''' "Validation of viewstate MAC failed". Writing the GUID is what materialises the
        ''' session and makes the value stable.
        ''' </para>
        ''' <para>
        ''' Security is unchanged: the key is still per-session and unguessable, so a
        ''' ViewState harvested from one user cannot be replayed against another. Signing
        ''' in calls Session.Clear(), which rotates this key - deliberate, and safe because
        ''' that response is a redirect with no ViewState in flight.
        ''' </para>
        ''' </remarks>
        Protected Overrides Sub OnInit(e As EventArgs)
            If Session IsNot Nothing Then
                Dim key As String = TryCast(Session(ViewStateUserKeySlot), String)

                If String.IsNullOrEmpty(key) Then
                    key = Guid.NewGuid().ToString("N")
                    Session(ViewStateUserKeySlot) = key
                End If

                ViewStateUserKey = key
            End If

            MyBase.OnInit(e)
        End Sub

        ''' <summary>
        ''' BR-25 / AT-24. Ends a live session the moment the account behind it is
        ''' deactivated.
        ''' </summary>
        ''' <remarks>
        ''' A Forms ticket stays valid for its full 30 minutes once issued, so without this
        ''' an administrator could deactivate someone who then carries on working until
        ''' their ticket happened to expire. The requirement is that the next request is
        ''' refused, so the check runs on every authenticated page view - one indexed
        ''' primary-key lookup. Caching it would trade the rule's correctness for a saving
        ''' this application does not need.
        '''
        ''' Registered as an async task rather than blocking: the task runs before
        ''' PreRenderComplete, so the redirect still happens before anything is rendered.
        ''' </remarks>
        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)

            If IsSignedIn Then
                RegisterAsyncTask(New PageAsyncTask(AddressOf EnsureAccountStillActiveAsync))
            End If
        End Sub

        Private Async Function EnsureAccountStillActiveAsync() As Task
            Dim authentication As IAuthenticationService = New AuthenticationService()

            If Await authentication.IsStillActiveAsync(CurrentPersonId).ConfigureAwait(True) Then
                Return
            End If

            RoleHelper.SignOut(New HttpContextWrapper(Context))

            Response.Redirect("~/Account/Login.aspx?deactivated=1", endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

        ''' <summary>
        ''' The signed-in user's PersonId, or 0 when nobody is signed in.
        ''' </summary>
        Protected ReadOnly Property CurrentPersonId As Integer
            Get
                Dim identity As TutorConnectIdentity = CurrentIdentity
                Return If(identity Is Nothing, 0, identity.PersonId)
            End Get
        End Property

        ''' <summary>The signed-in user's role. Meaningless when nobody is signed in.</summary>
        Protected ReadOnly Property CurrentRole As UserRole
            Get
                Dim identity As TutorConnectIdentity = CurrentIdentity
                Return If(identity Is Nothing, UserRole.Student, identity.Role)
            End Get
        End Property

        Protected ReadOnly Property IsSignedIn As Boolean
            Get
                Return CurrentIdentity IsNot Nothing
            End Get
        End Property

        Private ReadOnly Property CurrentIdentity As TutorConnectIdentity
            Get
                If User Is Nothing OrElse User.Identity Is Nothing Then Return Nothing
                Return TryCast(User.Identity, TutorConnectIdentity)
            End Get
        End Property

    End Class

End Namespace
