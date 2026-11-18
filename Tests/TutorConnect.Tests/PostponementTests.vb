Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Services

Namespace TutorConnect.Tests

    ''' <summary>
    ''' Part 12.4 and 12.5 - a student proposes a new time, the tutor answers.
    ''' </summary>
    ''' <remarks>
    ''' The rule these tests exist to protect: <b>a proposal never changes the booking</b>.
    ''' Only the tutor's approval does. A student who proposes a new time and hears nothing
    ''' still has the session they agreed to.
    ''' </remarks>
    <TestClass>
    Public Class PostponementTests

        Private ReadOnly _postponements As New PostponementService()
        Private ReadOnly _bookings As New BookingService()
        Private ReadOnly _repository As New PostponementRepository()
        Private ReadOnly _audit As New AuditRepository()
        Private _scenario As TestScenario

        <TestInitialize>
        Public Sub Setup()
            _scenario = TestScenario.CreateAsync().GetAwaiter().GetResult()
        End Sub

        Private Shared ReadOnly NineOClock As TimeSpan = TimeSpan.Parse("09:00")
        Private Shared ReadOnly ElevenOClock As TimeSpan = TimeSpan.Parse("11:00")

        ''' <summary>
        ''' The acting student proposes 09:00-11:00 on the given date.
        ''' </summary>
        ''' <remarks>
        ''' By value, not ByRef: VB forbids a ByRef parameter on an async method, and the
        ''' caller already holds the date it passed in.
        ''' </remarks>
        Private Async Function ProposeAsync(fixture As RequestFixture,
                                            newDate As Date) As Task(Of ServiceResult(Of Integer))
            Return Await _postponements.ProposeAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                newDate, NineOClock, ElevenOClock,
                ScheduleChangeReason.TimetableClash, "Lecture moved.")
        End Function

#Region "Proposing"

        <TestMethod>
        Public Async Function AProposalIsRecordedAndLeavesTheBookingAlone() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()

            Dim result = Await ProposeAsync(fixture, newDate)
            Assert.IsTrue(result.Success, result.Message)

            ' The booking is untouched. This is the whole point of the separate table.
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking " &
                "WHERE BookingId = @p0 AND BookingDate = @p1 AND StartTime = @p2 AND Status = 'Scheduled';",
                fixture.BookingId, fixture.SlotDate, fixture.StartTime),
                "the agreed session must not move until the tutor agrees")

            ' The original values are copied from the booking, not supplied by the caller.
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.PostponementRequest " &
                "WHERE PostponementId = @p0 AND OriginalDate = @p1 AND ProposedDate = @p2 " &
                "  AND Status = 'PendingTutorReview' AND RespondedAt IS NULL;",
                result.Data, fixture.SlotDate, newDate))
        End Function

        <TestMethod>
        Public Async Function TheTutorIsNotifiedOfAProposal() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()

            Dim before = Await NoticesAsync(_scenario.TutorPerson.PersonId, "BookingPostponeRequested")
            Await ProposeAsync(fixture, newDate)

            Assert.AreEqual(before + 1,
                Await NoticesAsync(_scenario.TutorPerson.PersonId, "BookingPostponeRequested"))
        End Function

        <TestMethod>
        Public Async Function AnEndTimeAtOrBeforeTheStartIsRefused() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _postponements.ProposeAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                TestScenario.NextFreeDate(), ElevenOClock, NineOClock,
                ScheduleChangeReason.Other, Nothing)

            Assert.IsFalse(result.Success, result.Message)
        End Function

        <TestMethod>
        Public Async Function ADateInThePastIsRefused() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _postponements.ProposeAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                Date.Today.AddDays(-1), NineOClock, ElevenOClock,
                ScheduleChangeReason.Other, Nothing)

            Assert.IsFalse(result.Success, result.Message)
        End Function

        <TestMethod>
        Public Async Function AnotherStudentCannotProposeAgainstThisBooking() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _postponements.ProposeAsync(
                _scenario.OtherStudent.PersonId, fixture.BookingId,
                TestScenario.NextFreeDate(), NineOClock, ElevenOClock,
                ScheduleChangeReason.Other, "not mine")

            Assert.IsFalse(result.Success)
            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.PostponementRequest WHERE BookingId = @p0;", fixture.BookingId))
        End Function

        ''' <summary>
        ''' Enforced by the filtered unique index, not by a check in the service - two
        ''' submissions racing each other would both pass a SELECT-then-INSERT.
        ''' </summary>
        <TestMethod>
        Public Async Function ASecondUnresolvedProposalIsRefused() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim firstDate As Date = TestScenario.NextFreeDate()
            Dim secondDate As Date = TestScenario.NextFreeDate()

            Dim first = Await ProposeAsync(fixture, firstDate)
            Assert.IsTrue(first.Success, first.Message)

            Dim second = Await _postponements.ProposeAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                secondDate, NineOClock, ElevenOClock,
                ScheduleChangeReason.Other, "changed my mind")

            Assert.IsFalse(second.Success, "one open proposal per booking")
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.PostponementRequest " &
                "WHERE BookingId = @p0 AND Status = 'PendingTutorReview';", fixture.BookingId))
        End Function

#End Region

#Region "Approving"

        <TestMethod>
        Public Async Function ApprovingMovesTheBookingAndKeepsTheHistory() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim result = Await _postponements.ApproveAsync(
                _scenario.TutorPerson.PersonId, proposal.Data, "That suits me.")

            Assert.IsTrue(result.Success, result.Message)

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking " &
                "WHERE BookingId = @p0 AND BookingDate = @p1 AND StartTime = @p2 AND Status = 'Scheduled';",
                fixture.BookingId, newDate, NineOClock),
                "the booking must now sit on the approved date")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.PostponementRequest " &
                "WHERE PostponementId = @p0 AND Status = 'Approved' AND RespondedAt IS NOT NULL " &
                "  AND OriginalDate = @p1;",
                proposal.Data, fixture.SlotDate),
                "what the session used to be must survive the move")
        End Function

        <TestMethod>
        Public Async Function ApprovingNotifiesTheStudentAndIsAudited() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim noticesBefore = Await NoticesAsync(_scenario.Student.PersonId, "BookingPostponeApproved")
            Dim auditBefore = Await _audit.CountForEntityAsync(AuditService.BookingEntity, fixture.BookingId)

            Await _postponements.ApproveAsync(_scenario.TutorPerson.PersonId, proposal.Data, Nothing)

            Assert.AreEqual(noticesBefore + 1,
                Await NoticesAsync(_scenario.Student.PersonId, "BookingPostponeApproved"))
            Assert.AreEqual(auditBefore + 1,
                Await _audit.CountForEntityAsync(AuditService.BookingEntity, fixture.BookingId))
        End Function

        <TestMethod>
        Public Async Function AnotherTutorCannotApproveIt() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim otherTutorPerson = Await New UserRepository().FindByUserNameOrEmailAsync("lerato_m")

            Dim result = Await _postponements.ApproveAsync(
                otherTutorPerson.PersonId, proposal.Data, "not mine to answer")

            Assert.IsFalse(result.Success)
            Assert.AreEqual("PendingTutorReview", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.PostponementRequest WHERE PostponementId = @p0;", proposal.Data))
        End Function

        <TestMethod>
        Public Async Function AProposalCannotBeAnsweredTwice() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Await _postponements.ApproveAsync(_scenario.TutorPerson.PersonId, proposal.Data, Nothing)

            Dim again = Await _postponements.ApproveAsync(
                _scenario.TutorPerson.PersonId, proposal.Data, Nothing)

            Assert.IsFalse(again.Success, again.Message)
        End Function

        ''' <summary>
        ''' The tutor already has another session across the proposed window.
        ''' </summary>
        <TestMethod>
        Public Async Function AWindowTheTutorHasAlreadyFilledIsRefused() As Task
            ' A session the tutor already holds, for a different student.
            Dim occupied = Await _scenario.NewScheduledBookingAsync(_scenario.OtherStudentId)

            ' The acting student proposes to move onto exactly that window.
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim proposal = Await _postponements.ProposeAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                occupied.SlotDate, occupied.StartTime, occupied.EndTime,
                ScheduleChangeReason.Other, Nothing)
            Assert.IsTrue(proposal.Success, proposal.Message)

            Dim result = Await _postponements.ApproveAsync(
                _scenario.TutorPerson.PersonId, proposal.Data, Nothing)

            Assert.IsFalse(result.Success, "the tutor cannot be in two places at once")
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking WHERE BookingId = @p0 AND BookingDate = @p1;",
                fixture.BookingId, fixture.SlotDate),
                "a refused approval must leave the booking where it was")
        End Function

        ''' <summary>
        ''' The student has another session across the proposed window - checked as well as
        ''' the tutor's diary, because a student cannot be in two places either.
        ''' </summary>
        <TestMethod>
        Public Async Function AWindowTheStudentHasAlreadyFilledIsRefused() As Task
            Dim otherSession = Await _scenario.NewScheduledBookingAsync()
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim proposal = Await _postponements.ProposeAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                otherSession.SlotDate, otherSession.StartTime, otherSession.EndTime,
                ScheduleChangeReason.Other, Nothing)
            Assert.IsTrue(proposal.Success, proposal.Message)

            Dim result = Await _postponements.ApproveAsync(
                _scenario.TutorPerson.PersonId, proposal.Data, Nothing)

            Assert.IsFalse(result.Success, "the student already has a session then")
        End Function

        ''' <summary>
        ''' Two approvals for the same slot, started together. The AT-15 pattern applied to
        ''' this flow: a read-then-write with no serialisable transaction passes every
        ''' sequential test here and still double-books.
        ''' </summary>
        <TestMethod>
        Public Async Function TwoConcurrentApprovalsForOneWindowProduceOneBooking() As Task
            Dim target As Date = TestScenario.NextFreeDate()

            Dim first = Await _scenario.NewScheduledBookingAsync()
            Dim second = Await _scenario.NewScheduledBookingAsync(_scenario.OtherStudentId)

            Dim proposalOne = Await _postponements.ProposeAsync(
                _scenario.Student.PersonId, first.BookingId, target, NineOClock, ElevenOClock,
                ScheduleChangeReason.Other, Nothing)
            Dim proposalTwo = Await _postponements.ProposeAsync(
                _scenario.OtherStudent.PersonId, second.BookingId, target, NineOClock, ElevenOClock,
                ScheduleChangeReason.Other, Nothing)

            Assert.IsTrue(proposalOne.Success, proposalOne.Message)
            Assert.IsTrue(proposalTwo.Success, proposalTwo.Message)

            ' Started before either is awaited - awaiting them in turn would prove nothing.
            Dim approveOne = _postponements.ApproveAsync(_scenario.TutorPerson.PersonId, proposalOne.Data, Nothing)
            Dim approveTwo = _postponements.ApproveAsync(_scenario.TutorPerson.PersonId, proposalTwo.Data, Nothing)
            Await Task.WhenAll(approveOne, approveTwo)

            Dim winners As Integer = 0
            If approveOne.Result.Success Then winners += 1
            If approveTwo.Result.Success Then winners += 1

            Assert.AreEqual(1, winners,
                $"exactly one may win - [{approveOne.Result.Message}] [{approveTwo.Result.Message}]")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking " &
                "WHERE BookingDate = @p0 AND Status = 'Scheduled' " &
                "  AND StartTime < @p2 AND @p1 < EndTime " &
                "  AND BookingId IN (@p3, @p4);",
                target, NineOClock, ElevenOClock, first.BookingId, second.BookingId),
                "the tutor must not end up double-booked")
        End Function

#End Region

#Region "Declining"

        <TestMethod>
        Public Async Function DecliningLeavesTheBookingWhereItWas() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim result = Await _postponements.DeclineAsync(
                _scenario.TutorPerson.PersonId, proposal.Data, "I teach another group then.")

            Assert.IsTrue(result.Success, result.Message)

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking " &
                "WHERE BookingId = @p0 AND BookingDate = @p1 AND Status = 'Scheduled';",
                fixture.BookingId, fixture.SlotDate),
                "a declined proposal must change nothing")

            Assert.AreEqual("I teach another group then.", Await TestDatabase.ScalarStringAsync(
                "SELECT ISNULL(TutorResponseNote, '') FROM dbo.PostponementRequest WHERE PostponementId = @p0;",
                proposal.Data), "the student needs to know why")
        End Function

        <TestMethod>
        Public Async Function DecliningWithoutAReasonIsRefused() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim result = Await _postponements.DeclineAsync(
                _scenario.TutorPerson.PersonId, proposal.Data, "   ")

            Assert.IsFalse(result.Success, "a refusal with no reason leaves the student nothing to act on")
            Assert.AreEqual("PendingTutorReview", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.PostponementRequest WHERE PostponementId = @p0;", proposal.Data))
        End Function

        <TestMethod>
        Public Async Function DecliningNotifiesTheStudent() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim before = Await NoticesAsync(_scenario.Student.PersonId, "BookingPostponeDeclined")
            Await _postponements.DeclineAsync(_scenario.TutorPerson.PersonId, proposal.Data, "Not that week.")

            Assert.AreEqual(before + 1,
                Await NoticesAsync(_scenario.Student.PersonId, "BookingPostponeDeclined"))
        End Function

#End Region

#Region "Withdrawing"

        <TestMethod>
        Public Async Function AStudentCanWithdrawTheirOwnPendingProposal() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim result = Await _postponements.WithdrawAsync(_scenario.Student.PersonId, proposal.Data)

            Assert.IsTrue(result.Success, result.Message)
            Assert.AreEqual("Cancelled", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.PostponementRequest WHERE PostponementId = @p0;", proposal.Data))

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking " &
                "WHERE BookingId = @p0 AND BookingDate = @p1 AND Status = 'Scheduled';",
                fixture.BookingId, fixture.SlotDate),
                "withdrawing must leave the session exactly as it was")
        End Function

        ''' <summary>
        ''' The tutor had it in a queue they were about to answer; it should not simply vanish.
        ''' </summary>
        <TestMethod>
        Public Async Function WithdrawingTellsTheTutor() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim before = Await NoticesAsync(_scenario.TutorPerson.PersonId, "BookingPostponeWithdrawn")
            Await _postponements.WithdrawAsync(_scenario.Student.PersonId, proposal.Data)

            Assert.AreEqual(before + 1,
                Await NoticesAsync(_scenario.TutorPerson.PersonId, "BookingPostponeWithdrawn"))
        End Function

        <TestMethod>
        Public Async Function AnotherStudentCannotWithdrawIt() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Dim result = Await _postponements.WithdrawAsync(_scenario.OtherStudent.PersonId, proposal.Data)

            Assert.IsFalse(result.Success)
            Assert.AreEqual("PendingTutorReview", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.PostponementRequest WHERE PostponementId = @p0;", proposal.Data))
        End Function

        <TestMethod>
        Public Async Function AnAnsweredProposalCannotBeWithdrawn() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Await _postponements.DeclineAsync(_scenario.TutorPerson.PersonId, proposal.Data, "Not that week.")

            Dim result = Await _postponements.WithdrawAsync(_scenario.Student.PersonId, proposal.Data)

            Assert.IsFalse(result.Success, "the tutor has already answered it")
            Assert.AreEqual("Declined", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.PostponementRequest WHERE PostponementId = @p0;", proposal.Data))
        End Function

        ''' <summary>Withdrawing frees the booking for a fresh proposal.</summary>
        <TestMethod>
        Public Async Function AfterWithdrawingANewProposalIsAllowed() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim first = Await ProposeAsync(fixture, TestScenario.NextFreeDate())

            Await _postponements.WithdrawAsync(_scenario.Student.PersonId, first.Data)

            Dim second = Await ProposeAsync(fixture, TestScenario.NextFreeDate())
            Assert.IsTrue(second.Success, second.Message)
        End Function

#End Region

#Region "Administrator rescheduling (Part 12.7)"

        <TestMethod>
        Public Async Function AnAdministratorCanMoveAScheduledSession() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")
            Dim newDate As Date = TestScenario.NextFreeDate()

            Dim result = Await _bookings.RescheduleAsync(
                admin.PersonId, fixture.BookingId, newDate, NineOClock, ElevenOClock,
                "Room double-booked by the faculty.")

            Assert.IsTrue(result.Success, result.Message)
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking " &
                "WHERE BookingId = @p0 AND BookingDate = @p1 AND StartTime = @p2 AND Status = 'Scheduled';",
                fixture.BookingId, newDate, NineOClock))
        End Function

        <TestMethod>
        Public Async Function ReschedudlingTellsBothPartiesAndIsAudited() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")

            Dim studentBefore = Await NoticesAsync(_scenario.Student.PersonId, "BookingRescheduled")
            Dim tutorBefore = Await NoticesAsync(_scenario.TutorPerson.PersonId, "BookingRescheduled")
            Dim auditBefore = Await _audit.CountForEntityAsync(AuditService.BookingEntity, fixture.BookingId)

            Await _bookings.RescheduleAsync(admin.PersonId, fixture.BookingId,
                                            TestScenario.NextFreeDate(), NineOClock, ElevenOClock,
                                            "Venue change.")

            ' Neither of them asked for this, so both are told.
            Assert.AreEqual(studentBefore + 1, Await NoticesAsync(_scenario.Student.PersonId, "BookingRescheduled"))
            Assert.AreEqual(tutorBefore + 1, Await NoticesAsync(_scenario.TutorPerson.PersonId, "BookingRescheduled"))
            Assert.AreEqual(auditBefore + 1,
                Await _audit.CountForEntityAsync(AuditService.BookingEntity, fixture.BookingId))
        End Function

        <TestMethod>
        Public Async Function ReschedulingWithoutAReasonIsRefused() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")

            Dim result = Await _bookings.RescheduleAsync(
                admin.PersonId, fixture.BookingId, TestScenario.NextFreeDate(),
                NineOClock, ElevenOClock, "   ")

            Assert.IsFalse(result.Success, "an override with no stated reason is not an audit trail")
        End Function

        <TestMethod>
        Public Async Function ReschedulingOntoABusyWindowIsRefused() As Task
            Dim occupied = Await _scenario.NewScheduledBookingAsync(_scenario.OtherStudentId)
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")

            Dim result = Await _bookings.RescheduleAsync(
                admin.PersonId, fixture.BookingId, occupied.SlotDate,
                occupied.StartTime, occupied.EndTime, "Trying to double-book the tutor.")

            Assert.IsFalse(result.Success, "an administrator cannot double-book a tutor either")
            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking WHERE BookingId = @p0 AND BookingDate = @p1;",
                fixture.BookingId, fixture.SlotDate), "a refused move must change nothing")
        End Function

        <TestMethod>
        Public Async Function ACompletedSessionCannotBeMoved() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")

            Dim result = Await _bookings.RescheduleAsync(
                admin.PersonId, fixture.BookingId, TestScenario.NextFreeDate(),
                NineOClock, ElevenOClock, "Too late.")

            Assert.IsFalse(result.Success)
        End Function

        ''' <summary>
        ''' Moving the session settles the schedule, so an open proposal about where it should
        ''' go is moot - and leaving it open would let a tutor "approve" a move afterwards.
        ''' </summary>
        <TestMethod>
        Public Async Function ReschedulingClosesAnOpenProposal() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim proposal = Await ProposeAsync(fixture, TestScenario.NextFreeDate())
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")

            Await _bookings.RescheduleAsync(admin.PersonId, fixture.BookingId,
                                            TestScenario.NextFreeDate(), NineOClock, ElevenOClock,
                                            "Faculty timetable change.")

            Assert.AreEqual("Cancelled", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.PostponementRequest WHERE PostponementId = @p0;", proposal.Data))
        End Function

#End Region

#Region "Interaction with cancellation"

        ''' <summary>
        ''' Cancelling the session closes any open proposal. Leaving it open would let a
        ''' tutor later "approve" a move for a session that no longer exists.
        ''' </summary>
        <TestMethod>
        Public Async Function CancellingTheBookingClosesAnOpenProposal() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim newDate As Date = TestScenario.NextFreeDate()
            Dim proposal = Await ProposeAsync(fixture, newDate)

            Await _bookings.CancelAsync(_scenario.Student.PersonId, fixture.BookingId,
                                        ScheduleChangeReason.Medical, Nothing)

            Assert.AreEqual("Cancelled", Await TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.PostponementRequest WHERE PostponementId = @p0;", proposal.Data))

            Dim late = Await _postponements.ApproveAsync(
                _scenario.TutorPerson.PersonId, proposal.Data, Nothing)
            Assert.IsFalse(late.Success, "a cancelled session cannot be moved")
        End Function

#End Region

        Private Shared Function NoticesAsync(personId As Integer, kind As String) As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification " &
                "WHERE PersonId = @p0 AND NotificationType = @p1;", personId, kind)
        End Function

    End Class

End Namespace
