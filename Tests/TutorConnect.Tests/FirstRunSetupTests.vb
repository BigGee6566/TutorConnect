Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Services

Namespace TutorConnect.Tests

    ''' <summary>
    ''' First-run setup: the only route by which an administrator can be created without
    ''' one already existing.
    ''' </summary>
    ''' <remarks>
    ''' The success path needs an installation with no administrator at all, which the
    ''' seeded database is not. Rather than delete the seeded administrator - which other
    ''' tests depend on and which foreign keys would fight - these tests run against a
    ''' second, empty database built from the same schema script, and put the connection
    ''' back afterwards.
    ''' </remarks>
    <TestClass>
    Public Class FirstRunSetupTests

        Private ReadOnly _auth As New AuthenticationService()
        Private ReadOnly _users As New UserRepository()

        Private Shared Function NewAdministrator(unique As String) As Person
            Return New Person With {
                .UserName = "root_" & unique, .FirstName = "First", .LastName = "Administrator",
                .Gender = "Other", .EmailAddress = $"root_{unique}@example.com",
                .CellNumber = "0800000001", .Location = "East London"
            }
        End Function

#Region "The closed path - the seeded database already has an administrator"

        <TestMethod>
        Public Async Function Setup_IsNotRequiredOnceAnAdministratorExists() As Task
            Assert.IsTrue(Await _users.AdministratorExistsAsync(), "the seed creates one")
            Assert.IsFalse(Await _auth.SetupRequiredAsync())
        End Function

        ''' <summary>
        ''' The page hides its form when setup is closed, but a hidden form can still be
        ''' posted to. The refusal has to live in the service, or this is a back door that
        ''' mints an administrator on a running system.
        ''' </summary>
        <TestMethod>
        Public Async Function Setup_RefusesToCreateASecondAdministratorEvenWhenPostedDirectly() As Task
            Dim unique As String = TestDatabase.UniqueSuffix()
            Dim before As Integer = Await AdministratorCountAsync()

            Dim result = Await _auth.CreateFirstAdministratorAsync(
                NewAdministrator(unique), "Passw0rd1", "Passw0rd1")

            Assert.IsFalse(result.Success)
            Assert.AreEqual(AuthenticationService.SetupClosedMessage, result.Message)
            Assert.AreEqual(before, Await AdministratorCountAsync(), "no account may be written")
        End Function

        ''' <summary>
        ''' A deactivated administrator is still an account someone controls. Treating the
        ''' system as unconfigured because the only one was switched off would let anyone
        ''' create a fresh administrator for themselves.
        ''' </summary>
        <TestMethod>
        Public Async Function Setup_StaysClosedWhenTheOnlyAdministratorIsDeactivated() As Task
            Dim administrator = Await _users.FindByUserNameOrEmailAsync("admin")
            Await TestDatabase.ExecuteAsync(
                "UPDATE dbo.Person SET IsActive = 0 WHERE PersonId = @p0;", administrator.PersonId)

            Try
                Assert.IsFalse(Await _auth.SetupRequiredAsync())
            Finally
                TestDatabase.ExecuteAsync(
                    "UPDATE dbo.Person SET IsActive = 1 WHERE PersonId = @p0;",
                    administrator.PersonId).GetAwaiter().GetResult()
            End Try
        End Function

#End Region

#Region "The open path - an installation built from the scripts and nothing else"

        <TestMethod>
        Public Async Function Setup_CreatesTheFirstAdministratorAndThenClosesItself() As Task
            Await TestDatabase.UsingEmptyInstallationAsync(
                Async Function()
                    Assert.IsTrue(Await _auth.SetupRequiredAsync(),
                                  "a database built from the scripts alone has no people in it")

                    Dim unique As String = TestDatabase.UniqueSuffix()
                    Dim account As Person = NewAdministrator(unique)

                    Dim created = Await _auth.CreateFirstAdministratorAsync(account, "Passw0rd1", "Passw0rd1")
                    Assert.IsTrue(created.Success, created.Message)

                    Dim stored = Await _users.FindByUserNameOrEmailAsync(account.UserName)
                    Assert.IsNotNull(stored)
                    Assert.AreEqual(UserRole.Administrator, stored.Role)
                    Assert.IsTrue(stored.IsActive)

                    ' The credentials chosen on the form are the ones that work.
                    Dim signedIn = Await _auth.AuthenticateAsync(account.UserName, "Passw0rd1")
                    Assert.IsTrue(signedIn.Success, signedIn.Message)

                    ' And the door shuts behind them.
                    Assert.IsFalse(Await _auth.SetupRequiredAsync())

                    Dim second = Await _auth.CreateFirstAdministratorAsync(
                        NewAdministrator(TestDatabase.UniqueSuffix()), "Passw0rd1", "Passw0rd1")
                    Assert.IsFalse(second.Success, "setup is single-use")
                End Function)
        End Function

        <TestMethod>
        Public Async Function Setup_ValidatesTheSubmissionBeforeCreatingAnything() As Task
            Await TestDatabase.UsingEmptyInstallationAsync(
                Async Function()
                    Dim mismatched = Await _auth.CreateFirstAdministratorAsync(
                        NewAdministrator(TestDatabase.UniqueSuffix()), "Passw0rd1", "Passw0rd2")

                    Assert.IsFalse(mismatched.Success, "the confirmation must match")
                    Assert.IsTrue(Await _auth.SetupRequiredAsync(),
                                  "a rejected submission must leave setup open")
                    Assert.AreEqual(0, Await AdministratorCountAsync())
                End Function)
        End Function

#End Region

        Private Shared Function AdministratorCountAsync() As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Person WHERE [Role] = 'Administrator';")
        End Function

    End Class

End Namespace
