Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Utilities

Namespace Services

    ''' <summary>
    ''' Bookings: accept, decline, complete, cancel.
    ''' </summary>
    ''' <remarks>
    ''' The serialisable transaction that enforces BR-14 and BR-15 lives in
    ''' <see cref="BookingRepository.AcceptRequestAsync"/>, because a transaction cannot
    ''' exist without the connection the repository owns. This class decides what the
    ''' outcome means and what the user is told.
    ''' </remarks>
    Public Class BookingService
        Implements IBookingService

        Private ReadOnly _bookings As BookingRepository
        Private ReadOnly _requests As RequestRepository
        Private ReadOnly _tutors As TutorRepository
        Private ReadOnly _users As UserRepository
        Private ReadOnly _notifications As INotificationService

        Public Sub New()
            Me.New(New BookingRepository(), New RequestRepository(), New TutorRepository(),
                   New UserRepository(), New NotificationService())
        End Sub

        Public Sub New(bookings As BookingRepository,
                       requests As RequestRepository,
                       tutors As TutorRepository,
                       users As UserRepository,
                       notifications As INotificationService)
            _bookings = bookings
            _requests = requests
            _tutors = tutors
            _users = users
            _notifications = notifications
        End Sub

        ''' <inheritdoc/>
        Public Async Function AcceptRequestAsync(tutorId As Integer,
                                                 requestId As Integer) As Task(Of ServiceResult(Of Integer)) _
            Implements IBookingService.AcceptRequestAsync

            If tutorId <= 0 Then
                Return ServiceResult(Of Integer).Fail("You are not signed in as a tutor.")
            End If

            Dim request As SessionRequest = Await _requests.GetByIdAsync(requestId).ConfigureAwait(False)

            Dim outcome As AcceptOutcome

            Try
                outcome = Await _bookings.AcceptRequestAsync(requestId, tutorId).ConfigureAwait(False)

            Catch ex As System.Data.SqlClient.SqlException When ex.Number = 1205
                ' Deadlock victim. Two tutors, or two tabs, reached the same slot at once;
                ' one was chosen to roll back. Nothing was written, so this is a conflict
                ' to report, not a fault.
                Return ServiceResult(Of Integer).Fail(
                    "Another change reached that slot at the same moment. Refresh and try again.")
            End Try

            Select Case outcome
                Case AcceptOutcome.RequestNotPending
                    Return ServiceResult(Of Integer).Fail(
                        "That request is no longer pending. It may have been withdrawn or already answered.")

                Case AcceptOutcome.OutsideAvailability
                    Return ServiceResult(Of Integer).Fail(
                        "That request no longer sits inside one of your published slots.")

                Case AcceptOutcome.SlotAlreadyTaken
                    Return ServiceResult(Of Integer).Fail("That slot has already been booked.")

                Case AcceptOutcome.TutorConflict
                    Return ServiceResult(Of Integer).Fail(
                        "You already have a scheduled session that overlaps this time.")

                Case AcceptOutcome.StudentConflict
                    Return ServiceResult(Of Integer).Fail(
                        "The student already has a booking that overlaps this time.")
            End Select

            If request IsNot Nothing Then
                Await NotifyStudentAsync(
                    request.StudentId,
                    NotificationType.RequestAccepted,
                    "Request accepted",
                    $"Your session on {request.RequestedDate:dd/MM/yyyy} at " &
                    $"{request.StartTime:hh\:mm} has been confirmed.",
                    "~/Student/Bookings/MyBookings.aspx").ConfigureAwait(False)
            End If

            Return ServiceResult(Of Integer).Ok(requestId, "Request accepted and the session is booked.")
        End Function

        ''' <inheritdoc/>
        Public Async Function DeclineRequestAsync(tutorId As Integer,
                                                  requestId As Integer,
                                                  declineReason As String) As Task(Of ServiceResult) _
            Implements IBookingService.DeclineRequestAsync

            Dim request As SessionRequest = Await _requests.GetByIdAsync(requestId).ConfigureAwait(False)

            Dim declined As Boolean =
                Await _requests.DeclineAsync(requestId, tutorId, declineReason).ConfigureAwait(False)

            If Not declined Then
                Return ServiceResult.Fail(
                    "That request is no longer pending, or it is not yours to answer.")
            End If

            If request IsNot Nothing Then
                Dim detail As String = If(String.IsNullOrWhiteSpace(declineReason),
                                          String.Empty,
                                          " Reason: " & declineReason)

                Await NotifyStudentAsync(
                    request.StudentId,
                    NotificationType.RequestDeclined,
                    "Request declined",
                    $"Your request for {request.RequestedDate:dd/MM/yyyy} was declined.{detail}",
                    "~/Student/Requests/MyRequests.aspx").ConfigureAwait(False)
            End If

            Return ServiceResult.Ok("Request declined.")
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of Booking))) _
            Implements IBookingService.GetForStudentAsync

            Dim rows As IList(Of Booking) = Await _bookings.GetForStudentAsync(studentId).ConfigureAwait(False)
            Return ServiceResult(Of IList(Of Booking)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of Booking))) _
            Implements IBookingService.GetForTutorAsync

            Dim rows As IList(Of Booking) = Await _bookings.GetForTutorAsync(tutorId).ConfigureAwait(False)
            Return ServiceResult(Of IList(Of Booking)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-24. Ownership is decided from the signed-in PersonId, not from anything the
        ''' page passed in. A booking belonging to someone else returns the same message as
        ''' one that does not exist, so an id cannot be probed for existence.
        ''' </remarks>
        Public Async Function GetForViewerAsync(bookingId As Integer,
                                                actingPersonId As Integer) As Task(Of ServiceResult(Of Booking)) _
            Implements IBookingService.GetForViewerAsync

            Dim booking As Booking = Await _bookings.GetByIdAsync(bookingId).ConfigureAwait(False)
            If booking Is Nothing Then
                Return ServiceResult(Of Booking).Fail("That booking is not available.")
            End If

            Dim studentPersonId As Integer =
                Await _tutors.GetPersonIdForStudentAsync(booking.StudentId).ConfigureAwait(False)

            Dim tutorProfile As TutorProfile =
                Await _users.GetTutorByPersonIdAsync(actingPersonId).ConfigureAwait(False)

            Dim isOwner As Boolean =
                studentPersonId = actingPersonId OrElse
                (tutorProfile IsNot Nothing AndAlso tutorProfile.TutorId = booking.TutorId)

            ' An administrator too. Manage Bookings already lists every booking on the
            ' platform, so refusing to open one was inconsistent - and it silently broke both
            ' administrator cancellation and rescheduling, which route through this check.
            ' The role is read from the database by PersonId, never taken from the request.
            If Not isOwner Then
                Dim actor As Person = Await _users.GetByIdAsync(actingPersonId).ConfigureAwait(False)
                isOwner = actor IsNot Nothing AndAlso actor.Role = UserRole.Administrator
            End If

            If Not isOwner Then
                Return ServiceResult(Of Booking).Fail("That booking is not available.")
            End If

            Return ServiceResult(Of Booking).Ok(booking)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-23. Both the ownership test and the "end time has passed" test are inside the
        ''' UPDATE, so an early or foreign attempt changes nothing rather than relying on a
        ''' check that could be raced.
        ''' </remarks>
        Public Async Function MarkCompletedAsync(tutorId As Integer,
                                                 bookingId As Integer) As Task(Of ServiceResult) _
            Implements IBookingService.MarkCompletedAsync

            Dim booking As Booking = Await _bookings.GetByIdAsync(bookingId).ConfigureAwait(False)

            Dim completed As Boolean =
                Await _bookings.MarkCompletedAsync(bookingId, tutorId).ConfigureAwait(False)

            If Not completed Then
                If booking IsNot Nothing AndAlso Not booking.HasEnded Then
                    Return ServiceResult.Fail(
                        "This session cannot be completed yet - it has not finished.")
                End If

                Return ServiceResult.Fail(
                    "This session could not be completed. It may already be completed or cancelled.")
            End If

            If booking IsNot Nothing Then
                Await NotifyStudentAsync(
                    booking.StudentId,
                    NotificationType.BookingCompleted,
                    "Session completed",
                    $"Your session on {booking.BookingDate:dd/MM/yyyy} is marked completed. " &
                    "You can now leave a review.",
                    "~/Student/Bookings/MyBookings.aspx").ConfigureAwait(False)
            End If

            Return ServiceResult.Ok("Session marked completed.")
        End Function

        ''' <inheritdoc/>
        ''' <summary>
        ''' Cancels a scheduled session (Part 12.3).
        ''' </summary>
        ''' <remarks>
        ''' <para>
        ''' The student, the tutor or an administrator may all cancel. Whoever does it, the
        ''' notification goes to <b>the other party</b> - the earlier version notified the
        ''' student whoever had acted, so a student who cancelled their own session was told
        ''' about it and the tutor was told nothing.
        ''' </para>
        ''' <para>
        ''' This is not BR-13. BR-13 governs a <i>SessionRequest</i>, where a student may
        ''' withdraw only while it is still Pending. This cancels the <i>Booking</i> that a
        ''' confirmed request produced - a different object and a different operation, with
        ''' a reason recorded against it.
        ''' </para>
        ''' </remarks>
        Public Async Function CancelAsync(actingPersonId As Integer,
                                          bookingId As Integer,
                                          reasonCategory As ScheduleChangeReason,
                                          cancelReason As String) As Task(Of ServiceResult) _
            Implements IBookingService.CancelAsync

            If Not String.IsNullOrEmpty(cancelReason) AndAlso
               cancelReason.Length > ScheduleChangeReasons.MaximumNoteLength Then
                Return ServiceResult.Fail(
                    $"Keep the note to {ScheduleChangeReasons.MaximumNoteLength} characters or fewer.")
            End If

            ' Ownership first: the same check the detail screen uses, so a booking belonging
            ' to someone else gives the same answer as one that does not exist.
            Dim viewable As ServiceResult(Of Booking) =
                Await GetForViewerAsync(bookingId, actingPersonId).ConfigureAwait(False)

            If Not viewable.Success Then
                Return ServiceResult.Fail(viewable.Message)
            End If

            Dim booking As Booking = viewable.Data

            If booking.Status <> BookingStatus.Scheduled Then
                Return ServiceResult.Fail("Only a scheduled session can be cancelled.")
            End If

            Dim actorRole As String = Await DescribeActorAsync(actingPersonId, booking).ConfigureAwait(False)

            Dim entry As New AuditEntry With {
                .ActorPersonId = actingPersonId,
                .ActorRole = actorRole,
                .ActionType = AuditAction.BookingCancelled,
                .EntityType = AuditService.BookingEntity,
                .EntityId = bookingId,
                .PreviousState = $"Scheduled {booking.BookingDate:dd/MM/yyyy} {booking.TimeRange}",
                .NewState = "Cancelled",
                .Reason = ScheduleChangeReasons.Describe(reasonCategory),
                .ResponseNote = cancelReason
            }

            Dim cancelled As Boolean = Await _bookings.CancelAsync(
                bookingId, actingPersonId, reasonCategory, cancelReason, entry).ConfigureAwait(False)

            If Not cancelled Then
                ' Lost a race with another cancellation or a completion.
                Return ServiceResult.Fail("Only a scheduled session can be cancelled.")
            End If

            Await NotifyCounterpartyAsync(actingPersonId, booking, reasonCategory, cancelReason).ConfigureAwait(False)

            Return ServiceResult.Ok("Session cancelled and the slot released.")
        End Function

        ''' <summary>
        ''' An administrator moves a scheduled session (Part 12.7).
        ''' </summary>
        ''' <remarks>
        ''' The designed path for moving a session is a student proposing and a tutor
        ''' agreeing. This is the override for when that is not possible - a room change, a
        ''' clash neither party can resolve - and it is deliberately audited and announced to
        ''' both of them, because neither one asked for it.
        ''' </remarks>
        Public Async Function RescheduleAsync(actingPersonId As Integer,
                                              bookingId As Integer,
                                              newDate As Date,
                                              newStart As TimeSpan,
                                              newEnd As TimeSpan,
                                              reason As String) As Task(Of ServiceResult) _
            Implements IBookingService.RescheduleAsync

            If newEnd <= newStart Then
                Return ServiceResult.Fail("The end time must be later than the start time.")
            End If

            If newDate.Add(newStart) <= Date.Now Then
                Return ServiceResult.Fail("Choose a date and time in the future.")
            End If

            If String.IsNullOrWhiteSpace(reason) Then
                Return ServiceResult.Fail("Give a reason for moving this session.")
            End If

            If reason.Length > ScheduleChangeReasons.MaximumNoteLength Then
                Return ServiceResult.Fail(
                    $"Keep the reason to {ScheduleChangeReasons.MaximumNoteLength} characters or fewer.")
            End If

            Dim viewable As ServiceResult(Of Booking) =
                Await GetForViewerAsync(bookingId, actingPersonId).ConfigureAwait(False)

            If Not viewable.Success Then
                Return ServiceResult.Fail(viewable.Message)
            End If

            Dim booking As Booking = viewable.Data

            Dim entry As New AuditEntry With {
                .ActorPersonId = actingPersonId,
                .ActorRole = UserRole.Administrator.ToString(),
                .ActionType = AuditAction.BookingRescheduled,
                .EntityType = AuditService.BookingEntity,
                .EntityId = bookingId,
                .PreviousState = $"{booking.BookingDate:dd/MM/yyyy} {booking.TimeRange}",
                .NewState = $"{newDate:dd/MM/yyyy} {newStart:hh\:mm} - {newEnd:hh\:mm}",
                .Reason = reason
            }

            Dim outcome As PostponeOutcome = Await _bookings.RescheduleAsync(
                bookingId, newDate, newStart, newEnd, entry).ConfigureAwait(False)

            Select Case outcome
                Case PostponeOutcome.BookingNotScheduled
                    Return ServiceResult.Fail("Only a scheduled session can be moved.")
                Case PostponeOutcome.TutorConflict
                    Return ServiceResult.Fail("The tutor already has a session across that time.")
                Case PostponeOutcome.StudentConflict
                    Return ServiceResult.Fail("The student already has a session across that time.")
            End Select

            Await NotifyBothAsync(
                booking, NotificationType.BookingRescheduled, "Session moved",
                $"The {booking.SubjectName} session has been moved from " &
                $"{booking.BookingDate:dd/MM/yyyy} ({booking.TimeRange}) to " &
                $"{newDate:dd/MM/yyyy} ({newStart:hh\:mm} - {newEnd:hh\:mm}). Reason: {reason}").ConfigureAwait(False)

            Return ServiceResult.Ok("Session moved and both people notified.")
        End Function

        ''' <summary>Tells the student and the tutor, whoever acted.</summary>
        Private Async Function NotifyBothAsync(booking As Booking,
                                               kind As NotificationType,
                                               title As String,
                                               detail As String) As Task
            Dim studentPersonId As Integer =
                Await _tutors.GetPersonIdForStudentAsync(booking.StudentId).ConfigureAwait(False)
            Dim tutorPersonId As Integer =
                Await _tutors.GetPersonIdForTutorAsync(booking.TutorId).ConfigureAwait(False)

            If studentPersonId > 0 Then
                Await _notifications.NotifyAsync(studentPersonId, kind, title, detail,
                                                 "~/Student/Bookings/MyBookings.aspx").ConfigureAwait(False)
            End If

            If tutorPersonId > 0 Then
                Await _notifications.NotifyAsync(tutorPersonId, kind, title, detail,
                                                 "~/Tutor/Sessions/MySessions.aspx").ConfigureAwait(False)
            End If
        End Function

        ''' <summary>
        ''' Tells whichever party did not press the button.
        ''' </summary>
        Private Async Function NotifyCounterpartyAsync(actingPersonId As Integer,
                                                       booking As Booking,
                                                       reasonCategory As ScheduleChangeReason,
                                                       note As String) As Task
            Dim studentPersonId As Integer =
                Await _tutors.GetPersonIdForStudentAsync(booking.StudentId).ConfigureAwait(False)
            Dim tutorPersonId As Integer =
                Await _tutors.GetPersonIdForTutorAsync(booking.TutorId).ConfigureAwait(False)

            Dim detail As String =
                $"The {booking.SubjectName} session on {booking.BookingDate:dd/MM/yyyy} " &
                $"({booking.TimeRange}) was cancelled. Reason: {ScheduleChangeReasons.Describe(reasonCategory)}." &
                If(String.IsNullOrWhiteSpace(note), String.Empty, $" {note}")

            ' An administrator is neither party, so both are told.
            If actingPersonId <> studentPersonId AndAlso studentPersonId > 0 Then
                Await _notifications.NotifyAsync(
                    studentPersonId, NotificationType.BookingCancelled, "Session cancelled",
                    detail, "~/Student/Bookings/MyBookings.aspx").ConfigureAwait(False)
            End If

            If actingPersonId <> tutorPersonId AndAlso tutorPersonId > 0 Then
                Await _notifications.NotifyAsync(
                    tutorPersonId, NotificationType.BookingCancelled, "Session cancelled",
                    detail, "~/Tutor/Sessions/MySessions.aspx").ConfigureAwait(False)
            End If
        End Function

        ''' <summary>The acting person's role relative to this booking, for the audit record.</summary>
        Private Async Function DescribeActorAsync(actingPersonId As Integer, booking As Booking) As Task(Of String)
            Dim studentPersonId As Integer =
                Await _tutors.GetPersonIdForStudentAsync(booking.StudentId).ConfigureAwait(False)

            If actingPersonId = studentPersonId Then Return UserRole.Student.ToString()

            Dim tutorPersonId As Integer =
                Await _tutors.GetPersonIdForTutorAsync(booking.TutorId).ConfigureAwait(False)

            If actingPersonId = tutorPersonId Then Return UserRole.Tutor.ToString()

            Return UserRole.Administrator.ToString()
        End Function

        Private Async Function NotifyStudentAsync(studentId As Integer,
                                                  kind As NotificationType,
                                                  title As String,
                                                  message As String,
                                                  linkUrl As String) As Task
            Dim personId As Integer =
                Await _tutors.GetPersonIdForStudentAsync(studentId).ConfigureAwait(False)

            If personId > 0 Then
                Await _notifications.NotifyAsync(personId, kind, title, message, linkUrl).ConfigureAwait(False)
            End If
        End Function

    End Class

End Namespace

