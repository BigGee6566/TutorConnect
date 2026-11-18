Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.Enums
Imports TutorConnect.Utilities

Namespace Controls

    ''' <summary>One entry in the sidebar.</summary>
    Public Class NavItem

        Public Sub New(text As String, url As String, icon As String)
            Me.Text = text
            Me.Url = url
            Me.Icon = icon
        End Sub

        Public ReadOnly Property Text As String

        ''' <summary>Application-relative, e.g. "~/Student/Dashboard.aspx".</summary>
        Public ReadOnly Property Url As String

        ''' <summary>Inline SVG markup. Authored here, so there is no icon-font dependency.</summary>
        Public ReadOnly Property Icon As String

        ''' <summary>Optional count pill, e.g. pending requests. Zero hides it.</summary>
        Public Property Count As Integer

    End Class

    ''' <summary>
    ''' The fixed navy navigation rail (section F.3).
    ''' </summary>
    ''' <remarks>
    ''' The item list is driven by <see cref="Role"/>, so a page cannot accidentally show
    ''' a student the tutor menu. This is presentation only - it is not an access control.
    ''' Authorisation is enforced by the &lt;location&gt; rules in Web.config and by each
    ''' service checking the acting user.
    ''' </remarks>
    Public Class NavigationSidebar
        Inherits UserControl

        Protected WithEvents litInitials As Literal
        Protected WithEvents litFullName As Literal
        Protected WithEvents litRole As Literal
        Protected WithEvents rptNav As Repeater

        ''' <summary>Which menu to render.</summary>
        Public Property Role As UserRole = UserRole.Student

        ''' <summary>Display name shown under the avatar.</summary>
        Public Property FullName As String = String.Empty

        ''' <summary>Pending student requests, shown as a pill on the tutor's menu.</summary>
        Public Property PendingRequestCount As Integer

        ''' <summary>
        ''' Binds at the page's PreRenderComplete, not at this control's PreRender.
        ''' </summary>
        ''' <remarks>
        ''' Tasks registered with RegisterAsyncTask run *after* PreRender, so the master
        ''' page has not yet loaded the account by the time a control's own PreRender
        ''' fires. Binding here means the sidebar always renders the values the async
        ''' load produced rather than the defaults.
        ''' </remarks>
        Protected Overrides Sub OnInit(e As EventArgs)
            MyBase.OnInit(e)
            AddHandler Page.PreRenderComplete, AddressOf BindSidebar
        End Sub

        Private Sub BindSidebar(sender As Object, e As EventArgs)
            litFullName.Text = FullName
            litRole.Text = Role.ToString()
            litInitials.Text = BuildInitials(FullName)

            rptNav.DataSource = BuildItems()
            rptNav.DataBind()
        End Sub

        ''' <summary>
        ''' Initials stand in for an avatar image. Profile photographs are optional, and a
        ''' missing one would otherwise leave a broken image in the rail.
        ''' </summary>
        Private Shared Function BuildInitials(fullName As String) As String
            If String.IsNullOrWhiteSpace(fullName) Then Return "?"

            Dim parts As String() = fullName.Split(" "c)
            Dim initials As String = parts.
                Where(Function(p) p.Length > 0).
                Take(2).
                Select(Function(p) p.Substring(0, 1)).
                Aggregate(String.Empty, Function(acc, s) acc & s)

            Return If(initials.Length = 0, "?", initials.ToUpperInvariant())
        End Function

        Private Function BuildItems() As IList(Of NavItem)
            Select Case Role
                Case UserRole.Tutor
                    Dim requests As New NavItem("Student Requests", "~/Tutor/Requests/StudentRequests.aspx", Icons.People) With {
                        .Count = PendingRequestCount
                    }
                    Return New List(Of NavItem) From {
                        New NavItem("Dashboard", "~/Tutor/Dashboard.aspx", Icons.Home),
                        New NavItem("My Profile", "~/Tutor/Profile/MyProfile.aspx", Icons.Person),
                        New NavItem("My Subjects", "~/Tutor/Subjects/MySubjects.aspx", Icons.Book),
                        New NavItem("My Availability", "~/Tutor/Availability/MyAvailability.aspx", Icons.Calendar),
                        requests,
                        New NavItem("My Sessions", "~/Tutor/Sessions/MySessions.aspx", Icons.Calendar)
                    }

                Case UserRole.Administrator
                    Return New List(Of NavItem) From {
                        New NavItem("Dashboard", "~/Admin/Dashboard.aspx", Icons.Home),
                        New NavItem("Manage Users", "~/Admin/Users/ManageUsers.aspx", Icons.People),
                        New NavItem("Subjects", "~/Admin/Subjects/ManageSubjects.aspx", Icons.Book),
                        New NavItem("Bookings", "~/Admin/Bookings/ManageBookings.aspx", Icons.Calendar),
                        New NavItem("Reviews", "~/Admin/Reviews/ManageReviews.aspx", Icons.Star),
                        New NavItem("Audit Log", "~/Admin/Audit/AuditLog.aspx", Icons.Clipboard)
                    }

                Case Else
                    Return New List(Of NavItem) From {
                        New NavItem("Dashboard", "~/Student/Dashboard.aspx", Icons.Home),
                        New NavItem("My Profile", "~/Student/Profile/MyProfile.aspx", Icons.Person),
                        New NavItem("My Subjects", "~/Student/Subjects/MySubjects.aspx", Icons.Book),
                        New NavItem("Find Tutor", "~/Student/Tutors/FindTutor.aspx", Icons.Search),
                        New NavItem("My Requests", "~/Student/Requests/MyRequests.aspx", Icons.Clipboard),
                        New NavItem("My Bookings", "~/Student/Bookings/MyBookings.aspx", Icons.Calendar),
                        New NavItem("Reviews", "~/Student/Reviews/MyReviews.aspx", Icons.Star)
                    }
            End Select
        End Function

#Region "Repeater helpers"

        Protected Function GetUrl(item As Object) As String
            Return ResolveUrl(DirectCast(item, NavItem).Url)
        End Function

        Protected Function GetText(item As Object) As String
            Return HttpUtility.HtmlEncode(DirectCast(item, NavItem).Text)
        End Function

        Protected Function GetIcon(item As Object) As String
            ' Authored markup, not user input.
            Return DirectCast(item, NavItem).Icon
        End Function

        ''' <summary>
        ''' Emits aria-current="page" on the active item. The styling keys off the same
        ''' attribute, so what is highlighted and what is announced cannot diverge.
        ''' </summary>
        Protected Function GetCurrentAttribute(item As Object) As String
            Dim target As String = ResolveUrl(DirectCast(item, NavItem).Url)
            Dim current As String = Request.AppRelativeCurrentExecutionFilePath

            If String.Equals(target, ResolveUrl(current), StringComparison.OrdinalIgnoreCase) Then
                Return "aria-current=""page"""
            End If

            Return String.Empty
        End Function

        Protected Function GetCountPill(item As Object) As String
            Dim count As Integer = DirectCast(item, NavItem).Count
            If count <= 0 Then Return String.Empty

            Return $"<span class=""sidebar__count"">{count}</span>"
        End Function

#End Region



    End Class

End Namespace

