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

Namespace AdminArea

    ''' <summary>
    ''' Creates an additional administrator account.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' Registration refuses the Administrator role and Account/Setup.aspx closes itself as
    ''' soon as the first administrator exists, so this is the only route to a second one.
    ''' </para>
    ''' <para>
    ''' The page is reachable only by an administrator - the &lt;location path="Admin"&gt;
    ''' rule in Web.config sees to that - but the service re-checks the acting account's
    ''' role anyway. This is the one action whose output is another account with full
    ''' privilege, and one gate is not enough for that.
    ''' </para>
    ''' <para>
    ''' No password is ever redisplayed, on success or on failure.
    ''' </para>
    ''' </remarks>
    Public Class AddAdministrator
        Inherits BasePage

        Private ReadOnly _admin As IAdminService = New AdminService()

        Protected WithEvents alert As AlertMessage
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
            Dim shell As AdminShell = TryCast(Master, AdminShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Add Administrator"
                shell.PageSubtitle = "Create another account with full administrator rights"
            End If
        End Sub

        Protected Sub btnCreate_Click(sender As Object, e As EventArgs) Handles btnCreate.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf CreateAsync))
        End Sub

        Private Async Function CreateAsync() As Task
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

            ' Cleared before the call, so nothing can return early and leave them on screen.
            txtPassword.Text = String.Empty
            txtConfirm.Text = String.Empty

            Dim result As ServiceResult(Of Integer) = Await _admin.CreateAdministratorAsync(
                CurrentPersonId, account, password, confirm).ConfigureAwait(True)

            If Not result.Success Then
                alert.Show(AlertKind.Error, result.Message)
                Return
            End If

            ' The form is emptied on success so a second submission cannot create a
            ' duplicate by refresh, and so the new person's details do not sit on screen.
            ClearForm()
            alert.Show(AlertKind.Success, result.Message)
        End Function

        Private Sub ClearForm()
            txtUserName.Text = String.Empty
            txtFirstName.Text = String.Empty
            txtLastName.Text = String.Empty
            ddlGender.SelectedValue = "Other"
            txtEmail.Text = String.Empty
            txtCell.Text = String.Empty
            txtLocation.Text = String.Empty
        End Sub

    End Class

End Namespace
