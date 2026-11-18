Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace TutorArea

    ''' <summary>
    ''' The tutor's own profile, including the bio students see.
    ''' </summary>
    ''' <remarks>
    ''' AT-22. There is no TutorId in the URL or the form: the service resolves the tutor
    ''' record from the signed-in PersonId and writes only that one.
    ''' </remarks>
    Public Class MyProfile
        Inherits TutorPageBase

        Private ReadOnly _tutors As ITutorService = New TutorService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents txtFirstName As TextBox
        Protected WithEvents txtLastName As TextBox
        Protected WithEvents ddlGender As DropDownList
        Protected WithEvents txtEmail As TextBox
        Protected WithEvents txtCellNumber As TextBox
        Protected WithEvents txtLocation As TextBox
        Protected WithEvents txtQualification As TextBox
        Protected WithEvents txtExperienceYears As TextBox
        Protected WithEvents txtRate As TextBox
        Protected WithEvents txtBio As TextBox
        Protected WithEvents litApproval As Literal
        Protected WithEvents btnSave As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As TutorShell = TryCast(Master, TutorShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "My Profile"
                shell.PageSubtitle = "View and update your profile"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

        Private Async Function LoadAsync() As Task
            Dim result As ServiceResult(Of TutorProfile) =
                Await _tutors.GetProfileAsync(CurrentPersonId).ConfigureAwait(True)

            If Not result.Success Then
                alert.Show(AlertKind.Error, result.Message)
                btnSave.Enabled = False
                Return
            End If

            litApproval.Text = If(result.Data.IsApproved,
                                  "Approved - students can find you in search.",
                                  "Awaiting administrator approval. You will not appear in search until approved.")

            If IsPostBack Then Return

            Dim account As Person = result.Data.Account
            Dim culture As CultureInfo = CultureInfo.GetCultureInfo("en-ZA")

            txtFirstName.Text = account.FirstName
            txtLastName.Text = account.LastName
            ddlGender.SelectedValue = account.Gender
            txtEmail.Text = account.EmailAddress
            txtCellNumber.Text = account.CellNumber
            txtLocation.Text = account.Location
            txtQualification.Text = result.Data.Qualification
            txtExperienceYears.Text = result.Data.ExperienceYears.ToString(culture)
            txtRate.Text = result.Data.RatePerSession.ToString("0.##", culture)
            txtBio.Text = result.Data.Bio
        End Function

        Protected Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf SaveAsync))
        End Sub

        Private Async Function SaveAsync() As Task
            Dim current As TutorProfile = Await ResolveTutorProfileAsync()
            If current Is Nothing Then
                alert.Show(AlertKind.Error, "No tutor profile is linked to this account.")
                Return
            End If

            Dim account As New Person With {
                .PersonId = CurrentPersonId,
                .UserName = current.Account.UserName,
                .FirstName = txtFirstName.Text.Trim(),
                .LastName = txtLastName.Text.Trim(),
                .Gender = ddlGender.SelectedValue,
                .EmailAddress = txtEmail.Text.Trim(),
                .CellNumber = txtCellNumber.Text.Trim(),
                .Location = txtLocation.Text.Trim()
            }

            Dim details As New TutorProfile With {
                .Qualification = txtQualification.Text.Trim(),
                .ExperienceYears = ParseInt(txtExperienceYears.Text),
                .RatePerSession = ParseDecimal(txtRate.Text),
                .Bio = txtBio.Text.Trim()
            }

            Dim result As ServiceResult =
                Await _tutors.UpdateProfileAsync(CurrentPersonId, account, details).ConfigureAwait(True)

            alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
        End Function

        ''' <summary>Unparseable input becomes -1 so the validator rejects it with a message.</summary>
        Private Shared Function ParseInt(value As String) As Integer
            Dim parsed As Integer
            If Integer.TryParse(value, NumberStyles.Integer, CultureInfo.GetCultureInfo("en-ZA"), parsed) Then
                Return parsed
            End If
            Return -1
        End Function

        Private Shared Function ParseDecimal(value As String) As Decimal
            Dim parsed As Decimal
            If Decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("en-ZA"), parsed) Then
                Return parsed
            End If
            Return -1D
        End Function

    End Class

End Namespace
