Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Enums
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace Account

    ''' <summary>
    ''' Self-registration for students and tutors. Administrators can never be registered
    ''' here: the first one comes from Setup.aspx on an empty installation.
    ''' </summary>
    Public Class Register
        Inherits BasePage

        Private ReadOnly _authentication As IAuthenticationService = New AuthenticationService()

        Protected WithEvents pnlError As Panel
        Protected WithEvents litError As Literal
        Protected WithEvents rdoStudent As RadioButton
        Protected WithEvents rdoTutor As RadioButton
        Protected WithEvents pnlStudentFields As Panel
        Protected WithEvents pnlTutorFields As Panel
        Protected WithEvents txtFirstName As TextBox
        Protected WithEvents txtLastName As TextBox
        Protected WithEvents txtEmail As TextBox
        Protected WithEvents txtUserName As TextBox
        Protected WithEvents txtCellNumber As TextBox
        Protected WithEvents ddlGender As DropDownList
        Protected WithEvents txtLocation As TextBox
        Protected WithEvents txtStudentNo As TextBox
        Protected WithEvents txtQualification As TextBox
        Protected WithEvents txtExperienceYears As TextBox
        Protected WithEvents txtRatePerSession As TextBox
        Protected WithEvents txtPassword As TextBox
        Protected WithEvents txtConfirmPassword As TextBox
        Protected WithEvents btnRegister As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As Auth = TryCast(Master, Auth)
            If shell IsNot Nothing Then
                shell.BrandImageUrl = "~/Images/auth/register-background.jpg"
            End If

            ApplyRoleVisibility()
        End Sub

        ''' <summary>
        ''' Shows the fields that belong to the chosen account type.
        ''' </summary>
        ''' <remarks>
        ''' Done on the server via AutoPostBack rather than in JavaScript, so the form is
        ''' usable with scripting disabled. The client-side niceties in auth.js are
        ''' additions, not requirements.
        ''' </remarks>
        Private Sub ApplyRoleVisibility()
            Dim isTutor As Boolean = rdoTutor.Checked
            pnlTutorFields.Visible = isTutor
            pnlStudentFields.Visible = Not isTutor
        End Sub

        Protected Sub AccountType_Changed(sender As Object, e As EventArgs) _
            Handles rdoStudent.CheckedChanged, rdoTutor.CheckedChanged
            ApplyRoleVisibility()
        End Sub

        Protected Sub btnRegister_Click(sender As Object, e As EventArgs) Handles btnRegister.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf CreateAccountAsync))
        End Sub

        Private Async Function CreateAccountAsync() As Task
            ' SEC-08 locks out repeated sign-in attempts per account; registration has no
            ' account to lock, so the limit is per client address instead.
            If RateLimiter.IsLimited("register", Context) Then
                litError.Text =
                    $"Too many registration attempts. Wait {RateLimiter.WindowMinutes} minutes and try again."
                pnlError.Visible = True
                Return
            End If

            Dim role As UserRole = If(rdoTutor.Checked, UserRole.Tutor, UserRole.Student)

            Dim account As New Person With {
                .UserName = txtUserName.Text.Trim(),
                .FirstName = txtFirstName.Text.Trim(),
                .LastName = txtLastName.Text.Trim(),
                .Gender = ddlGender.SelectedValue,
                .EmailAddress = txtEmail.Text.Trim(),
                .CellNumber = txtCellNumber.Text.Trim(),
                .Location = txtLocation.Text.Trim(),
                .Role = role
            }

            Dim tutorDetails As TutorProfile = Nothing
            If role = UserRole.Tutor Then
                tutorDetails = New TutorProfile With {
                    .Qualification = txtQualification.Text.Trim(),
                    .ExperienceYears = ParseInteger(txtExperienceYears.Text),
                    .RatePerSession = ParseDecimal(txtRatePerSession.Text)
                }
            End If

            Dim result As ServiceResult(Of Integer) = Await _authentication.RegisterAsync(
                account,
                txtPassword.Text,
                txtConfirmPassword.Text,
                txtStudentNo.Text.Trim(),
                tutorDetails).ConfigureAwait(True)

            If Not result.Success Then
                litError.Text = result.Message
                pnlError.Visible = True
                Return
            End If

            ' Both roles are returned to Login to sign in themselves. No authentication
            ' ticket is issued here, so registering never grants a session on its own.
            '
            ' For a tutor this is also required by BR-26: the account is unapproved, so
            ' there is nothing it could usefully do until an administrator approves it.
            Dim registeredAs As String = If(role = UserRole.Tutor, "tutor", "student")

            Response.Redirect($"~/Account/Login.aspx?registered={registeredAs}", endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

        ''' <summary>
        ''' Never Integer.Parse on user input. An unparseable value becomes -1 so the
        ''' validator rejects it with a message rather than the page throwing.
        ''' </summary>
        Private Shared Function ParseInteger(value As String) As Integer
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
