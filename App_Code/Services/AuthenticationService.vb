Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Validation

Namespace Services

    ''' <summary>
    ''' Signing in, registering, and the rules that gate both.
    ''' </summary>
    Public Class AuthenticationService
        Implements IAuthenticationService

        ''' <summary>
        ''' The only message shown for a failed sign-in, whatever the actual cause.
        ''' </summary>
        ''' <remarks>
        ''' Wrong password, unknown username, and locked-out account all produce this exact
        ''' text. Distinguishing them tells an attacker which usernames exist and when a
        ''' lockout is in force, which is a free account-enumeration oracle.
        ''' </remarks>
        Public Const GenericFailureMessage As String = "Incorrect username or password."

        Public Const InactiveAccountMessage As String = "This account is not active. Contact the administrator."

        ''' <summary>
        ''' Shown when either the username or the email address is already registered.
        ''' </summary>
        ''' <remarks>
        ''' Deliberately does not say which. Distinguishing them lets anyone test an email
        ''' address against the platform and learn whether that person has an account -
        ''' the same enumeration problem the sign-in message avoids.
        ''' </remarks>
        Public Const DuplicateAccountMessage As String =
            "An account already exists with that username or email address. " &
            "Try signing in, or use different details."

        ''' <summary>SEC-08: 5 failures in 15 minutes.</summary>
        Public Const LockoutThreshold As Integer = 5

        Public Const LockoutMinutes As Integer = 15

        Private ReadOnly _users As UserRepository

        Public Sub New()
            Me.New(New UserRepository())
        End Sub

        ''' <summary>Constructor used by tests to supply a repository against a test database.</summary>
        Public Sub New(users As UserRepository)
            If users Is Nothing Then Throw New ArgumentNullException(NameOf(users))
            _users = users
        End Sub

        ''' <inheritdoc/>
        Public Async Function AuthenticateAsync(userNameOrEmail As String,
                                                password As String) As Task(Of ServiceResult(Of Person)) _
            Implements IAuthenticationService.AuthenticateAsync

            If String.IsNullOrWhiteSpace(userNameOrEmail) OrElse String.IsNullOrEmpty(password) Then
                Return ServiceResult(Of Person).Fail(GenericFailureMessage)
            End If

            Dim account As Person = Await _users.FindByUserNameOrEmailAsync(userNameOrEmail).ConfigureAwait(False)

            ' No such account. Same message as a wrong password (AT-05).
            If account Is Nothing Then
                Return ServiceResult(Of Person).Fail(GenericFailureMessage)
            End If

            ' BR-25 / AT-06. The one case the user is told about, because only an
            ' administrator can resolve it.
            If Not account.IsActive Then
                Return ServiceResult(Of Person).Fail(InactiveAccountMessage)
            End If

            ' SEC-08. A locked account gets the generic message: revealing the lockout
            ' would confirm the username exists.
            If account.LockoutEndUtc.HasValue AndAlso account.LockoutEndUtc.Value > Date.UtcNow Then
                Return ServiceResult(Of Person).Fail(GenericFailureMessage)
            End If

            Dim credentials As PersonCredentials =
                Await _users.GetCredentialsAsync(account.PersonId).ConfigureAwait(False)

            If credentials Is Nothing Then
                Return ServiceResult(Of Person).Fail(GenericFailureMessage)
            End If

            Dim passwordMatches As Boolean = PasswordHelper.Verify(
                password, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashIterations)

            If Not passwordMatches Then
                Await _users.RegisterFailedLoginAsync(account.PersonId, LockoutThreshold, LockoutMinutes).ConfigureAwait(False)
                Return ServiceResult(Of Person).Fail(GenericFailureMessage)
            End If

            Await _users.ResetFailedLoginAsync(account.PersonId).ConfigureAwait(False)

            ' Transparently upgrade a hash made with an older work factor.
            If PasswordHelper.NeedsRehash(credentials.HashIterations) Then
                Dim hash As Byte() = Nothing, salt As Byte() = Nothing, iterations As Integer
                PasswordHelper.CreateHash(password, hash, salt, iterations)
                Await _users.SetPasswordAsync(account.PersonId, hash, salt, iterations).ConfigureAwait(False)
            End If

            Return ServiceResult(Of Person).Ok(account)
        End Function

        ''' <inheritdoc/>
        Public Async Function RegisterAsync(account As Person,
                                            password As String,
                                            confirmPassword As String,
                                            studentNo As String,
                                            tutorDetails As TutorProfile) As Task(Of ServiceResult(Of Integer)) _
            Implements IAuthenticationService.RegisterAsync

            If account Is Nothing Then Throw New ArgumentNullException(NameOf(account))

            ' 1. Shape of the submission.
            Dim validation As ValidationResult = UserValidator.ValidateAccount(
                account.UserName, account.FirstName, account.LastName, account.Gender,
                account.EmailAddress, account.CellNumber, account.Location)

            For Each item In UserValidator.ValidatePasswordConfirmation(password, confirmPassword).Errors
                validation.AddError(item.FieldName, item.Message)
            Next

            Select Case account.Role
                Case UserRole.Student
                    For Each item In UserValidator.ValidateStudentNumber(studentNo).Errors
                        validation.AddError(item.FieldName, item.Message)
                    Next

                Case UserRole.Tutor
                    If tutorDetails Is Nothing Then
                        validation.AddError("Qualification", "Enter your tutoring details.")
                    Else
                        For Each item In UserValidator.ValidateTutorDetails(
                            tutorDetails.Qualification, tutorDetails.ExperienceYears, tutorDetails.RatePerSession).Errors
                            validation.AddError(item.FieldName, item.Message)
                        Next
                    End If

                Case UserRole.Administrator
                    ' Never by self-registration. The first one comes from Setup.aspx on an
                    ' empty installation; after that, only an existing administrator can
                    ' create another.
                    Return ServiceResult(Of Integer).Fail("You cannot register an administrator account.")
            End Select

            If Not validation.IsValid Then
                Return ServiceResult(Of Integer).Fail(validation.ToSummary())
            End If

            ' 2. BR-01 uniqueness. The database's UNIQUE constraints are the real guard;
            '    these checks exist to produce a usable message instead of an exception
            '    (AT-02).
            '
            '    Both clashes return the SAME wording. Saying "that email is already
            '    registered" turns this form into an oracle: submit an address, learn
            '    whether that person has an account here. The user still learns enough to
            '    act - change the username or sign in instead - without the form
            '    confirming who is a member.
            Dim userNameTaken As Boolean =
                Await _users.UserNameExistsAsync(account.UserName).ConfigureAwait(False)

            Dim emailTaken As Boolean =
                Await _users.EmailExistsAsync(account.EmailAddress).ConfigureAwait(False)

            If userNameTaken OrElse emailTaken Then
                Return ServiceResult(Of Integer).Fail(DuplicateAccountMessage)
            End If

            ' 3. Hash, then write Person + profile in one transaction.
            Dim passwordHash As Byte() = Nothing
            Dim passwordSalt As Byte() = Nothing
            Dim hashIterations As Integer
            PasswordHelper.CreateHash(password, passwordHash, passwordSalt, hashIterations)

            account.IsActive = True

            Dim personId As Integer = Await _users.CreateAccountAsync(
                account, passwordHash, passwordSalt, hashIterations, studentNo, tutorDetails).ConfigureAwait(False)

            Dim message As String =
                If(account.Role = UserRole.Tutor,
                   "Your tutor account is awaiting administrator approval.",
                   String.Empty)

            Return ServiceResult(Of Integer).Ok(personId, message)
        End Function

        ''' <summary>Shown when Setup.aspx is reached on a system that is already configured.</summary>
        Public Const SetupClosedMessage As String =
            "This installation already has an administrator, so first-run setup is closed. " &
            "Sign in instead, or ask an existing administrator for an account."

        ''' <inheritdoc/>
        Public Async Function SetupRequiredAsync() As Task(Of Boolean) _
            Implements IAuthenticationService.SetupRequiredAsync

            Return Not Await _users.AdministratorExistsAsync().ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Async Function CreateFirstAdministratorAsync(account As Person,
                                                            password As String,
                                                            confirmPassword As String) As Task(Of ServiceResult(Of Integer)) _
            Implements IAuthenticationService.CreateFirstAdministratorAsync

            If account Is Nothing Then Throw New ArgumentNullException(NameOf(account))

            ' Checked first, so a submission to a closed setup page is refused before any
            ' validation message can reveal what the form would have accepted.
            If Await _users.AdministratorExistsAsync().ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail(SetupClosedMessage)
            End If

            Dim validation As ValidationResult = UserValidator.ValidateAccount(
                account.UserName, account.FirstName, account.LastName, account.Gender,
                account.EmailAddress, account.CellNumber, account.Location)

            For Each item In UserValidator.ValidatePasswordConfirmation(password, confirmPassword).Errors
                validation.AddError(item.FieldName, item.Message)
            Next

            If Not validation.IsValid Then
                Return ServiceResult(Of Integer).Fail(validation.ToSummary())
            End If

            If Await _users.UserNameExistsAsync(account.UserName).ConfigureAwait(False) OrElse
               Await _users.EmailExistsAsync(account.EmailAddress).ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail(DuplicateAccountMessage)
            End If

            Dim passwordHash As Byte() = Nothing
            Dim passwordSalt As Byte() = Nothing
            Dim hashIterations As Integer
            PasswordHelper.CreateHash(password, passwordHash, passwordSalt, hashIterations)

            account.Role = UserRole.Administrator
            account.IsActive = True

            ' No profile row: an administrator has neither a Student nor a Tutor record (D-05).
            Dim personId As Integer = Await _users.CreateAccountAsync(
                account, passwordHash, passwordSalt, hashIterations, Nothing, Nothing).ConfigureAwait(False)

            Return ServiceResult(Of Integer).Ok(
                personId, "The administrator account was created. Sign in to continue.")
        End Function

        ''' <inheritdoc/>
        Public Async Function IsStillActiveAsync(personId As Integer) As Task(Of Boolean) _
            Implements IAuthenticationService.IsStillActiveAsync

            Dim account As Person = Await _users.GetByIdAsync(personId).ConfigureAwait(False)
            Return account IsNot Nothing AndAlso account.IsActive
        End Function

        ''' <inheritdoc/>
        Public Async Function GetAccountAsync(personId As Integer) As Task(Of ServiceResult(Of Person)) _
            Implements IAuthenticationService.GetAccountAsync

            If personId <= 0 Then
                Return ServiceResult(Of Person).Fail("You are not signed in.")
            End If

            Dim account As Person = Await _users.GetByIdAsync(personId).ConfigureAwait(False)

            If account Is Nothing Then
                Return ServiceResult(Of Person).Fail("That account no longer exists.")
            End If

            Return ServiceResult(Of Person).Ok(account)
        End Function

        ''' <inheritdoc/>
        Public Async Function ChangePasswordAsync(personId As Integer,
                                                  currentPassword As String,
                                                  newPassword As String,
                                                  confirmPassword As String) As Task(Of ServiceResult) _
            Implements IAuthenticationService.ChangePasswordAsync

            Dim validation As ValidationResult =
                UserValidator.ValidatePasswordConfirmation(newPassword, confirmPassword)

            If Not validation.IsValid Then
                Return ServiceResult.Fail(validation.ToSummary())
            End If

            Dim credentials As PersonCredentials = Await _users.GetCredentialsAsync(personId).ConfigureAwait(False)
            If credentials Is Nothing Then
                Return ServiceResult.Fail("That account no longer exists.")
            End If

            If Not PasswordHelper.Verify(currentPassword, credentials.PasswordHash,
                                         credentials.PasswordSalt, credentials.HashIterations) Then
                Return ServiceResult.Fail("Your current password is not correct.")
            End If

            Dim hash As Byte() = Nothing, salt As Byte() = Nothing, iterations As Integer
            PasswordHelper.CreateHash(newPassword, hash, salt, iterations)

            Dim updated As Boolean = Await _users.SetPasswordAsync(personId, hash, salt, iterations).ConfigureAwait(False)

            Return If(updated,
                      ServiceResult.Ok("Your password has been changed."),
                      ServiceResult.Fail("Your password could not be changed. Try again."))
        End Function

    End Class

End Namespace
