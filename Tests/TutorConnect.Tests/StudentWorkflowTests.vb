Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Services

Namespace TutorConnect.Tests

    ''' <summary>
    ''' AT-08 to AT-22 - the student's path from search to review - plus BR-05 and BR-27.
    ''' </summary>
    <TestClass>
    Public Class StudentWorkflowTests

        Private ReadOnly _search As New TutorSearchService()
        Private ReadOnly _requests As New RequestService()
        Private ReadOnly _bookings As New BookingService()
        Private ReadOnly _reviews As New ReviewService()
        Private ReadOnly _students As New StudentService()
        Private ReadOnly _reviewRepository As New ReviewRepository()

        Private _scenario As TestScenario

        <TestInitialize>
        Public Sub Setup()
            _scenario = TestScenario.CreateAsync().GetAwaiter().GetResult()
        End Sub

#Region "AT-08 search"

        <TestMethod>
        Public Async Function AT08_SearchBySubjectAndLevelReturnsOnlyMatchingTutors() As Task
            Dim subjectId = Await SubjectIdAsync("Information Systems")
            Dim levelId = Await LevelIdAsync("Third Year")

            Dim results = (Await _search.SearchAsync(subjectId, levelId, Nothing, Nothing, 1, 50)).Data

            Assert.IsTrue(results.Count > 0, "the seed data must produce at least one match")
            Assert.IsTrue(results.All(Function(r) r.SubjectName = "Information Systems"), "subject filter")
            Assert.IsTrue(results.All(Function(r) r.StudyLevelName = "Third Year"), "level filter")
        End Function

        ''' <summary>
        ''' BR-26. Approval is checked in the search SQL itself, not by filtering afterwards,
        ''' so there is no window in which an unapproved tutor is listed.
        ''' </summary>
        <TestMethod>
        Public Async Function AT08_AnUnapprovedTutorIsExcluded() As Task
            Dim subjectId = Await SubjectIdAsync("Information Systems")
            Dim levelId = Await LevelIdAsync("Third Year")

            Await TestDatabase.ExecuteAsync("UPDATE dbo.Tutor SET IsApproved = 0 WHERE TutorId = @p0;", _scenario.TutorId)
            Try
                Dim results = (Await _search.SearchAsync(subjectId, levelId, Nothing, Nothing, 1, 50)).Data
                Assert.IsFalse(results.Any(Function(r) r.TutorId = _scenario.TutorId))
            Finally
                ' VB forbids Await inside Finally, so the restore blocks. It is a single
                ' UPDATE with no synchronisation context to deadlock against.
                TestDatabase.ExecuteAsync(
                    "UPDATE dbo.Tutor SET IsApproved = 1 WHERE TutorId = @p0;",
                    _scenario.TutorId).GetAwaiter().GetResult()
            End Try
        End Function

        <TestMethod>
        Public Async Function AT08_ADeactivatedTutorIsExcluded() As Task
            Dim subjectId = Await SubjectIdAsync("Information Systems")
            Dim levelId = Await LevelIdAsync("Third Year")

            Await TestDatabase.ExecuteAsync("UPDATE dbo.Person SET IsActive = 0 WHERE PersonId = @p0;",
                                            _scenario.TutorPerson.PersonId)
            Try
                Dim results = (Await _search.SearchAsync(subjectId, levelId, Nothing, Nothing, 1, 50)).Data
                Assert.IsFalse(results.Any(Function(r) r.TutorId = _scenario.TutorId))
            Finally
                TestDatabase.ExecuteAsync(
                    "UPDATE dbo.Person SET IsActive = 1 WHERE PersonId = @p0;",
                    _scenario.TutorPerson.PersonId).GetAwaiter().GetResult()
            End Try
        End Function

#End Region

#Region "AT-09 requesting a session"

        <TestMethod>
        Public Async Function AT09_AValidFutureSlotCanBeRequested() As Task
            Dim slots = (Await _search.GetOpenSlotsAsync(_scenario.TutorSubjectId)).Data
            Assert.IsTrue(slots.Count > 0, "the seeded tutor must publish open slots")

            Dim fixture = Await _scenario.NewPendingRequestAsync()

            Dim mine = (Await _requests.GetForStudentAsync(_scenario.StudentId)).Data
            Dim created = mine.FirstOrDefault(Function(r) r.RequestId = fixture.RequestId)

            Assert.IsNotNull(created, "the request must appear in My Requests")
            Assert.AreEqual(RequestStatus.Pending, created.Status)
        End Function

#End Region

#Region "AT-10 times"

        <TestMethod>
        Public Async Function AT10_AnEndTimeAtOrBeforeTheStartIsRejectedServerSide() As Task
            Dim slotDate As Date = TestScenario.NextFreeDate()
            Dim availabilityId = Await TestDatabase.CreateAvailabilityDirectAsync(
                _scenario.TutorSubjectId, slotDate, "09:00", "11:00")

            Dim backwards = Await _requests.CreateAsync(
                _scenario.StudentId, _scenario.TutorSubjectId, availabilityId, slotDate,
                TimeSpan.Parse("11:00"), TimeSpan.Parse("09:00"), Nothing)

            Assert.IsFalse(backwards.Success, "an end time before the start must be refused")
            StringAssert.Contains(backwards.Message.ToLowerInvariant(), "end time",
                                  "the message must name the field at fault")

            Dim equal = Await _requests.CreateAsync(
                _scenario.StudentId, _scenario.TutorSubjectId, availabilityId, slotDate,
                TimeSpan.Parse("09:00"), TimeSpan.Parse("09:00"), Nothing)

            Assert.IsFalse(equal.Success, "a zero-length session must be refused too")
        End Function

#End Region

#Region "AT-11 outside availability"

        <TestMethod>
        Public Async Function AT11_ARequestOutsideTheTutorsSlotIsRejected() As Task
            Dim slotDate As Date = TestScenario.NextFreeDate()
            Dim availabilityId = Await TestDatabase.CreateAvailabilityDirectAsync(
                _scenario.TutorSubjectId, slotDate, "09:00", "11:00")

            Dim tooEarly = Await _requests.CreateAsync(
                _scenario.StudentId, _scenario.TutorSubjectId, availabilityId, slotDate,
                TimeSpan.Parse("08:00"), TimeSpan.Parse("11:00"), Nothing)
            Assert.IsFalse(tooEarly.Success, "starting before the slot: " & tooEarly.Message)

            Dim overruns = Await _requests.CreateAsync(
                _scenario.StudentId, _scenario.TutorSubjectId, availabilityId, slotDate,
                TimeSpan.Parse("09:00"), TimeSpan.Parse("12:00"), Nothing)
            Assert.IsFalse(overruns.Success, "ending after the slot: " & overruns.Message)

            Dim wrongDay = Await _requests.CreateAsync(
                _scenario.StudentId, _scenario.TutorSubjectId, availabilityId, slotDate.AddDays(1),
                TimeSpan.Parse("09:00"), TimeSpan.Parse("11:00"), Nothing)
            Assert.IsFalse(wrongDay.Success, "a different date: " & wrongDay.Message)
        End Function

        ''' <summary>
        ''' BR-11. The slot is inserted directly, because the availability service refuses to
        ''' publish a past slot (BR-07) - the rule still has to hold for a row that reached
        ''' the table some other way.
        ''' </summary>
        <TestMethod>
        Public Async Function BR11_ASlotInThePastCannotBeRequested() As Task
            Dim pastDate As Date = Date.Today.AddDays(-3)
            Dim availabilityId = Await TestDatabase.CreateAvailabilityDirectAsync(
                _scenario.TutorSubjectId, pastDate, "10:00", "12:00")

            Dim result = Await _requests.CreateAsync(
                _scenario.StudentId, _scenario.TutorSubjectId, availabilityId, pastDate,
                TimeSpan.Parse("10:00"), TimeSpan.Parse("12:00"), Nothing)

            Assert.IsFalse(result.Success, result.Message)
        End Function

#End Region

#Region "AT-12 student overlap (BR-12)"

        <TestMethod>
        Public Async Function AT12_AStudentCannotHoldTwoOverlappingBookings() As Task
            Dim booked = Await _scenario.NewScheduledBookingAsync()

            ' A second slot on the same date, shifted by half an hour so the windows overlap.
            Dim overlappingId = Await TestDatabase.CreateAvailabilityDirectAsync(
                _scenario.TutorSubjectId, booked.SlotDate, "09:30", "11:30")

            Dim clash = Await _requests.CreateAsync(
                _scenario.StudentId, _scenario.TutorSubjectId, overlappingId, booked.SlotDate,
                TimeSpan.Parse("09:30"), TimeSpan.Parse("11:30"), Nothing)

            Assert.IsFalse(clash.Success, "the overlapping request must be refused")
            StringAssert.Contains(clash.Message.ToLowerInvariant(), "overlap",
                                  "the message must explain the clash")
        End Function

        ''' <summary>The rule is per student, so it must not block everyone else's day.</summary>
        <TestMethod>
        Public Async Function BR12_ADifferentStudentIsNotBlockedByThatOverlap() As Task
            Dim booked = Await _scenario.NewScheduledBookingAsync()

            Dim overlappingId = Await TestDatabase.CreateAvailabilityDirectAsync(
                _scenario.TutorSubjectId, booked.SlotDate, "09:30", "11:30")

            Dim otherStudent = Await _requests.CreateAsync(
                _scenario.OtherStudentId, _scenario.TutorSubjectId, overlappingId, booked.SlotDate,
                TimeSpan.Parse("09:30"), TimeSpan.Parse("11:30"), Nothing)

            Assert.IsTrue(otherStudent.Success, otherStudent.Message)
        End Function

#End Region

#Region "AT-13 cancelling (BR-13)"

        <TestMethod>
        Public Async Function AT13_APendingRequestCanBeCancelled() As Task
            Dim fixture = Await _scenario.NewPendingRequestAsync()

            Dim result = Await _requests.CancelAsync(_scenario.StudentId, fixture.RequestId)

            Assert.IsTrue(result.Success, result.Message)
        End Function

        <TestMethod>
        Public Async Function BR13_AConfirmedRequestCannotBeCancelled() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _requests.CancelAsync(_scenario.StudentId, fixture.RequestId)

            Assert.IsFalse(result.Success, "once accepted, cancelling is the tutor's decision")
        End Function

        <TestMethod>
        Public Async Function BR24_AStudentCannotCancelAnotherStudentsRequest() As Task
            Dim fixture = Await _scenario.NewPendingRequestAsync()

            Dim result = Await _requests.CancelAsync(_scenario.OtherStudentId, fixture.RequestId)

            Assert.IsFalse(result.Success)

            Dim stillPending = Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.SessionRequest WHERE RequestId = @p0;", fixture.RequestId)
            Assert.AreEqual("Pending", stillPending, "the request must be untouched")
        End Function

#End Region

#Region "AT-17 / AT-18 completion (BR-23)"

        <TestMethod>
        Public Async Function AT17_ASessionCannotBeCompletedBeforeItsEndTime() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _bookings.MarkCompletedAsync(_scenario.TutorId, fixture.BookingId)

            Assert.IsFalse(result.Success, "BR-23: the session is still in the future")
        End Function

        <TestMethod>
        Public Async Function AT18_CompletingAFinishedSessionCreatesTheSessionRowAndOpensReviewing() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim sessionRows = Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.TutoringSession WHERE BookingId = @p0;", fixture.BookingId)
            Assert.AreEqual(1, sessionRows, "exactly one TutoringSession row")

            Assert.IsTrue(Await _reviews.CanReviewAsync(_scenario.StudentId, fixture.BookingId),
                          "the student may now review it")
        End Function

        <TestMethod>
        Public Async Function AT18_AnotherTutorCannotCompleteTheSession() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Await TestDatabase.ExecuteAsync(
                "UPDATE dbo.Booking SET BookingDate = @p0 WHERE BookingId = @p1;",
                Date.Today.AddDays(-1), fixture.BookingId)

            Dim result = Await _bookings.MarkCompletedAsync(_scenario.TutorId + 9999, fixture.BookingId)

            Assert.IsFalse(result.Success)
            Assert.AreEqual("Scheduled", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.Booking WHERE BookingId = @p0;", fixture.BookingId),
                "the booking must be untouched")
        End Function

#End Region

#Region "AT-19 reviewing"

        ''' <summary>
        ''' The rating is asserted against a tutor created for this test alone. Sharing the
        ''' seeded tutor would make the expected average depend on whatever other tests had
        ''' already written, which is exactly the coupling these tests exist to avoid.
        ''' </summary>
        <TestMethod>
        Public Async Function AT19_AReviewIsSavedAndMovesTheTutorsAverageRating() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim before = Await _reviewRepository.GetTutorRatingAsync(_scenario.TutorId)

            Dim result = Await _reviews.CreateAsync(_scenario.StudentId, fixture.BookingId, CByte(5),
                                                    "Excellent session, very clear.")
            Assert.IsTrue(result.Success, result.Message)

            Dim after = Await _reviewRepository.GetTutorRatingAsync(_scenario.TutorId)

            Assert.AreEqual(before.ReviewCount + 1, after.ReviewCount, "the review count must rise by one")
            Assert.IsTrue(after.AverageRating.HasValue, "an average must now exist")
            Assert.IsTrue(after.AverageRating.Value >= If(before.AverageRating, 0D),
                          "a five-star review cannot lower the average")
            Assert.IsTrue(after.AverageRating.Value <= 5D, "the average cannot exceed the scale")
        End Function

        <TestMethod>
        Public Async Function AT19_ARatingOutsideOneToFiveIsRejected() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim result = Await _reviews.CreateAsync(_scenario.StudentId, fixture.BookingId, CByte(9), "out of range")

            Assert.IsFalse(result.Success, result.Message)
            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Review WHERE BookingId = @p0;", fixture.BookingId))
        End Function

#End Region

#Region "AT-21 one review per booking (BR-17)"

        <TestMethod>
        Public Async Function AT21_ASecondReviewForTheSameBookingIsRejected() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim first = Await _reviews.CreateAsync(_scenario.StudentId, fixture.BookingId, CByte(4), "good")
            Assert.IsTrue(first.Success, first.Message)

            Dim second = Await _reviews.CreateAsync(_scenario.StudentId, fixture.BookingId, CByte(3), "changed my mind")
            Assert.IsFalse(second.Success, second.Message)

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Review WHERE BookingId = @p0;", fixture.BookingId),
                "still exactly one review row")
        End Function

#End Region

#Region "AT-20 reviewing a scheduled booking (BR-16)"

        ''' <summary>
        ''' The page hides the review form for a Scheduled booking; this posts to the service
        ''' anyway, which is what a tampered form would do.
        ''' </summary>
        <TestMethod>
        Public Async Function AT20_AScheduledBookingCannotBeReviewedEvenByTampering() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Assert.IsFalse(Await _reviews.CanReviewAsync(_scenario.StudentId, fixture.BookingId),
                           "CanReview must say no")

            Dim tampered = Await _reviews.CreateAsync(_scenario.StudentId, fixture.BookingId, CByte(5), "too early")

            Assert.IsFalse(tampered.Success, tampered.Message)
            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Review WHERE BookingId = @p0;", fixture.BookingId),
                "no review row may be written")
        End Function

        <TestMethod>
        Public Async Function AT20_AnotherStudentCannotReviewACompletedBooking() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim result = Await _reviews.CreateAsync(_scenario.OtherStudentId, fixture.BookingId, CByte(1), "not mine")

            Assert.IsFalse(result.Success)
            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Review WHERE BookingId = @p0;", fixture.BookingId))
        End Function

#End Region

#Region "BR-05 duplicate subject"

        <TestMethod>
        Public Async Function BR05_AStudentCannotAddTheSameSubjectAndLevelTwice() As Task
            Dim subjectId = Await SubjectIdAsync("Information Systems")
            Dim levelId = Await LevelIdAsync("Third Year")

            ' The first call may legitimately fail as already-present, depending on the seed;
            ' what matters is that after it the pair exists exactly once and a further add
            ' is refused.
            Await _students.AddSubjectAsync(_scenario.StudentId, subjectId, levelId)

            Dim again = Await _students.AddSubjectAsync(_scenario.StudentId, subjectId, levelId)
            Assert.IsFalse(again.Success, "a duplicate must be refused")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.StudentSubject " &
                "WHERE StudentId = @p0 AND SubjectId = @p1 AND StudyLevelId = @p2;",
                _scenario.StudentId, subjectId, levelId))
        End Function

#End Region

#Region "AT-22 record-level authorisation (BR-24)"

        <TestMethod>
        Public Async Function AT22_OnlyTheOwnerAndTheTutorCanOpenABooking() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim owner = Await _bookings.GetForViewerAsync(fixture.BookingId, _scenario.Student.PersonId)
            Assert.IsTrue(owner.Success, "the student who booked it: " & owner.Message)

            Dim tutor = Await _bookings.GetForViewerAsync(fixture.BookingId, _scenario.TutorPerson.PersonId)
            Assert.IsTrue(tutor.Success, "the session's tutor: " & tutor.Message)

            Dim stranger = Await _bookings.GetForViewerAsync(fixture.BookingId, _scenario.OtherStudent.PersonId)
            Assert.IsFalse(stranger.Success, "another student must be refused")
        End Function

#End Region

#Region "BR-27 notifications"

        <TestMethod>
        Public Async Function BR27_AcceptingAndCompletingNotifiesBothParties() As Task
            Dim tutorBefore = Await NotificationCountAsync(_scenario.TutorPerson.PersonId)
            Dim studentBefore = Await NotificationCountAsync(_scenario.Student.PersonId)

            Await _scenario.NewCompletedBookingAsync()

            Assert.IsTrue(Await NotificationCountAsync(_scenario.TutorPerson.PersonId) > tutorBefore,
                          "the tutor is told a request arrived")
            Assert.IsTrue(Await NotificationCountAsync(_scenario.Student.PersonId) > studentBefore,
                          "the student is told it was accepted and completed")
        End Function

#End Region

#Region "Helpers"

        Private Shared Function SubjectIdAsync(subjectName As String) As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT SubjectId FROM dbo.[Subject] WHERE SubjectName = @p0;", subjectName)
        End Function

        Private Shared Function LevelIdAsync(levelName As String) As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT StudyLevelId FROM dbo.StudyLevel WHERE StudyLevelName = @p0;", levelName)
        End Function

        Private Shared Function NotificationCountAsync(personId As Integer) As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification WHERE PersonId = @p0;", personId)
        End Function

#End Region

    End Class

End Namespace
