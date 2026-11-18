Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Services

Namespace TutorConnect.Tests

    ''' <summary>
    ''' Part 12.3 - cancelling a scheduled session, with a reason, by whichever party.
    ''' </summary>
    ''' <remarks>
    ''' Not to be confused with BR-13, which is <c>RequestService.CancelAsync</c> and
    ''' withdraws a request that has not been accepted yet. That rule is unchanged and its
    ''' test still stands; this covers the booking a confirmed request produced.
    ''' </remarks>
    <TestClass>
    Public Class CancellationTests

        Private ReadOnly _bookings As New BookingService()
        Private ReadOnly _audit As New AuditRepository()
        Private _scenario As TestScenario

        <TestInitialize>
        Public Sub Setup()
            _scenario = TestScenario.CreateAsync().GetAwaiter().GetResult()
        End Sub

        Private Shared Function StatusOfAsync(bookingId As Integer) As Task(Of String)
            Return TestDatabase.ScalarStringAsync(
                "SELECT Status FROM dbo.Booking WHERE BookingId = @p0;", bookingId)
        End Function

#Region "The happy path"

        <TestMethod>
        Public Async Function Cancelling_RecordsWhoWhyAndWhen() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _bookings.CancelAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                ScheduleChangeReason.TimetableClash, "Clashes with a practical.")

            Assert.IsTrue(result.Success, result.Message)
            Assert.AreEqual("Cancelled", Await StatusOfAsync(fixture.BookingId))

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Booking " &
                "WHERE BookingId = @p0 AND CancelledAt IS NOT NULL " &
                "  AND CancelledByPersonId = @p1 AND CancelReasonCategory = 'TimetableClash' " &
                "  AND CancelReason = @p2;",
                fixture.BookingId, _scenario.Student.PersonId, "Clashes with a practical."),
                "who, why and when must all be stored")
        End Function

        ''' <summary>The slot goes back on the market rather than being lost.</summary>
        <TestMethod>
        Public Async Function Cancelling_ReleasesTheSlot() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT CAST(IsBooked AS INT) FROM dbo.TutorAvailability WHERE AvailabilityId = @p0;",
                fixture.AvailabilityId), "booked to begin with")

            Await _bookings.CancelAsync(_scenario.Student.PersonId, fixture.BookingId,
                                        ScheduleChangeReason.Medical, Nothing)

            Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                "SELECT CAST(IsBooked AS INT) FROM dbo.TutorAvailability WHERE AvailabilityId = @p0;",
                fixture.AvailabilityId))
        End Function

        <TestMethod>
        Public Async Function Cancelling_WritesAnAuditRecord() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim before = Await _audit.CountForEntityAsync(AuditService.BookingEntity, fixture.BookingId)

            Await _bookings.CancelAsync(_scenario.Student.PersonId, fixture.BookingId,
                                        ScheduleChangeReason.ExamPreparation, "Exams start Monday.")

            Assert.AreEqual(before + 1,
                Await _audit.CountForEntityAsync(AuditService.BookingEntity, fixture.BookingId))

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.AuditLog " &
                "WHERE EntityType = 'Booking' AND EntityId = @p0 AND ActionType = 'BookingCancelled' " &
                "  AND ActorPersonId = @p1 AND ActorRole = 'Student' AND NewState = 'Cancelled';",
                fixture.BookingId, _scenario.Student.PersonId),
                "the record must name the actor, the action and the outcome")
        End Function

#End Region

#Region "Who gets told"

        ''' <summary>
        ''' The earlier implementation notified the student whoever had acted, so a student
        ''' cancelling their own session was told about it and the tutor heard nothing.
        ''' </summary>
        <TestMethod>
        Public Async Function WhenTheStudentCancels_TheTutorIsNotified() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim tutorBefore = Await CancelNoticesAsync(_scenario.TutorPerson.PersonId)
            Dim studentBefore = Await CancelNoticesAsync(_scenario.Student.PersonId)

            Await _bookings.CancelAsync(_scenario.Student.PersonId, fixture.BookingId,
                                        ScheduleChangeReason.Medical, Nothing)

            Assert.AreEqual(tutorBefore + 1, Await CancelNoticesAsync(_scenario.TutorPerson.PersonId),
                            "the tutor must be told")
            Assert.AreEqual(studentBefore, Await CancelNoticesAsync(_scenario.Student.PersonId),
                            "the person who pressed the button does not need telling")
        End Function

        <TestMethod>
        Public Async Function WhenTheTutorCancels_TheStudentIsNotified() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim studentBefore = Await CancelNoticesAsync(_scenario.Student.PersonId)
            Dim tutorBefore = Await CancelNoticesAsync(_scenario.TutorPerson.PersonId)

            Await _bookings.CancelAsync(_scenario.TutorPerson.PersonId, fixture.BookingId,
                                        ScheduleChangeReason.Other, "I am unwell.")

            Assert.AreEqual(studentBefore + 1, Await CancelNoticesAsync(_scenario.Student.PersonId))
            Assert.AreEqual(tutorBefore, Await CancelNoticesAsync(_scenario.TutorPerson.PersonId))
        End Function

        Private Shared Function CancelNoticesAsync(personId As Integer) As Task(Of Integer)
            Return TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.Notification " &
                "WHERE PersonId = @p0 AND NotificationType = 'BookingCancelled';", personId)
        End Function

#End Region

#Region "What is refused"

        ''' <summary>
        ''' An administrator can open any booking.
        ''' </summary>
        ''' <remarks>
        ''' This was missing, and its absence hid a real defect: `GetForViewerAsync` admitted
        ''' only the owning student and the assigned tutor, so an administrator was treated as
        ''' a stranger — which silently broke administrator cancellation and rescheduling,
        ''' both of which call it first. Manage Bookings lists every booking, so being unable
        ''' to open one was never coherent.
        ''' </remarks>
        <TestMethod>
        Public Async Function AnAdministratorCanOpenAnyBooking() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")

            Dim result = Await _bookings.GetForViewerAsync(fixture.BookingId, admin.PersonId)

            Assert.IsTrue(result.Success, result.Message)
            Assert.AreEqual(fixture.BookingId, result.Data.BookingId)
        End Function

        ''' <summary>
        ''' An administrator cancelling tells <b>both</b> parties, since neither of them acted.
        ''' </summary>
        <TestMethod>
        Public Async Function AnAdministratorCanCancelAndBothPartiesAreTold() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()
            Dim admin = Await New UserRepository().FindByUserNameOrEmailAsync("admin")

            Dim studentBefore = Await CancelNoticesAsync(_scenario.Student.PersonId)
            Dim tutorBefore = Await CancelNoticesAsync(_scenario.TutorPerson.PersonId)

            Dim result = Await _bookings.CancelAsync(
                admin.PersonId, fixture.BookingId,
                ScheduleChangeReason.Other, "Venue closed by the faculty.")

            Assert.IsTrue(result.Success, result.Message)
            Assert.AreEqual("Cancelled", Await StatusOfAsync(fixture.BookingId))

            Assert.AreEqual(studentBefore + 1, Await CancelNoticesAsync(_scenario.Student.PersonId))
            Assert.AreEqual(tutorBefore + 1, Await CancelNoticesAsync(_scenario.TutorPerson.PersonId))

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.AuditLog " &
                "WHERE EntityType = 'Booking' AND EntityId = @p0 AND ActionType = 'BookingCancelled' " &
                "  AND ActorRole = 'Administrator';", fixture.BookingId),
                "the audit record must name the administrator's role")
        End Function

        <TestMethod>
        Public Async Function AnotherStudentCannotCancelIt() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _bookings.CancelAsync(
                _scenario.OtherStudent.PersonId, fixture.BookingId,
                ScheduleChangeReason.Other, "not mine")

            Assert.IsFalse(result.Success)
            Assert.AreEqual("Scheduled", Await StatusOfAsync(fixture.BookingId),
                            "the booking must be untouched")
        End Function

        <TestMethod>
        Public Async Function ACancelledSessionCannotBeCancelledAgain() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim first = Await _bookings.CancelAsync(_scenario.Student.PersonId, fixture.BookingId,
                                                    ScheduleChangeReason.Medical, Nothing)
            Assert.IsTrue(first.Success, first.Message)

            Dim second = Await _bookings.CancelAsync(_scenario.Student.PersonId, fixture.BookingId,
                                                     ScheduleChangeReason.Medical, Nothing)
            Assert.IsFalse(second.Success, "a second click must not write a second cancellation")
        End Function

        <TestMethod>
        Public Async Function ACompletedSessionCannotBeCancelled() As Task
            Dim fixture = Await _scenario.NewCompletedBookingAsync()

            Dim result = Await _bookings.CancelAsync(_scenario.Student.PersonId, fixture.BookingId,
                                                     ScheduleChangeReason.Other, "too late")

            Assert.IsFalse(result.Success)
            Assert.AreEqual("Completed", Await StatusOfAsync(fixture.BookingId))
        End Function

        <TestMethod>
        Public Async Function ANoteOverTheLimitIsRefused() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Dim result = Await _bookings.CancelAsync(
                _scenario.Student.PersonId, fixture.BookingId,
                ScheduleChangeReason.Other, New String("x"c, 201))

            Assert.IsFalse(result.Success, "the column holds 200 characters")
            Assert.AreEqual("Scheduled", Await StatusOfAsync(fixture.BookingId))
        End Function

        ''' <summary>
        ''' The category is an enum in the service signature, so an arbitrary string cannot
        ''' reach it that way. This proves the database refuses one regardless, which is what
        ''' protects a write that never goes through the service.
        ''' </summary>
        <TestMethod>
        Public Async Function TheDatabaseRefusesACategoryOutsideTheList() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Await Assert.ThrowsExceptionAsync(Of System.Data.SqlClient.SqlException)(
                Function()
                    Return TestDatabase.ExecuteAsync(
                        "UPDATE dbo.Booking SET Status = 'Cancelled', CancelledAt = SYSUTCDATETIME(), " &
                        "CancelledByPersonId = @p1, CancelReasonCategory = 'BecauseISaidSo' " &
                        "WHERE BookingId = @p0;",
                        fixture.BookingId, _scenario.Student.PersonId)
                End Function)
        End Function

        ''' <summary>
        ''' A cancelled booking must say who cancelled it and why. Without the constraint a
        ''' status could be flipped and leave the history blank.
        ''' </summary>
        <TestMethod>
        Public Async Function TheDatabaseRefusesACancellationWithNoReason() As Task
            Dim fixture = Await _scenario.NewScheduledBookingAsync()

            Await Assert.ThrowsExceptionAsync(Of System.Data.SqlClient.SqlException)(
                Function()
                    Return TestDatabase.ExecuteAsync(
                        "UPDATE dbo.Booking SET Status = 'Cancelled' WHERE BookingId = @p0;",
                        fixture.BookingId)
                End Function)
        End Function

#End Region

#Region "Part 12.1 and 12.6 - inactive subjects"

        <TestMethod>
        Public Async Function AnInactiveSubjectCannotBeAddedToAStudentsList() As Task
            Dim students As New StudentService()
            Dim subjectId = Await TestDatabase.ScalarAsync(
                "SELECT SubjectId FROM dbo.[Subject] WHERE SubjectName = 'Information Systems';")
            Dim levelId = Await TestDatabase.ScalarAsync(
                "SELECT StudyLevelId FROM dbo.StudyLevel WHERE StudyLevelName = 'First Year';")

            Await TestDatabase.ExecuteAsync(
                "UPDATE dbo.[Subject] SET IsActive = 0 WHERE SubjectId = @p0;", subjectId)

            Try
                ' Straight to the service, as a tampered post would - the dropdown would not
                ' have offered this subject at all.
                Dim result = Await students.AddSubjectAsync(_scenario.StudentId, subjectId, levelId)

                Assert.IsFalse(result.Success, "an inactive subject must be refused")
                Assert.AreEqual(0, Await TestDatabase.ScalarAsync(
                    "SELECT COUNT(*) FROM dbo.StudentSubject " &
                    "WHERE StudentId = @p0 AND SubjectId = @p1 AND StudyLevelId = @p2;",
                    _scenario.StudentId, subjectId, levelId))
            Finally
                TestDatabase.ExecuteAsync(
                    "UPDATE dbo.[Subject] SET IsActive = 1 WHERE SubjectId = @p0;",
                    subjectId).GetAwaiter().GetResult()
            End Try
        End Function

        <TestMethod>
        Public Async Function AnInactiveSubjectCannotBeRequested() As Task
            Dim requests As New RequestService()
            Dim slotDate As Date = TestScenario.NextFreeDate()
            Dim availabilityId = Await TestDatabase.CreateAvailabilityDirectAsync(
                _scenario.TutorSubjectId, slotDate, "09:00", "11:00")

            Await TestDatabase.ExecuteAsync(
                "UPDATE s SET s.IsActive = 0 FROM dbo.[Subject] AS s " &
                "JOIN dbo.TutorSubject AS ts ON ts.SubjectId = s.SubjectId " &
                "WHERE ts.TutorSubjectId = @p0;", _scenario.TutorSubjectId)

            Try
                Dim result = Await requests.CreateAsync(
                    _scenario.StudentId, _scenario.TutorSubjectId, availabilityId,
                    slotDate, TimeSpan.Parse("09:00"), TimeSpan.Parse("11:00"), Nothing)

                Assert.IsFalse(result.Success, "Part 12.6: no new requests for a withdrawn subject")
            Finally
                TestDatabase.ExecuteAsync(
                    "UPDATE s SET s.IsActive = 1 FROM dbo.[Subject] AS s " &
                    "JOIN dbo.TutorSubject AS ts ON ts.SubjectId = s.SubjectId " &
                    "WHERE ts.TutorSubjectId = @p0;",
                    _scenario.TutorSubjectId).GetAwaiter().GetResult()
            End Try
        End Function

        <TestMethod>
        Public Async Function AnInactiveSubjectCannotHaveNewAvailability() As Task
            Dim availability As New AvailabilityService()

            Await TestDatabase.ExecuteAsync(
                "UPDATE s SET s.IsActive = 0 FROM dbo.[Subject] AS s " &
                "JOIN dbo.TutorSubject AS ts ON ts.SubjectId = s.SubjectId " &
                "WHERE ts.TutorSubjectId = @p0;", _scenario.TutorSubjectId)

            Try
                Dim result = Await availability.AddAsync(
                    _scenario.TutorId, _scenario.TutorSubjectId, TestScenario.NextFreeDate(),
                    TimeSpan.Parse("09:00"), TimeSpan.Parse("11:00"))

                Assert.IsFalse(result.Success)
            Finally
                TestDatabase.ExecuteAsync(
                    "UPDATE s SET s.IsActive = 1 FROM dbo.[Subject] AS s " &
                    "JOIN dbo.TutorSubject AS ts ON ts.SubjectId = s.SubjectId " &
                    "WHERE ts.TutorSubjectId = @p0;",
                    _scenario.TutorSubjectId).GetAwaiter().GetResult()
            End Try
        End Function

#End Region

    End Class

End Namespace
