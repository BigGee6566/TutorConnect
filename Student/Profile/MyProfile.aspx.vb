Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>
    ''' The student's own profile.
    ''' </summary>
    ''' <remarks>
    ''' Loads and saves only the signed-in student's record. There is no id in the URL to
    ''' tamper with, and the service overwrites any PersonId it is handed with the one from
    ''' the ticket (BR-24).
    ''' </remarks>
    Public Class MyProfile
        Inherits StudentPageBase

        Private ReadOnly _students As IStudentService = New StudentService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents txtFirstName As TextBox
        Protected WithEvents txtLastName As TextBox
        Protected WithEvents ddlGender As DropDownList
        Protected WithEvents txtEmail As TextBox
        Protected WithEvents txtCellNumber As TextBox
        Protected WithEvents txtLocation As TextBox
        Protected WithEvents txtStudentNo As TextBox
        Protected WithEvents txtUserName As TextBox
        Protected WithEvents btnSave As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            SetHeading("My Profile", "View and update your details")

            If Not IsPostBack Then
                RegisterAsyncTask(New PageAsyncTask(AddressOf LoadProfileAsync))
            End If
        End Sub

        Private Async Function LoadProfileAsync() As Task
            Dim profile As StudentProfile = Await ResolveStudentProfileAsync()

            If profile Is Nothing OrElse profile.Account Is Nothing Then
                alert.Show(AlertKind.Error, "No student profile is linked to this account.")
                btnSave.Enabled = False
                Return
            End If

            Dim account As Person = profile.Account
            txtFirstName.Text = account.FirstName
            txtLastName.Text = account.LastName
            ddlGender.SelectedValue = account.Gender
            txtEmail.Text = account.EmailAddress
            txtCellNumber.Text = account.CellNumber
            txtLocation.Text = account.Location
            txtStudentNo.Text = profile.StudentNo
            txtUserName.Text = account.UserName
        End Function

        Protected Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf SaveProfileAsync))
        End Sub

        Private Async Function SaveProfileAsync() As Task
            Dim profile As StudentProfile = Await ResolveStudentProfileAsync()
            If profile Is Nothing Then
                alert.Show(AlertKind.Error, "No student profile is linked to this account.")
                Return
            End If

            Dim account As New Person With {
                .PersonId = CurrentPersonId,
                .UserName = profile.Account.UserName,
                .FirstName = txtFirstName.Text.Trim(),
                .LastName = txtLastName.Text.Trim(),
                .Gender = ddlGender.SelectedValue,
                .EmailAddress = txtEmail.Text.Trim(),
                .CellNumber = txtCellNumber.Text.Trim(),
                .Location = txtLocation.Text.Trim()
            }

            Dim result As ServiceResult =
                Await _students.UpdateProfileAsync(CurrentPersonId, account).ConfigureAwait(True)

            alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
        End Function

        ''' <summary>Sets the master page heading, if this page is hosted by one.</summary>
        Private Sub SetHeading(title As String, subtitle As String)
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell Is Nothing Then Return
            shell.PageTitle = title
            shell.PageSubtitle = subtitle
        End Sub

    End Class

End Namespace
