Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Models
Imports TutorConnect.Services

Namespace TutorConnect.Tests

    ''' <summary>
    ''' AT-14 to AT-16 and AT-23 - the tutor's side - plus BR-06, BR-07, BR-15, BR-20,
    ''' BR-21, BR-22 and the tutor half of BR-24.
    ''' </summary>
    <TestClass>
    Public Class TutorWorkflowTests

        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _availability As New AvailabilityService()
        Private ReadOnly _requests As New RequestService()
        Private ReadOnly _bookings As New BookingService()
        Private ReadOnly _tutors As New TutorService()
        Private ReadOnly _search As New TutorSearchService()

        Private _scenario As TestScenario
        Private _otherTutor As TutorProfile
        Private _otherTutorSubjectId As Integer

        <TestInitialize>
        Public Sub Setup()
            SetupAsync().GetAwaiter().GetResult()
        End Sub

        Private Async Function SetupAsync() As Task
            _scenario = Await TestScenario.CreateAsync()

            Dim person = Await _users.FindByUserNameOrEmailAsync("lerato_m")
            Assert.IsNotNull(person, "seed user lerato_m")
            _otherTutor = Await _users.GetTutorByPersonIdAsync(person.PersonId)

            _otherTutorSubjectId = Await TestDatabase.ScalarAsync(
                "SELECT TOP 1 TutorSubjectId FROM dbo.TutorSubject WHERE TutorId = @p0;", _otherTutor.TutorId)
        End Function

        ''' <summary>
        ''' Restores the acting tutor to approved and active.
        ''' </summary>
        ''' <remarks>
        ''' Several tests here withdraw approval deliberately. If one of them failed partway
        ''' the tutor would stay unapproved, and every later test would fail for a reason
        ''' that has nothing to do with what it is checking. Cleanup runs whatever the
        ''' outcome, so a real failure stays a single failure.
        ''' </remarks>
        <TestCleanup>
        Public Sub Restore()
            TestDatabase.ExecuteAsync(
                "UPDATE dbo.Tutor SET IsApproved = 1 WHERE TutorId = @p0;",
                _scenario.TutorId).GetAwaiter().GetResult()

            TestDatabase.ExecuteAsync(
                "UPDATE dbo.Person SET IsActive = 1 WHERE PersonId = @p0;",
                _scenario.TutorPerson.PersonId).GetAwaiter().GetResult()
        End Sub

#Region "BR-20 / BR-06 / BR-07 publishing availability"

        <TestMethod>
        Public Async Function BR20_ATutorsOwnSlotsMayNotOverlap() As Task
            Dim day As Date = TestScenario.NextFreeDate()

            Dim first = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId, day,
                                                     TimeSpan.Parse("10:00"), TimeSpan.Parse("12:00"))
            Assert.IsTrue(first.Success, first.Message)

            Dim overlapping = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId, day,
                                                           TimeSpan.Parse("11:00"), TimeSpan.Parse("13:00"))
            Assert.IsFalse(overlapping.Success, "11:00-13:00 overlaps 10:00-12:00")
        End Function

        ''' <summary>
        ''' The overlap test is half-open, so a slot beginning exactly when another ends is
        ''' not a clash. Treating it as one would stop a tutor filling a day back to back.
        ''' </summary>
        <TestMethod>
        Public Async Function BR20_AnAdjacentSlotIsNotAnOverlap() As Task
            Dim day As Date = TestScenario.NextFreeDate()

            Dim first = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId, day,
                                                     TimeSpan.Parse("10:00"), TimeSpan.Parse("12:00"))
            Assert.IsTrue(first.Success, first.Message)

            Dim adjacent = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId, day,
                                                        TimeSpan.Parse("12:00"), TimeSpan.Parse("13:00"))
            Assert.IsTrue(adjacent.Success, adjacent.Message)
        End Function

        <TestMethod>
        Public Async Function BR06_AnEndTimeBeforeTheStartIsRejected() As Task
            Dim result = Await _availability.AddAsync(
                _scenario.TutorId, _scenario.TutorSubjectId, TestScenario.NextFreeDate(),
                TimeSpan.Parse("15:00"), TimeSpan.Parse("14:00"))

            Assert.IsFalse(result.Success, result.Message)
        End Function

        <TestMethod>
        Public Async Function BR07_ASlotInThePastCannotBePublished() As Task
            Dim result = Await _availability.AddAsync(
                _scenario.TutorId, _scenario.TutorSubjectId, Date.Today.AddDays(-2),
                TimeSpan.Parse("10:00"), TimeSpan.Parse("11:00"))

            Assert.IsFalse(result.Success, result.Message)
        End Function

        <TestMethod>
        Public Async Function BR24_ATutorCannotPublishAgainstAnotherTutorsSubject() As Task
            Dim result = Await _availability.AddAsync(
                _scenario.TutorId, _otherTutorSubjectId, TestScenario.NextFreeDate(),
                TimeSpan.Parse("16:00"), TimeSpan.Parse("17:00"))

            Assert.IsFalse(result.Success, result.Message)
            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.TutorAvailability WHERE TutorSubjectId = @p0 AND AvailabilityDate > @p1;",
                _otherTutorSubjectId, Date.Today.AddDays(29)))
        End Function

#End Region

#Region "AT-14 accepting"

        <TestMethod>
        Public Async Function AT14_AcceptingAPendingRequestCreatesTheBookingAndClosesTheSlot() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Assert.AreEqual("Confirmed", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.SessionRequest WHERE RequestId = @p0;", fixture.RequestId))

            Assert.IsTrue(fixture.BookingId > 0, "a Booking row is written")
            Assert.AreEqual("Scheduled", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.Booking WHERE BookingId = @p0;", fixture.BookingId))

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT CAST(IsBooked AS INT) FROM dbo.TutorAvailability WHERE AvailabilityId = @p0;",
                fixture.AvailabilityId), "the slot is marked booked")
        End Function

        <TestMethod>
        Public Async Function AT14_TheStudentIsNotifiedOfTheAcceptance() As Task
            Dim before = Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification WHERE PersonId = @p0 AND NotificationType = 'RequestAccepted';",
                _scenario.Student.PersonId)

            Await _scenario.NewScheduledBookingAsync()

            Assert.AreEqual(before + 1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification WHERE PersonId = @p0 AND NotificationType = 'RequestAccepted';",
                _scenario.Student.PersonId))
        End Function

        <TestMethod>
        Public Async Function AT14_AcceptingTheSameRequestTwiceIsRefused() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim again = Await _bookings.AcceptRequestAsync(_scenario.TutorId, fixture.RequestId)

            Assert.IsFalse(again.Success, again.Message)
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking WHERE RequestId = @p0;", fixture.RequestId),
                "no second booking may appear")
        End Function

#End Region

#Region "AT-15 the race"

        ''' <summary>
        ''' Two students hold pending requests for one slot and the tutor accepts both at the
        ''' same instant. Exactly one may win.
        ''' </summary>
        ''' <remarks>
        ''' This is the check that a SERIALIZABLE transaction with UPDLOCK, HOLDLOCK exists
        ''' for. A read-then-write without it passes every sequential test and still
        ''' double-books here, so the two accepts are genuinely started before either is
        ''' awaited rather than awaited in turn.
        ''' </remarks>
        <TestMethod>
        Public Async Function AT15_TwoConcurrentAcceptsOfOneSlotProduceExactlyOneBooking() As Task
            Dim day As Date = TestScenario.NextFreeDate()
            Dim slot = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId, day,
                                                    TimeSpan.Parse("09:00"), TimeSpan.Parse("11:00"))
            Assert.IsTrue(slot.Success, slot.Message)

            Dim first = Await _requests.CreateAsync(_scenario.StudentId, _scenario.TutorSubjectId, slot.Data,
                                                    day, TimeSpan.Parse("09:00"), TimeSpan.Parse("11:00"), Nothing)
            Dim second = Await _requests.CreateAsync(_scenario.OtherStudentId, _scenario.TutorSubjectId, slot.Data,
                                                     day, TimeSpan.Parse("09:00"), TimeSpan.Parse("11:00"), Nothing)
            Assert.IsTrue(first.Success, first.Message)
            Assert.IsTrue(second.Success, second.Message)

            Dim acceptFirst = _bookings.AcceptRequestAsync(_scenario.TutorId, first.Data)
            Dim acceptSecond = _bookings.AcceptRequestAsync(_scenario.TutorId, second.Data)
            Await Task.WhenAll(acceptFirst, acceptSecond)

            Dim winners As Integer = 0
            If acceptFirst.Result.Success Then winners += 1
            If acceptSecond.Result.Success Then winners += 1

            Assert.AreEqual(1, winners,
                $"exactly one accept may succeed - [{acceptFirst.Result.Message}] [{acceptSecond.Result.Message}]")

            Dim loser As String = If(acceptFirst.Result.Success, acceptSecond.Result.Message, acceptFirst.Result.Message)
            Assert.IsFalse(String.IsNullOrWhiteSpace(loser), "the losing accept must explain itself")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking WHERE AvailabilityId = @p0 AND Status = 'Scheduled';", slot.Data),
                "the slot must not be double-booked")
        End Function

#End Region

#Region "AT-16 declining"

        <TestMethod>
        Public Async Function AT16_DecliningLeavesTheRequestDeclinedAndCreatesNoBooking() As Task
            Dim fixture = Await _scenario.NewPendingRequestAsync()

            Dim declined = Await _bookings.DeclineRequestAsync(
                _scenario.TutorId, fixture.RequestId, "I am not available after all.")
            Assert.IsTrue(declined.Success, declined.Message)

            Assert.AreEqual("Declined", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.SessionRequest WHERE RequestId = @p0;", fixture.RequestId))

            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking WHERE RequestId = @p0;", fixture.RequestId))

            Assert.AreEqual("I am not available after all.", Await TestDatabase.ScalarStringAsync(
                "SELECT ISNULL(DeclineReason, '') FROM dbo.SessionRequest WHERE RequestId = @p0;", fixture.RequestId),
                "the reason the tutor typed must reach the student's record")
        End Function

        <TestMethod>
        Public Async Function AT16_DecliningTwiceIsRefused() As Task
            Dim fixture = Await _scenario.NewPendingRequestAsync()

            Await _bookings.DeclineRequestAsync(_scenario.TutorId, fixture.RequestId, Nothing)
            Dim again = Await _bookings.DeclineRequestAsync(_scenario.TutorId, fixture.RequestId, Nothing)

            Assert.IsFalse(again.Success, again.Message)
        End Function

        <TestMethod>
        Public Async Function BR24_AnotherTutorCannotAnswerThisTutorsRequest() As Task
            Dim fixture = Await _scenario.NewPendingRequestAsync()

            Dim declined = Await _bookings.DeclineRequestAsync(_otherTutor.TutorId, fixture.RequestId, Nothing)
            Dim accepted = Await _bookings.AcceptRequestAsync(_otherTutor.TutorId, fixture.RequestId)

            Assert.IsFalse(declined.Success, "declining someone else's request")
            Assert.IsFalse(accepted.Success, "accepting someone else's request")

            Assert.AreEqual("Pending", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.SessionRequest WHERE RequestId = @p0;", fixture.RequestId),
                "the request must be untouched")
        End Function

#End Region

#Region "BR-15 availability changed after the request"

        ''' <summary>
        ''' The window is re-checked at the moment of acceptance, not only when the request
        ''' was made. A tutor who narrows a slot afterwards must not end up booked outside it.
        ''' </summary>
        <TestMethod>
        Public Async Function BR15_AcceptingIsRefusedOnceTheSlotNoLongerContainsTheWindow() As Task
            Dim day As Date = TestScenario.NextFreeDate()
            Dim slot = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId, day,
                                                    TimeSpan.Parse("09:00"), TimeSpan.Parse("12:00"))
            Assert.IsTrue(slot.Success, slot.Message)

            Dim request = Await _requests.CreateAsync(_scenario.StudentId, _scenario.TutorSubjectId, slot.Data,
                                                      day, TimeSpan.Parse("09:00"), TimeSpan.Parse("11:00"), Nothing)
            Assert.IsTrue(request.Success, request.Message)

            Await TestDatabase.ExecuteAsync(
                "UPDATE dbo.TutorAvailability SET StartTime = @p0 WHERE AvailabilityId = @p1;",
                TimeSpan.Parse("10:30"), slot.Data)

            Dim accepted = Await _bookings.AcceptRequestAsync(_scenario.TutorId, request.Data)

            Assert.IsFalse(accepted.Success, accepted.Message)
        End Function

#End Region

#Region "BR-22 deleting availability"

        <TestMethod>
        Public Async Function BR22_ASlotBehindAScheduledBookingCannotBeDeleted() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _availability.DeleteAsync(_scenario.TutorId, fixture.AvailabilityId)

            Assert.IsFalse(result.Success, "deleting it would strand a booked student")
            StringAssert.Contains(result.Message.ToLowerInvariant(), "scheduled",
                                  "the tutor must be told why")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.TutorAvailability WHERE AvailabilityId = @p0;", fixture.AvailabilityId))
        End Function

        <TestMethod>
        Public Async Function BR22_AnUnbookedSlotDeletesNormally() As Task
            Dim slot = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId,
                                                    TestScenario.NextFreeDate(),
                                                    TimeSpan.Parse("08:00"), TimeSpan.Parse("09:00"))
            Assert.IsTrue(slot.Success, slot.Message)

            Dim deleted = Await _availability.DeleteAsync(_scenario.TutorId, slot.Data)

            Assert.IsTrue(deleted.Success, deleted.Message)
            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.TutorAvailability WHERE AvailabilityId = @p0;", slot.Data))
        End Function

        <TestMethod>
        Public Async Function BR24_AnotherTutorCannotDeleteThisTutorsSlot() As Task
            Dim slot = Await _availability.AddAsync(_scenario.TutorId, _scenario.TutorSubjectId,
                                                    TestScenario.NextFreeDate(),
                                                    TimeSpan.Parse("08:00"), TimeSpan.Parse("09:00"))
            Assert.IsTrue(slot.Success, slot.Message)

            Dim result = Await _availability.DeleteAsync(_otherTutor.TutorId, slot.Data)

            Assert.IsFalse(result.Success, result.Message)
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.TutorAvailability WHERE AvailabilityId = @p0;", slot.Data),
                "the slot must survive")
        End Function

#End Region

#Region "BR-21 removing a subject"

        <TestMethod>
        Public Async Function BR21_ASubjectWithCommitmentsCannotBeRemoved() As Task
            ' Give the subject a commitment that certainly exists at the moment of the check.
            Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _tutors.RemoveSubjectAsync(_scenario.TutorId, _scenario.TutorSubjectId)

            Assert.IsFalse(result.Success, result.Message)
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.TutorSubject WHERE TutorSubjectId = @p0;", _scenario.TutorSubjectId),
                "the subject must still exist")
        End Function

#End Region

#Region "AT-23 approval"

        <TestMethod>
        Public Async Function AT23_ApprovalControlsVisibilityInSearchImmediately() As Task
            Await TestDatabase.ExecuteAsync("UPDATE dbo.Tutor SET IsApproved = 0 WHERE TutorId = @p0;",
                                            _scenario.TutorId)

            Dim before = (Await _search.SearchAsync(Nothing, Nothing, Nothing, Nothing, 1, 200)).Data
            Assert.IsFalse(before.Any(Function(r) r.TutorId = _scenario.TutorId), "absent while unapproved")

            Dim approved = Await _tutors.SetApprovedAsync(_scenario.TutorId, True)
            Assert.IsTrue(approved.Success, approved.Message)

            Dim after = (Await _search.SearchAsync(Nothing, Nothing, Nothing, Nothing, 1, 200)).Data
            Assert.IsTrue(after.Any(Function(r) r.TutorId = _scenario.TutorId),
                          "present as soon as approval is granted, with no further step")
        End Function

        <TestMethod>
        Public Async Function AT23_TheTutorIsNotifiedOfApproval() As Task
            Await TestDatabase.ExecuteAsync("UPDATE dbo.Tutor SET IsApproved = 0 WHERE TutorId = @p0;",
                                            _scenario.TutorId)

            Dim before = Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification " &
                "WHERE PersonId = @p0 AND NotificationType = 'TutorApproved';",
                _scenario.TutorPerson.PersonId)

            Await _tutors.SetApprovedAsync(_scenario.TutorId, True)

            Assert.AreEqual(before + 1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification " &
                "WHERE PersonId = @p0 AND NotificationType = 'TutorApproved';",
                _scenario.TutorPerson.PersonId))
        End Function

#End Region

    End Class

End Namespace
