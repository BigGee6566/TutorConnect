Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Text.RegularExpressions

Namespace Validation

    ''' <summary>
    ''' The outcome of validating one submission: a list of field-level messages.
    ''' </summary>
    Public NotInheritable Class ValidationResult

        Private ReadOnly _errors As New List(Of ValidationError)()

        Public ReadOnly Property Errors As IReadOnlyList(Of ValidationError)
            Get
                Return _errors
            End Get
        End Property

        Public ReadOnly Property IsValid As Boolean
            Get
                Return _errors.Count = 0
            End Get
        End Property

        Public Sub AddError(fieldName As String, message As String)
            _errors.Add(New ValidationError(fieldName, message))
        End Sub

        ''' <summary>All messages joined, for a summary region with role="alert".</summary>
        Public Function ToSummary() As String
            Return String.Join(" ", _errors.Select(Function(e) e.Message))
        End Function

    End Class

    Public NotInheritable Class ValidationError

        Public Sub New(fieldName As String, message As String)
            Me.FieldName = fieldName
            Me.Message = message
        End Sub

        ''' <summary>Used to wire the message to its input via aria-describedby.</summary>
        Public ReadOnly Property FieldName As String

        Public ReadOnly Property Message As String

    End Class

    ''' <summary>
    ''' Registration and profile rules (BR-01, BR-02, BR-03).
    ''' </summary>
    ''' <remarks>
    ''' This class is deliberately free of database and HTTP dependencies so it can be unit
    ''' tested directly. Uniqueness (BR-01) needs the database and is therefore checked by
    ''' the service, not here.
    ''' </remarks>
    Public NotInheritable Class UserValidator

        Public Const MinimumPasswordLength As Integer = 8

        ' Deliberately permissive. Over-strict e-mail patterns reject valid addresses, and
        ' the only authoritative test is whether mail arrives.
        Private Shared ReadOnly EmailPattern As New Regex(
            "^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$",
            RegexOptions.Compiled Or RegexOptions.CultureInvariant)

        ' South African mobile numbers: 10 digits starting 0, or +27 followed by 9.
        Private Shared ReadOnly CellPattern As New Regex(
            "^(0\d{9}|\+27\d{9})$",
            RegexOptions.Compiled Or RegexOptions.CultureInvariant)

        Private Sub New()
            ' Static helper; never instantiated.
        End Sub

        ''' <summary>
        ''' BR-02. Minimum 8 characters with at least one letter and one digit.
        ''' </summary>
        Public Shared Function ValidatePassword(password As String, fieldName As String) As ValidationResult
            Dim result As New ValidationResult()

            If String.IsNullOrEmpty(password) Then
                result.AddError(fieldName, "Enter a password.")
                Return result
            End If

            If password.Length < MinimumPasswordLength Then
                result.AddError(fieldName, $"Your password must be at least {MinimumPasswordLength} characters long.")
            End If

            If Not password.Any(AddressOf Char.IsLetter) Then
                result.AddError(fieldName, "Your password must contain at least one letter.")
            End If

            If Not password.Any(AddressOf Char.IsDigit) Then
                result.AddError(fieldName, "Your password must contain at least one number.")
            End If

            Return result
        End Function

        ''' <summary>BR-03.</summary>
        Public Shared Function ValidatePasswordConfirmation(password As String,
                                                            confirmPassword As String) As ValidationResult
            Dim result As ValidationResult = ValidatePassword(password, "Password")

            If Not String.Equals(password, confirmPassword, StringComparison.Ordinal) Then
                result.AddError("ConfirmPassword", "The two passwords do not match.")
            End If

            Return result
        End Function

        ''' <summary>
        ''' Validates the personal details common to registration and profile editing.
        ''' Uniqueness of username and e-mail is checked by the service against the database.
        ''' </summary>
        Public Shared Function ValidateAccount(userName As String,
                                               firstName As String,
                                               lastName As String,
                                               gender As String,
                                               emailAddress As String,
                                               cellNumber As String,
                                               location As String) As ValidationResult
            Dim result As New ValidationResult()

            RequireText(result, "UserName", userName, "Enter a username.", 50)
            RequireText(result, "FirstName", firstName, "Enter your first name.", 50)
            RequireText(result, "LastName", lastName, "Enter your last name.", 50)
            ' "Location" is the canonical name for this field - never "Address".
            RequireText(result, "Location", location, "Enter your location, for example East London.", 100)

            If String.IsNullOrWhiteSpace(gender) Then
                result.AddError("Gender", "Select a gender.")
            ElseIf Not {"Male", "Female", "Other"}.Contains(gender) Then
                result.AddError("Gender", "Select a gender from the list.")
            End If

            If String.IsNullOrWhiteSpace(emailAddress) Then
                result.AddError("EmailAddress", "Enter an email address.")
            ElseIf emailAddress.Length > 100 Then
                result.AddError("EmailAddress", "That email address is too long.")
            ElseIf Not EmailPattern.IsMatch(emailAddress) Then
                result.AddError("EmailAddress", "Enter a valid email address.")
            End If

            If String.IsNullOrWhiteSpace(cellNumber) Then
                result.AddError("CellNumber", "Enter a cell number.")
            ElseIf Not CellPattern.IsMatch(cellNumber.Replace(" ", String.Empty)) Then
                result.AddError("CellNumber", "Enter a valid cell number, for example 0821234567.")
            End If

            Return result
        End Function

        ''' <summary>Student number: required, and no longer than the column allows.</summary>
        Public Shared Function ValidateStudentNumber(studentNo As String) As ValidationResult
            Dim result As New ValidationResult()
            RequireText(result, "StudentNo", studentNo, "Enter your student number.", 20)
            Return result
        End Function

        ''' <summary>Tutor-specific fields.</summary>
        Public Shared Function ValidateTutorDetails(qualification As String,
                                                    experienceYears As Integer,
                                                    ratePerSession As Decimal) As ValidationResult
            Dim result As New ValidationResult()

            RequireText(result, "Qualification", qualification, "Enter your qualification.", 100)

            If experienceYears < 0 Then
                result.AddError("ExperienceYears", "Years of experience cannot be negative.")
            ElseIf experienceYears > 70 Then
                result.AddError("ExperienceYears", "Enter a realistic number of years of experience.")
            End If

            If ratePerSession < 0D Then
                result.AddError("RatePerSession", "The rate per session cannot be negative.")
            ElseIf ratePerSession > 100000D Then
                result.AddError("RatePerSession", "Enter a realistic rate per session.")
            End If

            Return result
        End Function

        Private Shared Sub RequireText(result As ValidationResult,
                                       fieldName As String,
                                       value As String,
                                       message As String,
                                       maximumLength As Integer)
            If String.IsNullOrWhiteSpace(value) Then
                result.AddError(fieldName, message)
            ElseIf value.Trim().Length > maximumLength Then
                result.AddError(fieldName, $"That value is too long. Use {maximumLength} characters or fewer.")
            End If
        End Sub

    End Class

End Namespace
