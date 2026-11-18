Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace Account

    ''' <summary>
    ''' The entry point of the application. There is no public landing page.
    ''' </summary>
    Public Class Login
        Inherits BasePage

        Private ReadOnly _authentication As IAuthenticationService = New AuthenticationService()

        Protected WithEvents alertRegistered As Controls.AlertMessage
        Protected WithEvents pnlError As Panel
        Protected WithEvents litError As Literal
        Protected WithEvents txtUserName As TextBox
        Protected WithEvents txtPassword As TextBox
        Protected WithEvents btnLogin As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As Auth = TryCast(Master, Auth)
            If shell IsNot Nothing Then
                shell.BrandImageUrl = "~/Images/auth/login-background.jpg"
            End If

            If Not IsPostBack Then
                ShowRegistrationConfirmation()
                txtUserName.Focus()
                RegisterAsyncTask(New PageAsyncTask(AddressOf RedirectToSetupIfUnconfiguredAsync))
            End If
        End Sub

        ''' <summary>
        ''' Sends a brand-new installation to first-run setup.
        ''' </summary>
        ''' <remarks>
        ''' A freshly built database has no people in it, so this page would otherwise
        ''' refuse every credential with no explanation of what to do instead. One indexed
        ''' lookup on the first view of Login is a small price for an installation that
        ''' explains itself; it is skipped on postback.
        ''' </remarks>
        Private Async Function RedirectToSetupIfUnconfiguredAsync() As Task
            If Not Await _authentication.SetupRequiredAsync().ConfigureAwait(True) Then Return

            Response.Redirect("~/Account/Setup.aspx", endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

        ''' <summary>
        ''' Confirms a just-completed registration.
        ''' </summary>
        ''' <remarks>
        ''' The query string is untrusted, so this matches the two values it knows and
        ''' ignores anything else. The message is chosen here and never echoed from the
        ''' URL, which rules out reflected content in the alert region.
        ''' </remarks>
        Private Sub ShowRegistrationConfirmation()
            Select Case Request.QueryString("registered")
                Case "student"
                    alertRegistered.Show(AlertKind.Success,
                        "Your account has been created. Sign in to continue.")

                Case "tutor"
                    alertRegistered.Show(AlertKind.Success,
                        "Your account has been created and is awaiting administrator approval. " &
                        "You can sign in once it has been approved.")
            End Select

            ' BR-25: the user was signed out mid-session because their account was
            ' deactivated. Saying so is kinder than an unexplained return to Login.
            If Request.QueryString("deactivated") = "1" Then
                alertRegistered.Show(AlertKind.Error, AuthenticationService.InactiveAccountMessage)
            End If

            If Request.QueryString("setup") = "1" Then
                alertRegistered.Show(AlertKind.Success,
                    "The administrator account was created. Sign in to continue.")
            End If
        End Sub

        Protected Sub btnLogin_Click(sender As Object, e As EventArgs) Handles btnLogin.Click
            ' RegisterAsyncTask is how an async operation joins the Web Forms page
            ' lifecycle. Calling .Result here instead would deadlock on the request context.
            RegisterAsyncTask(New PageAsyncTask(AddressOf SignInAsync))
        End Sub

        Private Async Function SignInAsync() As Task
            Dim userName As String = txtUserName.Text.Trim()
            Dim password As String = txtPassword.Text

            ' Always clear the password field, whatever happens next.
            txtPassword.Text = String.Empty

            If userName.Length = 0 OrElse password.Length = 0 Then
                ShowError("Enter your username and password.")
                Return
            End If

            Dim result As ServiceResult(Of Person) =
                Await _authentication.AuthenticateAsync(userName, password).ConfigureAwait(True)

            If Not result.Success Then
                ShowError(result.Message)
                Return
            End If

            Dim account As Person = result.Data

            ' SEC-07. Clearing empties the session; rotating the identifier is what
            ' actually defeats session fixation, because an id planted before sign-in
            ' would otherwise remain valid after it.
            Session.Clear()
            RoleHelper.RegenerateSessionId(Context)

            RoleHelper.IssueTicket(New HttpContextWrapper(Context), account.PersonId,
                                   account.UserName, account.Role, persistent:=False)

            Response.Redirect(ResolveReturnUrl(account), endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

        ''' <summary>
        ''' Honours ReturnUrl only when it is local. An absolute URL here would be an open
        ''' redirect: an attacker could send a signed-in user straight to a look-alike site.
        ''' </summary>
        Private Function ResolveReturnUrl(account As Person) As String
            Dim returnUrl As String = Request.QueryString("ReturnUrl")

            If IsLocalUrl(returnUrl) Then
                Return returnUrl
            End If

            Return ResolveUrl(RoleHelper.GetHomePage(account.Role))
        End Function

        ''' <summary>
        ''' True only for a URL that points back into this application.
        ''' </summary>
        ''' <remarks>
        ''' The specification calls for Request.IsUrlLocalToHost, but that member does not
        ''' exist on HttpRequest or HttpRequestBase - it is an extension method in
        ''' System.Web.WebPages, an assembly this Web Forms project does not reference.
        ''' The check is therefore written out here, matching the same rules:
        '''
        '''   accepted : "/Student/Dashboard.aspx", "~/Student/Dashboard.aspx"
        '''   rejected : "https://evil.example", "//evil.example", "/\evil.example"
        '''
        ''' The protocol-relative and backslash forms matter: browsers treat "//host" as
        ''' an absolute URL, so accepting it would hand a freshly signed-in user to an
        ''' attacker's look-alike site.
        ''' </remarks>
        Friend Shared Function IsLocalUrl(url As String) As Boolean
            If String.IsNullOrWhiteSpace(url) Then Return False

            If url.StartsWith("~/", StringComparison.Ordinal) Then Return True

            If url(0) <> "/"c Then Return False

            ' A single leading slash only. "//host" and "/\host" are absolute to a browser.
            If url.Length = 1 Then Return True
            Return url(1) <> "/"c AndAlso url(1) <> "\"c
        End Function

        ''' <summary>
        ''' Shows the failure message above the form.
        ''' </summary>
        ''' <remarks>
        ''' Deliberately does not call Focus(). This runs inside the registered async task,
        ''' which the page lifecycle executes AFTER PreRender, and Page.SetFocus is only
        ''' legal before and during PreRender - so the call threw
        ''' InvalidOperationException and every failed sign-in returned HTTP 500 instead of
        ''' this message. The service-level test still passed throughout, because the fault
        ''' was in the page, not the rule.
        ''' </remarks>
        Private Sub ShowError(message As String)
            litError.Text = message
            pnlError.Visible = True
        End Sub

    End Class

End Namespace
