Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports System.Web.UI
Imports TutorConnect.Controls
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace MasterPages

    ''' <summary>
    ''' Shared behaviour for the three signed-in shells.
    ''' </summary>
    ''' <remarks>
    ''' Student, Tutor and Admin masters differ only in which menu the sidebar shows, so
    ''' everything else - loading the account, filling the rail, exposing the page
    ''' heading - lives here rather than being written out three times.
    ''' </remarks>
    Public MustInherit Class AppMasterBase
        Inherits MasterPage

        Private ReadOnly _authentication As IAuthenticationService = New AuthenticationService()
        Private ReadOnly _notifications As INotificationService = New NotificationService()

        Protected WithEvents sidebar As NavigationSidebar
        Protected WithEvents header As TopHeader
        Protected WithEvents litPageTitle As Literal
        Protected WithEvents litPageSubtitle As Literal

        ''' <summary>
        ''' The heading is bound at PreRenderComplete because a page typically composes it
        ''' from the account ("Welcome, Zanele!"), which is only available once the
        ''' async load has run.
        ''' </summary>
        Protected Overrides Sub OnInit(e As EventArgs)
            MyBase.OnInit(e)
            AddHandler Page.PreRenderComplete, AddressOf BindHeading
        End Sub

        Private Sub BindHeading(sender As Object, e As EventArgs)
            litPageTitle.Text = PageTitle
            litPageSubtitle.Text = PageSubtitle
            litPageSubtitle.Visible = Not String.IsNullOrWhiteSpace(PageSubtitle)
        End Sub

        ''' <summary>Which menu the sidebar renders. Supplied by each concrete master.</summary>
        Protected MustOverride ReadOnly Property ShellRole As UserRole

        ''' <summary>H1 shown above the content, e.g. "Welcome, Zanele!".</summary>
        Public Property PageTitle As String = String.Empty

        ''' <summary>Muted line beneath the heading.</summary>
        Public Property PageSubtitle As String = String.Empty

        ''' <summary>Pending student requests, surfaced as a pill on the tutor's menu.</summary>
        Public Property PendingRequestCount As Integer

        ''' <summary>The signed-in account, available to pages once the async load runs.</summary>
        Protected Property CurrentAccount As Person

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            ' The identifier comes from the authentication ticket, never from the URL (BR-24).
            Dim basePage As BasePage = TryCast(Page, BasePage)
            If basePage Is Nothing Then Return

            Page.RegisterAsyncTask(New PageAsyncTask(AddressOf LoadShellAsync))
        End Sub

        ''' <summary>
        ''' Loads the account and fills the sidebar.
        ''' </summary>
        ''' <remarks>
        ''' Runs after PreRender and before PreRenderComplete, which is exactly when the
        ''' sidebar binds - so the rail always shows the loaded name rather than a default.
        ''' </remarks>
        Private Async Function LoadShellAsync() As Task
            Dim personId As Integer = SignedInPersonId()

            sidebar.Role = ShellRole
            sidebar.PendingRequestCount = PendingRequestCount
            header.Title = If(String.IsNullOrWhiteSpace(PageTitle), "TutorConnect", PageTitle)

            If personId <= 0 Then Return

            ' BR-27. Fetched here because a user control cannot register async work of its
            ' own, and blocking inside the control would stall the render.
            header.UnreadCount = Await _notifications.CountUnreadAsync(personId).ConfigureAwait(True)

            Dim result As ServiceResult(Of Person) =
                Await _authentication.GetAccountAsync(personId).ConfigureAwait(True)

            If Not result.Success Then Return

            CurrentAccount = result.Data
            sidebar.FullName = CurrentAccount.FullName
        End Function

        ''' <summary>The signed-in PersonId, taken from the ticket.</summary>
        Protected Function SignedInPersonId() As Integer
            Dim identity As TutorConnectIdentity = TryCast(Page.User?.Identity, TutorConnectIdentity)
            Return If(identity Is Nothing, 0, identity.PersonId)
        End Function

    End Class

End Namespace
