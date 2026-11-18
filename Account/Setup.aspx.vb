Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace Account

    ''' <summary>
    ''' Creates the administrator account for a brand-new installation.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' 01_CreateSchema.sql and 02_SeedLookups.sql build the tables and the subject and
    ''' study-level lists, but they create no people; registration refuses to create an
    ''' administrator by design. Without this page a correctly installed system has no way
    ''' in at all - the installation instructions would have to end by telling the reader
    ''' to run the test project, which is not a deployment step.
    ''' </para>
    ''' <para>
    ''' The page closes itself the moment an administrator exists. That check is made in
    ''' the service on POST as well as here on GET: hiding a form is presentation, and a
    ''' form that has been hidden can still be submitted.
    ''' </para>
    ''' </remarks>
    Public Class Setup
        Inherits BasePage

        Private ReadOnly _authentication As IAuthenticationService = New AuthenticationService()

        Protected WithEvents pnlError As Panel
        Protected WithEvents litError As Literal
        Protected WithEvents pnlClosed As Panel
        Protected WithEvents alertClosed As Controls.AlertMessage
        Protected WithEvents pnlForm As Panel
        Protected WithEvents txtUserName As TextBox
        Protected WithEvents txtFirstName As TextBox
        Protected WithEvents txtLastName As TextBox
        Protected WithEvents ddlGender As DropDownList
        Protected WithEvents txtEmail As TextBox
        Protected WithEvents txtCell As TextBox
        Protected WithEvents txtLocation As TextBox
        Protected WithEvents txtPassword As TextBox
        Protected WithEvents txtConfirm As TextBox
        Protected WithEvents btnCreate As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As Auth = TryCast(Master, Auth)
            If shell IsNot Nothing Then
                shell.BrandImageUrl = "~/Images/auth/register-background.jpg"
            End If

            If Not IsPostBack Then
                RegisterAsyncTask(New PageAsyncTask(AddressOf ApplyAvailabilityAsync))
            End If
        End Sub

        ''' <summary>Shows the form only while the installation has no administrator.</summary>
        Private Async Function ApplyAvailabilityAsync() As Task
            Dim required As Boolean = Await _authentication.SetupRequiredAsync().ConfigureAwait(True)

            pnlForm.Visible = required
            pnlClosed.Visible = Not required

            If Not required Then
                alertClosed.Show(AlertKind.Error, AuthenticationService.SetupClosedMessage)
            End If

            ' No Focus() call here. A registered async task runs after PreRender, and
            ' Page.SetFocus is only legal before and during it - calling it from this method
            ' throws InvalidOperationException and takes the whole page down with a 500.
        End Function

        Protected Sub btnCreate_Click(sender As Object, e As EventArgs) Handles btnCreate.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf CreateAdministratorAsync))
        End Sub

        Private Async Function CreateAdministratorAsync() As Task
            Dim account As New Person With {
                .UserName = txtUserName.Text.Trim(),
                .FirstName = txtFirstName.Text.Trim(),
                .LastName = txtLastName.Text.Trim(),
                .Gender = ddlGender.SelectedValue,
                .EmailAddress = txtEmail.Text.Trim(),
                .CellNumber = txtCell.Text.Trim(),
                .Location = txtLocation.Text.Trim()
            }

            Dim password As String = txtPassword.Text
            Dim confirm As String = txtConfirm.Text

            ' Never redisplayed, whatever the outcome.
            txtPassword.Text = String.Empty
            txtConfirm.Text = String.Empty

            Dim result As ServiceResult(Of Integer) =
                Await _authentication.CreateFirstAdministratorAsync(account, password, confirm).ConfigureAwait(True)

            If Not result.Success Then
                litError.Text = result.Message
                pnlError.Visible = True

                ' The refusal may be "setup is closed", in which case the form must go.
                Dim stillRequired As Boolean = Await _authentication.SetupRequiredAsync().ConfigureAwait(True)
                pnlForm.Visible = stillRequired
                pnlClosed.Visible = Not stillRequired
                Return
            End If

            ' Straight to Login: the administrator signs in with the credentials they just
            ' chose, rather than being handed a session they never authenticated for.
            Response.Redirect("~/Account/Login.aspx?setup=1", endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

    End Class

End Namespace
