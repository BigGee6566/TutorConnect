Option Strict On
Option Explicit On

Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Models
Imports TutorConnect.Services

Namespace TutorConnect.Tests

    ''' <summary>
    ''' Resolves the seeded actors and builds the states the workflow tests need.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' The original console harnesses were one long script: the request created for AT-09
    ''' was the booking completed by AT-18 and reviewed by AT-19. That reads well but makes
    ''' every check depend on the one before it, so a single early failure cascades and no
    ''' test can be run on its own. Each test method here builds what it needs instead.
    ''' </para>
    ''' <para>
    ''' Independence costs something: several tests book the same student with the same
    ''' tutor, and BR-12 forbids a student holding two overlapping bookings. Rather than
    ''' order the tests carefully - which would reintroduce the coupling - every slot is
    ''' taken from <see cref="NextFreeDate"/>, which hands out a distinct future date per
    ''' call. Two tests therefore cannot collide no matter what order they run in.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class TestScenario

        Private Shared _dayCounter As Integer = 0

        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _requests As New RequestService()
        Private ReadOnly _bookings As New BookingService()

        Private Sub New()
        End Sub

        ''' <summary>The acting student (seeded).</summary>
        Public Property Student As Person
        Public Property StudentId As Integer

        ''' <summary>A second student, used for the "someone else's record" checks.</summary>
        Public Property OtherStudent As Person
        Public Property OtherStudentId As Integer

        ''' <summary>The acting tutor (seeded, approved, holds at least one subject).</summary>
        Public Property TutorPerson As Person
        Public Property TutorId As Integer
        Public Property TutorSubjectId As Integer

        Public Shared Async Function CreateAsync() As Task(Of TestScenario)
            Dim scenario As New TestScenario()

            scenario.Student = Await scenario._users.FindByUserNameOrEmailAsync("sinesipho_t")
            Assert.IsNotNull(scenario.Student, "seed user sinesipho_t")
            scenario.StudentId = (Await scenario._users.GetStudentByPersonIdAsync(scenario.Student.PersonId)).StudentId

            scenario.OtherStudent = Await scenario._users.FindByUserNameOrEmailAsync("john_doe")
            Assert.IsNotNull(scenario.OtherStudent, "seed user john_doe")
            scenario.OtherStudentId = (Await scenario._users.GetStudentByPersonIdAsync(scenario.OtherStudent.PersonId)).StudentId

            scenario.TutorPerson = Await scenario._users.FindByUserNameOrEmailAsync("nhlawulo_m")
            Assert.IsNotNull(scenario.TutorPerson, "seed user nhlawulo_m")
            scenario.TutorId = (Await scenario._users.GetTutorByPersonIdAsync(scenario.TutorPerson.PersonId)).TutorId

            scenario.TutorSubjectId = Await TestDatabase.ScalarAsync(
                "SELECT TOP 1 TutorSubjectId FROM dbo.TutorSubject WHERE TutorId = @p0;", scenario.TutorId)
            Assert.AreNotEqual(0, scenario.TutorSubjectId, "the seeded tutor must hold at least one subject")

            Return scenario
        End Function

        ''' <summary>
        ''' A future date no other test will use, so BR-12 cannot make one test fail
        ''' because of another.
        ''' </summary>
        Public Shared Function NextFreeDate() As Date
            Return Date.Today.AddDays(30 + Interlocked.Increment(_dayCounter))
        End Function

        ''' <summary>Creates a pending request on a private date.</summary>
        Public Async Function NewPendingRequestAsync(Optional forStudentId As Integer = 0) As Task(Of RequestFixture)
            Dim slotDate As Date = NextFreeDate()
            Dim startTime As TimeSpan = TimeSpan.Parse("09:00")
            Dim endTime As TimeSpan = TimeSpan.Parse("11:00")

            Dim availabilityId As Integer = Await TestDatabase.CreateAvailabilityDirectAsync(
                TutorSubjectId, slotDate, "09:00", "11:00")

            Dim owner As Integer = If(forStudentId = 0, StudentId, forStudentId)

            Dim created = Await _requests.CreateAsync(owner, TutorSubjectId, availabilityId,
                                                      slotDate, startTime, endTime, "Automated test request.")
            Assert.IsTrue(created.Success, "fixture: create request - " & created.Message)

            Return New RequestFixture With {
                .RequestId = created.Data, .AvailabilityId = availabilityId, .StudentId = owner,
                .SlotDate = slotDate, .StartTime = startTime, .EndTime = endTime
            }
        End Function

        ''' <summary>Creates a request and has the tutor accept it, giving a Scheduled booking.</summary>
        Public Async Function NewScheduledBookingAsync(Optional forStudentId As Integer = 0) As Task(Of RequestFixture)
            Dim fixture = Await NewPendingRequestAsync(forStudentId)

            Dim accepted = Await _bookings.AcceptRequestAsync(TutorId, fixture.RequestId)
            Assert.IsTrue(accepted.Success, "fixture: accept request - " & accepted.Message)

            fixture.BookingId = Await TestDatabase.ScalarAsync(
                "SELECT TOP 1 BookingId FROM dbo.Booking WHERE RequestId = @p0;", fixture.RequestId)
            Assert.AreNotEqual(0, fixture.BookingId, "fixture: booking row")

            Return fixture
        End Function

        ''' <summary>
        ''' Creates a booking, moves it into the past and completes it.
        ''' </summary>
        ''' <remarks>
        ''' The date is moved rather than the clock, and it is moved *before* completing, so
        ''' BR-23 is satisfied honestly by a session that really has finished rather than
        ''' bypassed.
        ''' </remarks>
        Public Async Function NewCompletedBookingAsync(Optional forStudentId As Integer = 0) As Task(Of RequestFixture)
            Dim fixture = Await NewScheduledBookingAsync(forStudentId)

            Await TestDatabase.ExecuteAsync(
                "UPDATE dbo.Booking SET BookingDate = @p0 WHERE BookingId = @p1;",
                Date.Today.AddDays(-1), fixture.BookingId)

            Dim completed = Await _bookings.MarkCompletedAsync(TutorId, fixture.BookingId)
            Assert.IsTrue(completed.Success, "fixture: complete booking - " & completed.Message)

            Return fixture
        End Function

    End Class

    ''' <summary>What a fixture produced, so a test can assert against it.</summary>
    Public Class RequestFixture
        Public Property RequestId As Integer
        Public Property BookingId As Integer
        Public Property AvailabilityId As Integer
        Public Property StudentId As Integer
        Public Property SlotDate As Date
        Public Property StartTime As TimeSpan
        Public Property EndTime As TimeSpan
    End Class

End Namespace
