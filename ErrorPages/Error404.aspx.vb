Option Strict On
Option Explicit On

Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.Security

Namespace ErrorPages

    ''' <summary>
    ''' Friendly "not found" page.
    ''' </summary>
    ''' <remarks>
    ''' Anonymous by configuration, because an unauthenticated visitor mistyping a URL must
    ''' see this rather than be bounced to the login page.
    '''
    ''' Nothing about the requested path is echoed back into the markup: reflecting a URL
    ''' onto an error page is a classic cross-site scripting vector (SEC-09).
    ''' </remarks>
    Public Class Error404
        Inherits Page

        Protected WithEvents pnlUnderConstruction As Panel
        Protected WithEvents lnkHome As HyperLink
        Protected WithEvents lnkLogin As HyperLink

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            ' A real 404, not a 200 with an apology - search engines and monitoring both
            ' rely on the status code being honest.
            Response.StatusCode = 404
            Response.TrySkipIisCustomErrors = True

            Dim identity As TutorConnectIdentity = TryCast(User?.Identity, TutorConnectIdentity)

            If identity Is Nothing Then
                lnkHome.Visible = False
                Return
            End If

            lnkLogin.Visible = False
            lnkHome.NavigateUrl = RoleHelper.GetHomePage(identity.Role)

            ' While the application is still being built, a signed-in user following a
            ' dashboard link to an unwritten screen deserves an explanation.
            pnlUnderConstruction.Visible = True
        End Sub

    End Class

End Namespace
