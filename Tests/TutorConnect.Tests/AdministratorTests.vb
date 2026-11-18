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
    ''' AT-23 to AT-25, review moderation, and the administrator's read-only views.
    ''' </summary>
    <TestClass>
    Public Class AdministratorTests

        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _subjects As New SubjectRepository()
        Private ReadOnly _reviewRepository As New ReviewRepository()
        Private ReadOnly _admin As New AdminService()
        Private ReadOnly _auth As New AuthenticationService()
        Private ReadOnly _search As New TutorSearchService()
        Private ReadOnly _reviews As New ReviewService()

        Private _scenario As TestScenario
        Private _administrator As Person

        <TestInitialize>
        Public Sub Setup()
            SetupAsync().GetAwaiter().GetResult()
        End Sub

        Private Async Function SetupAsync() As Task
            _scenario = Await TestScenario.CreateAsync()
            _administrator = Await _users.FindByUserNameOrEmailAsync("admin")
            Assert.IsNotNull(_administrator, "seed user admin")
        End Function

        ''' <summary>
        ''' Puts back anything a test may have switched off. Without this, a failure part way
        ''' through leaves a deactivated user or an unapproved tutor behind and turns one
        ''' failure into several.
        ''' </summary>
        <TestCleanup>
        Public Sub Restore()
            TestDatabase.ExecuteAsync(
                "UPDATE dbo.Tutor SET IsApproved = 1 WHERE TutorId = @p0;",
                _scenario.TutorId).GetAwaiter().GetResult()

            TestDatabase.ExecuteAsync(
                "UPDATE dbo.Person SET IsActive = 1;").GetAwaiter().GetResult()

            TestDatabase.ExecuteAsync(
                "UPDATE dbo.[Subject] SET IsActive = 1;").GetAwaiter().GetResult()
        End Sub

#Region "AT-23 approving a tutor"

        <TestMethod>
        Public Async Function AT23_ApprovingATutorMakesThemSearchableImmediately() As Task
            Await TestDatabase.ExecuteAsync("UPDATE dbo.Tutor SET IsApproved = 0 WHERE TutorId = @p0;",
                                            _scenario.TutorId)

            Dim before = (Await _search.SearchAsync(Nothing, Nothing, Nothing, Nothing, 1, 200)).Data
            Assert.IsFalse(before.Any(Function(r) r.TutorId = _scenario.TutorId), "absent while unapproved")

            Dim approved = Await _admin.ApproveTutorAsync(_administrator.PersonId, _scenario.TutorId, True)
            Assert.IsTrue(approved.Success, approved.Message)

            Dim after = (Await _search.SearchAsync(Nothing, Nothing, Nothing, Nothing, 1, 200)).Data
            Assert.IsTrue(after.Any(Function(r) r.TutorId = _scenario.TutorId),
                          "visible with no further step")
        End Function

#End Region

#Region "AT-24 deactivating a user"

        <TestMethod>
        Public Async Function AT24_DeactivationEndsSignInAndIsReversible() As Task
            Dim target = Await _users.FindByUserNameOrEmailAsync("john_doe")

            Dim before = Await _auth.AuthenticateAsync("john_doe", TestDatabase.DevPassword)
            Assert.IsTrue(before.Success, "the account signs in beforehand: " & before.Message)

            Dim deactivated = Await _admin.SetUserActiveAsync(_administrator.PersonId, target.PersonId, False)
            Assert.IsTrue(deactivated.Success, deactivated.Message)

            Dim after = Await _auth.AuthenticateAsync("john_doe", TestDatabase.DevPassword)
            Assert.IsFalse(after.Success, "sign-in must now be refused")
            Assert.AreEqual(AuthenticationService.InactiveAccountMessage, after.Message)

            Dim reactivated = Await _admin.SetUserActiveAsync(_administrator.PersonId, target.PersonId, True)
            Assert.IsTrue(reactivated.Success, reactivated.Message)
            Assert.IsTrue((Await _auth.AuthenticateAsync("john_doe", TestDatabase.DevPassword)).Success,
                          "access is restored")
        End Function

        ''' <summary>
        ''' BR-25. <c>IsStillActiveAsync</c> is what <c>BasePage</c> calls on every request of
        ''' an authenticated session, so a False here is precisely what ends a session that
        ''' is already in progress - the "next request" the acceptance test asks about.
        ''' </summary>
        <TestMethod>
        Public Async Function BR25_ALiveSessionIsRefusedOnItsNextRequest() As Task
            Dim target = Await _users.FindByUserNameOrEmailAsync("john_doe")

            Assert.IsTrue(Await _auth.IsStillActiveAsync(target.PersonId), "active to begin with")

            Await _admin.SetUserActiveAsync(_administrator.PersonId, target.PersonId, False)

            Assert.IsFalse(Await _auth.IsStillActiveAsync(target.PersonId),
                           "the running session must be ended on its next request")
        End Function

        <TestMethod>
        Public Async Function AT24_TheDeactivatedUserIsNotified() As Task
            Dim target = Await _users.FindByUserNameOrEmailAsync("john_doe")

            Dim before = Await DeactivationNoticesAsync(target.PersonId)
            Await _admin.SetUserActiveAsync(_administrator.PersonId, target.PersonId, False)

            Assert.AreEqual(before + 1, Await DeactivationNoticesAsync(target.PersonId))
        End Function

        ''' <summary>
        ''' An administrator who deactivates their own account locks the last administrator
        ''' out of the system, with no way back in through the application.
        ''' </summary>
        <TestMethod>
        Public Async Function AT24_AnAdministratorCannotDeactivateThemselves() As Task
            Dim result = Await _admin.SetUserActiveAsync(
                _administrator.PersonId, _administrator.PersonId, False)

            Assert.IsFalse(result.Success, result.Message)
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT CAST(IsActive AS INT) FROM dbo.Person WHERE PersonId = @p0;", _administrator.PersonId))
        End Function

        Private Shared Function DeactivationNoticesAsync(personId As Integer) As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification " &
                "WHERE PersonId = @p0 AND NotificationType = 'AccountDeactivated';", personId)
        End Function

#End Region

#Region "AT-25 subject maintenance"

        <TestMethod>
        Public Async Function AT25_ASubjectInUseCannotBeDeletedAndDeactivationIsOffered() As Task
            Dim inUse = Await TestDatabase.ScalarAsync(
                "SELECT SubjectId FROM dbo.[Subject] WHERE SubjectName = 'Information Systems';")

            Dim blocked = Await _admin.DeleteSubjectAsync(_administrator.PersonId, inUse)

            Assert.IsFalse(blocked.Success, "deleting it would orphan live tutoring records")
            StringAssert.Contains(blocked.Message.ToLowerInvariant(), "deactivate",
                                  "the message must offer the alternative, not merely refuse")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.[Subject] WHERE SubjectId = @p0;", inUse), "the subject survives")
        End Function

        <TestMethod>
        Public Async Function AT25_DeactivatingASubjectRemovesItFromTheDropdowns() As Task
            Dim inUse = Await TestDatabase.ScalarAsync(
                "SELECT SubjectId FROM dbo.[Subject] WHERE SubjectName = 'Information Systems';")

            Dim deactivated = Await _admin.SetSubjectActiveAsync(_administrator.PersonId, inUse, False)
            Assert.IsTrue(deactivated.Success, deactivated.Message)

            Dim offered = Await _subjects.GetSubjectsAsync()
            Assert.IsFalse(offered.Any(Function(s) s.SubjectId = inUse),
                           "a deactivated subject must not be offered for new work")

            Await _admin.SetSubjectActiveAsync(_administrator.PersonId, inUse, True)
            Assert.IsTrue((Await _subjects.GetSubjectsAsync()).Any(Function(s) s.SubjectId = inUse),
                          "reactivating puts it back")
        End Function

        <TestMethod>
        Public Async Function AT25_AnUnusedSubjectIsAddedAndDeletedCleanly() As Task
            Dim name As String = "Physics " & TestDatabase.UniqueSuffix()

            Dim added = Await _admin.AddSubjectAsync(_administrator.PersonId, name)
            Assert.IsTrue(added.Success, added.Message)

            Dim subjectId = Await TestDatabase.ScalarAsync(
                "SELECT SubjectId FROM dbo.[Subject] WHERE SubjectName = @p0;", name)
            Assert.AreNotEqual(0, subjectId)

            Dim deleted = Await _admin.DeleteSubjectAsync(_administrator.PersonId, subjectId)
            Assert.IsTrue(deleted.Success, deleted.Message)

            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.[Subject] WHERE SubjectId = @p0;", subjectId))
        End Function

        <TestMethod>
        Public Async Function AT25_ADuplicateSubjectNameIsRefused() As Task
            Dim result = Await _admin.AddSubjectAsync(_administrator.PersonId, "Mathematics")

            Assert.IsFalse(result.Success, result.Message)
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.[Subject] WHERE SubjectName = 'Mathematics';"))
        End Function

#End Region

#Region "Review moderation"

        ''' <summary>
        ''' Builds a completed, reviewed session and returns the review's identifier.
        ''' </summary>
        Private Async Function NewReviewAsync(rating As Byte, comment As String) As Task(Of Integer)
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim created = Await _reviews.CreateAsync(_scenario.StudentId, fixture.BookingId, rating, comment)
            Assert.IsTrue(created.Success, "fixture: create review - " & created.Message)

            Return created.Data
        End Function

        <TestMethod>
        Public Async Function Moderation_HidingWithoutAReasonIsRefused() As Task
            Dim reviewId = Await NewReviewAsync(CByte(1), "Unfair and abusive remark.")

            Dim result = Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, True, "   ")

            Assert.IsFalse(result.Success, "a moderation action with no stated reason is not an audit trail")
            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT CAST(IsHidden AS INT) FROM dbo.Review WHERE ReviewId = @p0;", reviewId))
        End Function

        ''' <summary>
        ''' The part that is easy to get wrong: hiding has to remove the review from every
        ''' public surface, including the average rating and the rating shown in Find Tutor,
        ''' not merely from the list it was reported on.
        ''' </summary>
        <TestMethod>
        Public Async Function Moderation_AHiddenReviewLeavesEveryPublicSurface() As Task
            Dim reviewId = Await NewReviewAsync(CByte(1), "Unfair and abusive remark.")

            Dim before = Await _reviewRepository.GetTutorRatingAsync(_scenario.TutorId)
            Assert.IsTrue(before.ReviewCount > 0, "it counts toward the rating to begin with")

            Dim hidden = Await _admin.SetReviewHiddenAsync(
                _administrator.PersonId, reviewId, True, "Abusive language.")
            Assert.IsTrue(hidden.Success, hidden.Message)

            Dim tutorList = (Await _reviews.GetForTutorAsync(_scenario.TutorId)).Data
            Assert.IsFalse(tutorList.Any(Function(r) r.ReviewId = reviewId), "gone from the tutor's profile")

            Dim after = Await _reviewRepository.GetTutorRatingAsync(_scenario.TutorId)
            Assert.AreEqual(before.ReviewCount - 1, after.ReviewCount, "excluded from the average rating")

            Dim searchRow = (Await _search.SearchAsync(Nothing, Nothing, Nothing, Nothing, 1, 200)).Data.
                FirstOrDefault(Function(r) r.TutorId = _scenario.TutorId)
            Assert.IsNotNull(searchRow, "the tutor is still searchable")
            Assert.AreEqual(after.ReviewCount, searchRow.ReviewCount,
                            "Find Tutor's rating must agree with the repository")
        End Function

        ''' <summary>
        ''' Concealing a review from the person who wrote it would be dishonest, and they
        ''' need to know why it was hidden.
        ''' </summary>
        <TestMethod>
        Public Async Function Moderation_TheAuthorStillSeesTheirReviewAndTheReason() As Task
            Dim reviewId = Await NewReviewAsync(CByte(1), "Unfair and abusive remark.")

            Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, True, "Abusive language.")

            Dim mine = (Await _reviews.GetForStudentAsync(_scenario.StudentId)).Data.
                FirstOrDefault(Function(r) r.ReviewId = reviewId)

            Assert.IsNotNull(mine, "the author still sees it")
            Assert.IsTrue(mine.IsHidden, "marked as hidden")
            Assert.AreEqual("Abusive language.", mine.HiddenReason)
        End Function

        <TestMethod>
        Public Async Function Moderation_TheAuditTrailRecordsWhoHidItAndWhen() As Task
            Dim reviewId = Await NewReviewAsync(CByte(1), "Unfair and abusive remark.")

            Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, True, "Abusive language.")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Review " &
                "WHERE ReviewId = @p0 AND HiddenBy = @p1 AND HiddenAt IS NOT NULL;",
                reviewId, _administrator.PersonId))
        End Function

        <TestMethod>
        Public Async Function Moderation_RestoringRecoversTheRatingAndClearsTheStaleReason() As Task
            Dim reviewId = Await NewReviewAsync(CByte(1), "Unfair and abusive remark.")

            Dim before = Await _reviewRepository.GetTutorRatingAsync(_scenario.TutorId)

            Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, True, "Abusive language.")
            Dim restored = Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, False, Nothing)
            Assert.IsTrue(restored.Success, restored.Message)

            Dim after = Await _reviewRepository.GetTutorRatingAsync(_scenario.TutorId)
            Assert.AreEqual(before.ReviewCount, after.ReviewCount, "the rating recovers")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Review " &
                "WHERE ReviewId = @p0 AND HiddenReason IS NULL AND HiddenBy IS NULL;", reviewId),
                "a restored review must not keep the reason it was hidden for")
        End Function

#End Region

#Region "Administrator actions are audited (Part 12.7)"

        ''' <summary>
        ''' Every administrator mutation writes a record naming who did it.
        ''' </summary>
        ''' <remarks>
        ''' These were missing entirely: `AdminService` wrote no audit records at all, while
        ''' the Audit Log page offered filters for tutor approval, account activation and
        ''' review moderation. A filter that can never match is the same defect as a button
        ''' that does nothing.
        ''' </remarks>
        ''' <remarks>
        ''' Asserted as a delta, not an absolute count. Three other tests in this class also
        ''' deactivate john_doe, and each now writes its own audit record - so "exactly one"
        ''' is only true if this test happens to run first. The sibling tests below get away
        ''' with an absolute count because each creates a fresh entity of its own.
        ''' </remarks>
        <TestMethod>
        Public Async Function DeactivatingAUserIsAudited() As Task
            Dim target = Await _users.FindByUserNameOrEmailAsync("john_doe")

            Dim deactivatedBefore = Await AuditCountAsync("Person", target.PersonId, "UserDeactivated")
            Dim activatedBefore = Await AuditCountAsync("Person", target.PersonId, "UserActivated")

            Await _admin.SetUserActiveAsync(_administrator.PersonId, target.PersonId, False)
            Assert.AreEqual(deactivatedBefore + 1,
                            Await AuditCountAsync("Person", target.PersonId, "UserDeactivated"))

            Await _admin.SetUserActiveAsync(_administrator.PersonId, target.PersonId, True)
            Assert.AreEqual(activatedBefore + 1,
                            Await AuditCountAsync("Person", target.PersonId, "UserActivated"))
        End Function

        <TestMethod>
        Public Async Function ApprovingATutorIsAudited() As Task
            Await TestDatabase.ExecuteAsync("UPDATE dbo.Tutor SET IsApproved = 0 WHERE TutorId = @p0;",
                                            _scenario.TutorId)

            Dim before = Await AuditCountAsync("Tutor", _scenario.TutorId, "TutorApproved")
            Await _admin.ApproveTutorAsync(_administrator.PersonId, _scenario.TutorId, True)

            Assert.AreEqual(before + 1, Await AuditCountAsync("Tutor", _scenario.TutorId, "TutorApproved"))
        End Function

        <TestMethod>
        Public Async Function HidingAndRestoringAReviewIsAudited() As Task
            Dim reviewId = Await NewReviewAsync(CByte(2), "Audited moderation.")

            Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, True, "Abusive language.")
            Assert.AreEqual(1, Await AuditCountAsync("Review", reviewId, "ReviewHidden"))

            Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, False, Nothing)
            Assert.AreEqual(1, Await AuditCountAsync("Review", reviewId, "ReviewRestored"))
        End Function

        ''' <summary>Deleting is the destructive one, so it is the one that most needs a record.</summary>
        <TestMethod>
        Public Async Function CreatingAndDeletingASubjectIsAudited() As Task
            Dim name As String = "Audited " & TestDatabase.UniqueSuffix()

            Await _admin.AddSubjectAsync(_administrator.PersonId, name)
            Dim subjectId = Await TestDatabase.ScalarAsync(
                "SELECT SubjectId FROM dbo.[Subject] WHERE SubjectName = @p0;", name)

            Assert.AreEqual(1, Await AuditCountAsync("Subject", subjectId, "SubjectCreated"))

            Await _admin.DeleteSubjectAsync(_administrator.PersonId, subjectId)
            Assert.AreEqual(1, Await AuditCountAsync("Subject", subjectId, "SubjectDeleted"))
        End Function

#End Region

#Region "Creating another administrator"

        ''' <summary>
        ''' The account is created with the Administrator role, is active, has no Student or
        ''' Tutor profile row (D-05), and can actually sign in with the chosen password.
        ''' </summary>
        <TestMethod>
        Public Async Function AnAdministratorCanCreateAnotherAdministrator() As Task
            Dim suffix As String = TestDatabase.UniqueSuffix()
            Dim userName As String = "admin2_" & suffix
            Const password As String = "SecondAdmin2026!"

            Dim result = Await _admin.CreateAdministratorAsync(
                _administrator.PersonId, NewAdministrator(userName), password, password)

            Assert.IsTrue(result.Success, result.Message)

            Dim created = Await _users.FindByUserNameOrEmailAsync(userName)
            Assert.IsNotNull(created, "the account exists")
            Assert.AreEqual(UserRole.Administrator, created.Role)
            Assert.IsTrue(created.IsActive)

            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Student WHERE PersonId = @p0;", created.PersonId),
                "an administrator has no Student row")

            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Tutor WHERE PersonId = @p0;", created.PersonId),
                "an administrator has no Tutor row")

            ' The point of the whole feature: they can get in.
            Dim signIn = Await _auth.AuthenticateAsync(userName, password)
            Assert.IsTrue(signIn.Success, "the new administrator can sign in")
        End Function

        <TestMethod>
        Public Async Function CreatingAnAdministratorIsAudited() As Task
            Dim suffix As String = TestDatabase.UniqueSuffix()
            Dim userName As String = "admin3_" & suffix

            Dim result = Await _admin.CreateAdministratorAsync(
                _administrator.PersonId, NewAdministrator(userName),
                "SecondAdmin2026!", "SecondAdmin2026!")

            Assert.IsTrue(result.Success, result.Message)

            Dim created = Await _users.FindByUserNameOrEmailAsync(userName)
            Assert.AreEqual(1, Await AuditCountAsync("Person", created.PersonId, "AdministratorCreated"))
        End Function

        ''' <summary>
        ''' The role check lives in the service, not only in the Web.config location rule, so
        ''' it holds even when the service is called directly - as it is here.
        ''' </summary>
        <TestMethod>
        Public Async Function AStudentCannotCreateAnAdministrator() As Task
            Dim suffix As String = TestDatabase.UniqueSuffix()
            Dim userName As String = "sneaky_" & suffix

            Dim result = Await _admin.CreateAdministratorAsync(
                _scenario.Student.PersonId, NewAdministrator(userName),
                "SecondAdmin2026!", "SecondAdmin2026!")

            Assert.IsFalse(result.Success, "a student must not be able to make an administrator")
            Assert.IsNull(Await _users.FindByUserNameOrEmailAsync(userName), "nothing was written")
        End Function

        <TestMethod>
        Public Async Function ADeactivatedAdministratorCannotCreateAnAdministrator() As Task
            Dim suffix As String = TestDatabase.UniqueSuffix()
            Dim userName As String = "afterlock_" & suffix

            Await TestDatabase.ExecuteAsync(
                "UPDATE dbo.Person SET IsActive = 0 WHERE PersonId = @p0;", _administrator.PersonId)

            Dim result = Await _admin.CreateAdministratorAsync(
                _administrator.PersonId, NewAdministrator(userName),
                "SecondAdmin2026!", "SecondAdmin2026!")

            Assert.IsFalse(result.Success, "a deactivated administrator has no rights")
            Assert.IsNull(Await _users.FindByUserNameOrEmailAsync(userName), "nothing was written")
        End Function

        <TestMethod>
        Public Async Function ADuplicateUserNameIsRefused() As Task
            Dim result = Await _admin.CreateAdministratorAsync(
                _administrator.PersonId,
                NewAdministrator("admin"),
                "SecondAdmin2026!", "SecondAdmin2026!")

            Assert.IsFalse(result.Success, "'admin' is already taken")
        End Function

        <TestMethod>
        Public Async Function MismatchedPasswordsAreRefused() As Task
            Dim suffix As String = TestDatabase.UniqueSuffix()
            Dim userName As String = "mismatch_" & suffix

            Dim result = Await _admin.CreateAdministratorAsync(
                _administrator.PersonId, NewAdministrator(userName),
                "SecondAdmin2026!", "SomethingElse2026!")

            Assert.IsFalse(result.Success, "the confirmation did not match")
            Assert.IsNull(Await _users.FindByUserNameOrEmailAsync(userName), "nothing was written")
        End Function

        Private Shared Function NewAdministrator(userName As String) As Person
            Return New Person With {
                .UserName = userName,
                .FirstName = "Second",
                .LastName = "Administrator",
                .Gender = "Other",
                .EmailAddress = userName & "@tutorconnect.test",
                .CellNumber = "0821234567",
                .Location = "East London"
            }
        End Function

        Private Shared Function AuditCountAsync(entityType As String,
                                                entityId As Integer,
                                                action As String) As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.AuditLog " &
                "WHERE EntityType = @p0 AND EntityId = @p1 AND ActionType = @p2 " &
                "  AND ActorPersonId IS NOT NULL AND ActorRole = 'Administrator';",
                entityType, entityId, action)
        End Function

#End Region

#Region "Administrator read-only views"

        <TestMethod>
        Public Async Function Admin_SeesEveryReviewIncludingHiddenOnes() As Task
            Dim reviewId = Await NewReviewAsync(CByte(2), "Moderated later.")
            Await _admin.SetReviewHiddenAsync(_administrator.PersonId, reviewId, True, "Abusive language.")

            Dim all = (Await _admin.GetAllReviewsAsync()).Data

            Assert.IsTrue(all.Any(Function(r) r.ReviewId = reviewId),
                          "moderation is impossible if the moderator cannot see what they hid")
        End Function

        <TestMethod>
        Public Async Function Admin_SeesEveryBooking() As Task
            Await _scenario.NewScheduledBookingAsync()

            Dim all = (Await _admin.GetAllBookingsAsync()).Data

            Assert.IsTrue(all.Count > 0)
        End Function

        <TestMethod>
        Public Async Function Admin_FiltersRequestsByStatus() As Task
            Await _scenario.NewPendingRequestAsync()

            Dim pendingOnly = (Await _admin.GetAllRequestsAsync("Pending")).Data
            Dim everything = (Await _admin.GetAllRequestsAsync(Nothing)).Data

            Assert.IsTrue(pendingOnly.Count > 0)
            Assert.IsTrue(pendingOnly.All(Function(r) r.Status = RequestStatus.Pending))
            Assert.IsTrue(everything.Count >= pendingOnly.Count, "the unfiltered list cannot be smaller")
        End Function

        <TestMethod>
        Public Async Function Admin_SearchesUsersByRoleAndByName() As Task
            Dim tutors = (Await _admin.SearchUsersAsync(Nothing, UserRole.Tutor, Nothing)).Data
            Assert.IsTrue(tutors.Count > 0)
            Assert.IsTrue(tutors.All(Function(p) p.Role = UserRole.Tutor), "role filter")

            Dim byName = (Await _admin.SearchUsersAsync("Sinesipho", Nothing, Nothing)).Data
            Assert.IsTrue(byName.Any(Function(p) p.FirstName = "Sinesipho"), "name filter")
        End Function

#End Region

    End Class

End Namespace
