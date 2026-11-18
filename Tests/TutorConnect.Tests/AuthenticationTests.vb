Option Strict On
Option Explicit On

Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace TutorConnect.Tests

    ''' <summary>AT-01 to AT-06, plus SEC-01, SEC-08 and BR-04.</summary>
    <TestClass>
    Public Class AuthenticationTests

        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _tutors As New TutorRepository()
        Private ReadOnly _auth As New AuthenticationService()

#Region "SEC-01 password hashing"

        <TestMethod>
        Public Sub SEC01_HashHasTheSpecifiedShape()
            Dim hash As Byte() = Nothing, salt As Byte() = Nothing, iterations As Integer
            PasswordHelper.CreateHash("Correct-Horse-1", hash, salt, iterations)

            Assert.AreEqual(32, hash.Length, "SEC-01 requires a 32-byte subkey")
            Assert.AreEqual(16, salt.Length, "SEC-01 requires a 16-byte salt")
            Assert.AreEqual(100000, iterations, "SEC-01 requires 100,000 iterations")
        End Sub

        <TestMethod>
        Public Sub SEC01_VerifyAcceptsOnlyTheCorrectPassword()
            Dim hash As Byte() = Nothing, salt As Byte() = Nothing, iterations As Integer
            PasswordHelper.CreateHash("Correct-Horse-1", hash, salt, iterations)

            Assert.IsTrue(PasswordHelper.Verify("Correct-Horse-1", hash, salt, iterations))
            Assert.IsFalse(PasswordHelper.Verify("Correct-Horse-2", hash, salt, iterations))
        End Sub

        ''' <summary>
        ''' The same password must never produce the same stored hash, or a stolen database
        ''' would reveal which accounts share a password.
        ''' </summary>
        <TestMethod>
        Public Sub SEC01_EveryHashIsSaltedSeparately()
            Dim h1 As Byte() = Nothing, s1 As Byte() = Nothing, i1 As Integer
            Dim h2 As Byte() = Nothing, s2 As Byte() = Nothing, i2 As Integer

            PasswordHelper.CreateHash("Correct-Horse-1", h1, s1, i1)
            PasswordHelper.CreateHash("Correct-Horse-1", h2, s2, i2)

            Assert.AreNotEqual(Convert.ToBase64String(s1), Convert.ToBase64String(s2), "salts must differ")
            Assert.AreNotEqual(Convert.ToBase64String(h1), Convert.ToBase64String(h2), "hashes must differ")
        End Sub

#End Region

#Region "AT-01 registration"

        <TestMethod>
        Public Async Function AT01_RegisteringAStudentCreatesBothRowsAndHashesThePassword() As Task
            Dim unique As String = TestDatabase.UniqueSuffix()

            Dim account As New Person With {
                .UserName = "at01_" & unique, .FirstName = "Ayanda", .LastName = "Test",
                .Gender = "Female", .EmailAddress = $"at01_{unique}@example.com",
                .CellNumber = "0821110000", .Location = "East London", .Role = UserRole.Student
            }

            Dim result = Await _auth.RegisterAsync(account, "Passw0rd1", "Passw0rd1", "STU-AT01-" & unique, Nothing)
            Assert.IsTrue(result.Success, result.Message)

            Dim created As Person = Await _users.FindByUserNameOrEmailAsync(account.UserName)
            Assert.IsNotNull(created, "Person row")

            Dim student As StudentProfile = Await _users.GetStudentByPersonIdAsync(created.PersonId)
            Assert.IsNotNull(student, "the Student row is written in the same transaction")

            Dim credentials = Await _users.GetCredentialsAsync(created.PersonId)
            Assert.AreNotEqual("Passw0rd1", Text.Encoding.UTF8.GetString(credentials.PasswordHash),
                               "the stored hash must not be the password")
            Assert.AreEqual(32, credentials.PasswordHash.Length)
            Assert.AreEqual(16, credentials.PasswordSalt.Length)
            Assert.AreEqual(100000, credentials.HashIterations)
        End Function

#End Region

#Region "AT-02 duplicate account"

        <TestMethod>
        Public Async Function AT02_ATakenEmailIsRejectedAndWritesNothing() As Task
            Dim unique As String = TestDatabase.UniqueSuffix()

            Dim first As New Person With {
                .UserName = "at02a_" & unique, .FirstName = "First", .LastName = "Test",
                .Gender = "Male", .EmailAddress = $"at02_{unique}@example.com",
                .CellNumber = "0821110001", .Location = "Komani", .Role = UserRole.Student
            }
            Dim seeded = Await _auth.RegisterAsync(first, "Passw0rd1", "Passw0rd1", "STU-AT02A-" & unique, Nothing)
            Assert.IsTrue(seeded.Success, seeded.Message)

            Dim before As Integer = Await TestDatabase.ScalarAsync("SELECT COUNT(*) FROM dbo.Person;")

            Dim duplicate As New Person With {
                .UserName = "at02b_" & unique, .FirstName = "Second", .LastName = "Test",
                .Gender = "Male", .EmailAddress = first.EmailAddress,
                .CellNumber = "0821110002", .Location = "Komani", .Role = UserRole.Student
            }
            Dim result = Await _auth.RegisterAsync(duplicate, "Passw0rd1", "Passw0rd1", "STU-AT02B-" & unique, Nothing)

            Assert.IsFalse(result.Success, "a taken email must be refused")
            Assert.AreEqual(before, Await TestDatabase.ScalarAsync("SELECT COUNT(*) FROM dbo.Person;"),
                            "nothing may be written on a rejected registration")
        End Function

        ''' <summary>
        ''' SEC-08. The message must not say whether it was the username or the email that
        ''' clashed, or the registration form becomes a membership oracle: anyone could test
        ''' an address and learn whether that person is a member.
        ''' </summary>
        <TestMethod>
        Public Async Function SEC08_RegistrationDoesNotRevealWhichFieldWasTaken() As Task
            Dim unique As String = TestDatabase.UniqueSuffix()

            Dim seed As New Person With {
                .UserName = "enum_" & unique, .FirstName = "Seed", .LastName = "Test",
                .Gender = "Other", .EmailAddress = $"enum_{unique}@example.com",
                .CellNumber = "0821110003", .Location = "Mthatha", .Role = UserRole.Student
            }
            Dim seeded = Await _auth.RegisterAsync(seed, "Passw0rd1", "Passw0rd1", "STU-ENUM-" & unique, Nothing)
            Assert.IsTrue(seeded.Success, seeded.Message)

            Dim sameName As New Person With {
                .UserName = seed.UserName, .FirstName = "A", .LastName = "B", .Gender = "Other",
                .EmailAddress = $"free_{unique}@example.com", .CellNumber = "0821110004",
                .Location = "Mthatha", .Role = UserRole.Student
            }
            Dim sameEmail As New Person With {
                .UserName = "free_" & unique, .FirstName = "A", .LastName = "B", .Gender = "Other",
                .EmailAddress = seed.EmailAddress, .CellNumber = "0821110005",
                .Location = "Mthatha", .Role = UserRole.Student
            }

            Dim byName = Await _auth.RegisterAsync(sameName, "Passw0rd1", "Passw0rd1", "S1-" & unique, Nothing)
            Dim byEmail = Await _auth.RegisterAsync(sameEmail, "Passw0rd1", "Passw0rd1", "S2-" & unique, Nothing)

            Assert.IsFalse(byName.Success)
            Assert.IsFalse(byEmail.Success)
            Assert.AreEqual(AuthenticationService.DuplicateAccountMessage, byName.Message)
            Assert.AreEqual(byName.Message, byEmail.Message, "both clashes must return identical wording")
        End Function

#End Region

#Region "AT-03 tutor registration and BR-26"

        <TestMethod>
        Public Async Function AT03_ANewTutorIsUnapprovedAndInvisibleToSearch() As Task
            Dim unique As String = TestDatabase.UniqueSuffix()

            Dim account As New Person With {
                .UserName = "at03_" & unique, .FirstName = "Sipho", .LastName = "Newtutor",
                .Gender = "Male", .EmailAddress = $"at03_{unique}@example.com",
                .CellNumber = "0821110006", .Location = "Mthatha", .Role = UserRole.Tutor
            }
            Dim details As New TutorProfile With {
                .Qualification = "BSc Mathematics", .ExperienceYears = 2, .RatePerSession = 90D
            }

            Dim result = Await _auth.RegisterAsync(account, "Passw0rd1", "Passw0rd1", Nothing, details)
            Assert.IsTrue(result.Success, result.Message)
            StringAssert.Contains(result.Message.ToLowerInvariant(), "approval",
                                  "the tutor must be told approval is pending")

            Dim person = Await _users.FindByUserNameOrEmailAsync(account.UserName)
            Dim profile = Await _users.GetTutorByPersonIdAsync(person.PersonId)
            Assert.IsNotNull(profile, "Tutor row")
            Assert.IsFalse(profile.IsApproved, "a new tutor starts unapproved")

            ' Give them a subject, so approval is the only remaining reason to be excluded.
            Await TestDatabase.ExecuteAsync(
                "INSERT INTO dbo.TutorSubject (TutorId, SubjectId, StudyLevelId) " &
                "SELECT @p0, MIN(s.SubjectId), MIN(l.StudyLevelId) " &
                "FROM dbo.[Subject] AS s CROSS JOIN dbo.StudyLevel AS l;", profile.TutorId)

            Dim rows = Await _tutors.SearchAsync(Nothing, Nothing, Nothing, Nothing, 1, 200)

            Assert.IsFalse(rows.Any(Function(r) r.TutorId = profile.TutorId),
                           "BR-26: an unapproved tutor must never appear in Find Tutor")
        End Function

#End Region

#Region "AT-04 sign-in"

        <TestMethod>
        Public Async Function AT04_EachRoleSignsInAndLandsOnItsOwnDashboard() As Task
            Dim expected As New Dictionary(Of String, UserRole) From {
                {"admin", UserRole.Administrator},
                {"sinesipho_t", UserRole.Student},
                {"nhlawulo_m", UserRole.Tutor}
            }

            For Each pair In expected
                Dim result = Await _auth.AuthenticateAsync(pair.Key, TestDatabase.DevPassword)

                Assert.IsTrue(result.Success, $"{pair.Key}: {result.Message}")
                Assert.AreEqual(pair.Value, result.Data.Role, pair.Key)
                Assert.AreEqual(HomePageFor(pair.Value), RoleHelper.GetHomePage(result.Data.Role), pair.Key)
            Next
        End Function

        <TestMethod>
        Public Async Function AT04_SigningInByEmailAddressAlsoWorks() As Task
            Dim result = Await _auth.AuthenticateAsync("sines@gmail.com", TestDatabase.DevPassword)
            Assert.IsTrue(result.Success, result.Message)
        End Function

        Private Shared Function HomePageFor(role As UserRole) As String
            Select Case role
                Case UserRole.Student : Return "~/Student/Dashboard.aspx"
                Case UserRole.Tutor : Return "~/Tutor/Dashboard.aspx"
                Case Else : Return "~/Admin/Dashboard.aspx"
            End Select
        End Function

#End Region

#Region "AT-05 wrong password"

        ''' <summary>
        ''' A wrong password and an unknown username must be indistinguishable, or the sign-in
        ''' form reveals which accounts exist.
        ''' </summary>
        <TestMethod>
        Public Async Function AT05_WrongPasswordAndUnknownUserAreIndistinguishable() As Task
            Dim wrongPassword = Await _auth.AuthenticateAsync("john_doe", "definitely-not-the-password")
            Dim unknownUser = Await _auth.AuthenticateAsync("no_such_user_at_all", "whatever")

            Assert.IsFalse(wrongPassword.Success)
            Assert.IsFalse(unknownUser.Success)
            Assert.AreEqual(AuthenticationService.GenericFailureMessage, wrongPassword.Message)
            Assert.AreEqual(wrongPassword.Message, unknownUser.Message, "the two must be byte-identical")
        End Function

        <TestMethod>
        Public Async Function AT05_FailureCounterIncrementsThenResets() As Task
            ' Start from a known counter rather than whatever an earlier test left behind.
            Dim target = Await _users.FindByUserNameOrEmailAsync("john_doe")
            Await _users.ResetFailedLoginAsync(target.PersonId)

            Await _auth.AuthenticateAsync("john_doe", "wrong")
            Dim afterFailure = Await _users.FindByUserNameOrEmailAsync("john_doe")
            Assert.AreEqual(1, afterFailure.FailedLoginCount)

            Await _auth.AuthenticateAsync("john_doe", TestDatabase.DevPassword)
            Dim afterSuccess = Await _users.FindByUserNameOrEmailAsync("john_doe")
            Assert.AreEqual(0, afterSuccess.FailedLoginCount, "a good sign-in clears the counter")
        End Function

#End Region

#Region "AT-06 deactivated account"

        <TestMethod>
        Public Async Function AT06_ADeactivatedAccountCannotSignIn() As Task
            Dim target = Await _users.FindByUserNameOrEmailAsync("john_doe")
            Await _users.SetActiveAsync(target.PersonId, False)

            Try
                Dim result = Await _auth.AuthenticateAsync("john_doe", TestDatabase.DevPassword)

                Assert.IsFalse(result.Success)
                Assert.AreEqual(AuthenticationService.InactiveAccountMessage, result.Message)
                Assert.IsFalse(Await _auth.IsStillActiveAsync(target.PersonId),
                               "BR-25 uses the same check to end a live session")
            Finally
                ' Restore even on failure, or every later test using john_doe fails too.
                ' VB forbids Await inside Finally, so these block; they are single indexed
                ' updates with no synchronisation context to deadlock against.
                _users.SetActiveAsync(target.PersonId, True).GetAwaiter().GetResult()
                _users.ResetFailedLoginAsync(target.PersonId).GetAwaiter().GetResult()
            End Try
        End Function

#End Region

#Region "SEC-08 lockout"

        ''' <summary>
        ''' After the threshold the *correct* password is refused, and the message still does
        ''' not admit that a lockout is in force - saying so would confirm the username exists.
        ''' </summary>
        <TestMethod>
        Public Async Function SEC08_LockoutAfterFiveFailuresHidesItself() As Task
            Dim target = Await _users.FindByUserNameOrEmailAsync("thabo_j")
            Assert.IsNotNull(target, "seed user thabo_j")
            Await _users.ResetFailedLoginAsync(target.PersonId)

            Try
                For attempt = 1 To 5
                    Await _auth.AuthenticateAsync("thabo_j", "wrong-password")
                Next

                Dim locked = Await _users.FindByUserNameOrEmailAsync("thabo_j")
                Assert.IsTrue(locked.LockoutEndUtc.HasValue, "LockoutEndUtc must be set after 5 failures")

                Dim duringLockout = Await _auth.AuthenticateAsync("thabo_j", TestDatabase.DevPassword)
                Assert.IsFalse(duringLockout.Success, "the correct password is refused while locked")
                Assert.AreEqual(AuthenticationService.GenericFailureMessage, duringLockout.Message,
                                "the lockout must not reveal itself")
            Finally
                _users.ResetFailedLoginAsync(target.PersonId).GetAwaiter().GetResult()
            End Try
        End Function

#End Region

#Region "BR-04 role exclusion"

        ''' <summary>
        ''' Enforced by a database trigger rather than by application code, so it holds even
        ''' for a write that never goes through the services.
        ''' </summary>
        <TestMethod>
        Public Async Function BR04_APersonCannotBeBothStudentAndTutor() As Task
            Dim tutorPerson = Await _users.FindByUserNameOrEmailAsync("nhlawulo_m")

            Await Assert.ThrowsExceptionAsync(Of SqlException)(
                Function()
                    Return TestDatabase.ExecuteAsync(
                        "INSERT INTO dbo.Student (StudentNo, PersonId) VALUES (@p0, @p1);",
                        "STU-BR04-" & TestDatabase.UniqueSuffix(), tutorPerson.PersonId)
                End Function)
        End Function

#End Region

    End Class

End Namespace
