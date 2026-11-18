Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Utilities

Namespace Services

    ''' <summary>
    ''' Proposals to move an agreed session, and the tutor's answer (Part 12.4, 12.5).
    ''' </summary>
    ''' <remarks>
    ''' The rule that shapes this class: <b>a proposal never changes the booking</b>. Only the
    ''' tutor's approval does. A student who proposes a new time and hears nothing still has
    ''' the session they agreed to.
    ''' </remarks>
    Public Class PostponementService
        Implements IPostponementService

        Private ReadOnly _postponements As PostponementRepository
        Private ReadOnly _bookings As BookingRepository
        Private ReadOnly _tutors As TutorRepository
        Private ReadOnly _notifications As NotificationService
        Private ReadOnly _audit As AuditService

        Public Sub New()
            _postponements = New PostponementRepository()
            _bookings = New BookingRepository()
            _tutors = New TutorRepository()
            _notifications = New NotificationService()
            _audit = New AuditService()
        End Sub

        ''' <summary>How far ahead a proposal may reach, to keep it a postponement.</summary>
        Public Const MaximumDaysAhead As Integer = 120

        Public Const NotYoursMessage As String =
            "That session is not yours, or it is no longer scheduled."

#Region "Propose"

        ''' <inheritdoc/>
        Public Async Function ProposeAsync(actingPersonId As Integer,
                                           bookingId As Integer,
                                           proposedDate As Date,
                                           proposedStart As TimeSpan,
                                           proposedEnd As TimeSpan,
                                           reason As ScheduleChangeReason,
                                           note As String) As Task(Of ServiceResult(Of Integer)) _
            Implements IPostponementService.ProposeAsync

            If proposedEnd <= proposedStart Then
                Return ServiceResult(Of Integer).Fail("The end time must be later than the start time.")
            End If

            ' Local time, because the slot is a local wall-clock time - the same comparison
            ' the booking's own HasEnded uses.
            If proposedDate.Add(proposedStart) <= Date.Now Then
                Return ServiceResult(Of Integer).Fail("Choose a date and time in the future.")
            End If

            If proposedDate > Date.Today.AddDays(MaximumDaysAhead) Then
                Return ServiceResult(Of Integer).Fail(
                    $"Choose a date within the next {MaximumDaysAhead} days.")
            End If

            If Not String.IsNullOrEmpty(note) AndAlso
               note.Length > ScheduleChangeReasons.MaximumNoteLength Then
                Return ServiceResult(Of Integer).Fail(
                    $"Keep the note to {ScheduleChangeReasons.MaximumNoteLength} characters or fewer.")
            End If

            Dim studentId As Integer = Await _tutors.GetStudentIdForPersonAsync(actingPersonId).ConfigureAwait(False)
            If studentId <= 0 Then
                Return ServiceResult(Of Integer).Fail(NotYoursMessage)
            End If

            Dim postponementId As Integer

            Try
                ' Ownership is a condition of the INSERT, so a foreign booking writes nothing.
                postponementId = Await _postponements.CreateAsync(
                    bookingId, studentId, proposedDate, proposedStart, proposedEnd,
                    reason, note).ConfigureAwait(False)

            Catch ex As SqlException When ex.Number = 2601 OrElse ex.Number = 2627
                ' UQ_Postponement_OnePending. Two submissions raced, or the student has an
                ' open proposal already; either way the database is what decides.
                Return ServiceResult(Of Integer).Fail(
                    "You already have a postponement waiting for this session.")
            End Try

            If postponementId = 0 Then
                Return ServiceResult(Of Integer).Fail(NotYoursMessage)
            End If

            Dim created As PostponementRequest =
                Await _postponements.GetByIdAsync(postponementId).ConfigureAwait(False)

            Await NotifyTutorAsync(
                created.TutorId,
                NotificationType.BookingPostponeRequested,
                "Postponement requested",
                $"{created.StudentName} has asked to move the {created.SubjectName} session " &
                $"of {created.OriginalRange} to {created.ProposedRange}.",
                "~/Tutor/Sessions/MySessions.aspx").ConfigureAwait(False)

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Student.ToString(), AuditAction.PostponementRequested,
                AuditService.PostponementEntity, postponementId,
                created.OriginalRange, created.ProposedRange,
                ScheduleChangeReasons.Describe(reason), note).ConfigureAwait(False)

            Return ServiceResult(Of Integer).Ok(
                postponementId, "Your proposal has been sent to the tutor.")
        End Function

#End Region

#Region "Read"

        ''' <inheritdoc/>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of PostponementRequest))) _
            Implements IPostponementService.GetForTutorAsync

            Return ServiceResult(Of IList(Of PostponementRequest)).Ok(
                Await _postponements.GetForTutorAsync(tutorId).ConfigureAwait(False))
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of PostponementRequest))) _
            Implements IPostponementService.GetForStudentAsync

            Return ServiceResult(Of IList(Of PostponementRequest)).Ok(
                Await _postponements.GetForStudentAsync(studentId).ConfigureAwait(False))
        End Function

        ''' <inheritdoc/>
        Public Function GetPendingForBookingAsync(bookingId As Integer) As Task(Of PostponementRequest) _
            Implements IPostponementService.GetPendingForBookingAsync

            Return _postponements.GetPendingForBookingAsync(bookingId)
        End Function

#End Region

#Region "Answer"

        ''' <inheritdoc/>
        Public Async Function ApproveAsync(actingPersonId As Integer,
                                           postponementId As Integer,
                                           responseNote As String) As Task(Of ServiceResult) _
            Implements IPostponementService.ApproveAsync

            Dim tutorId As Integer = Await _tutors.GetTutorIdForPersonAsync(actingPersonId).ConfigureAwait(False)
            If tutorId <= 0 Then Return ServiceResult.Fail(NotYoursMessage)

            Dim proposal As PostponementRequest =
                Await _postponements.GetByIdAsync(postponementId).ConfigureAwait(False)

            ' The same message whether it does not exist or belongs to another tutor, so an
            ' identifier cannot be probed for existence.
            If proposal Is Nothing OrElse proposal.TutorId <> tutorId Then
                Return ServiceResult.Fail(NotYoursMessage)
            End If

            Dim outcome As PostponeOutcome =
                Await _postponements.ApproveAsync(postponementId, tutorId, responseNote).ConfigureAwait(False)

            Select Case outcome
                Case PostponeOutcome.NotPending
                    Return ServiceResult.Fail("That proposal has already been answered.")
                Case PostponeOutcome.BookingNotScheduled
                    Return ServiceResult.Fail("That session is no longer scheduled.")
                Case PostponeOutcome.TutorConflict
                    Return ServiceResult.Fail(
                        "You already have a session across that time. Decline this proposal " &
                        "or ask the student for a different slot.")
                Case PostponeOutcome.StudentConflict
                    Return ServiceResult.Fail(
                        "The student already has another session across that time.")
            End Select

            Await NotifyStudentAsync(
                proposal.StudentId,
                NotificationType.BookingPostponeApproved,
                "Postponement approved",
                $"{proposal.TutorName} has moved your {proposal.SubjectName} session to " &
                $"{proposal.ProposedRange}.",
                "~/Student/Bookings/MyBookings.aspx").ConfigureAwait(False)

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Tutor.ToString(), AuditAction.PostponementApproved,
                AuditService.BookingEntity, proposal.BookingId,
                proposal.OriginalRange, proposal.ProposedRange,
                ScheduleChangeReasons.Describe(proposal.ReasonCategory), responseNote).ConfigureAwait(False)

            Return ServiceResult.Ok("Session moved and the student notified.")
        End Function

        ''' <inheritdoc/>
        Public Async Function DeclineAsync(actingPersonId As Integer,
                                           postponementId As Integer,
                                           responseNote As String) As Task(Of ServiceResult) _
            Implements IPostponementService.DeclineAsync

            ' Required on decline: a refusal with no reason leaves the student with nothing
            ' to act on, and the specification asks for one.
            If String.IsNullOrWhiteSpace(responseNote) Then
                Return ServiceResult.Fail("Give the student a reason for declining.")
            End If

            If responseNote.Length > ScheduleChangeReasons.MaximumNoteLength Then
                Return ServiceResult.Fail(
                    $"Keep the reason to {ScheduleChangeReasons.MaximumNoteLength} characters or fewer.")
            End If

            Dim tutorId As Integer = Await _tutors.GetTutorIdForPersonAsync(actingPersonId).ConfigureAwait(False)
            If tutorId <= 0 Then Return ServiceResult.Fail(NotYoursMessage)

            Dim proposal As PostponementRequest =
                Await _postponements.GetByIdAsync(postponementId).ConfigureAwait(False)

            If proposal Is Nothing OrElse proposal.TutorId <> tutorId Then
                Return ServiceResult.Fail(NotYoursMessage)
            End If

            Dim declined As Boolean =
                Await _postponements.DeclineAsync(postponementId, tutorId, responseNote).ConfigureAwait(False)

            If Not declined Then
                Return ServiceResult.Fail("That proposal has already been answered.")
            End If

            Await NotifyStudentAsync(
                proposal.StudentId,
                NotificationType.BookingPostponeDeclined,
                "Postponement declined",
                $"{proposal.TutorName} kept your {proposal.SubjectName} session at " &
                $"{proposal.OriginalRange}. Reason: {responseNote}",
                "~/Student/Bookings/MyBookings.aspx").ConfigureAwait(False)

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Tutor.ToString(), AuditAction.PostponementDeclined,
                AuditService.PostponementEntity, postponementId,
                PostponementStatus.PendingTutorReview.ToString(), PostponementStatus.Declined.ToString(),
                ScheduleChangeReasons.Describe(proposal.ReasonCategory), responseNote).ConfigureAwait(False)

            Return ServiceResult.Ok("Proposal declined and the student notified.")
        End Function

        ''' <inheritdoc/>
        Public Async Function WithdrawAsync(actingPersonId As Integer,
                                            postponementId As Integer) As Task(Of ServiceResult) _
            Implements IPostponementService.WithdrawAsync

            Dim studentId As Integer = Await _tutors.GetStudentIdForPersonAsync(actingPersonId).ConfigureAwait(False)
            If studentId <= 0 Then Return ServiceResult.Fail(NotYoursMessage)

            Dim proposal As PostponementRequest =
                Await _postponements.GetByIdAsync(postponementId).ConfigureAwait(False)

            ' Same message whether it does not exist or belongs to someone else.
            If proposal Is Nothing OrElse proposal.StudentId <> studentId Then
                Return ServiceResult.Fail(NotYoursMessage)
            End If

            ' Ownership and pending state are conditions of the UPDATE, so a second click or
            ' a proposal the tutor has just answered changes nothing.
            Dim withdrawn As Boolean =
                Await _postponements.CancelAsync(postponementId, studentId).ConfigureAwait(False)

            If Not withdrawn Then
                Return ServiceResult.Fail("That request has already been answered.")
            End If

            Await NotifyTutorAsync(
                proposal.TutorId,
                NotificationType.BookingPostponeWithdrawn,
                "Postponement withdrawn",
                $"{proposal.StudentName} has taken back the request to move the " &
                $"{proposal.SubjectName} session. It stays at {proposal.OriginalRange}.",
                "~/Tutor/Sessions/MySessions.aspx").ConfigureAwait(False)

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Student.ToString(), AuditAction.PostponementWithdrawn,
                AuditService.PostponementEntity, postponementId,
                PostponementStatus.PendingTutorReview.ToString(),
                PostponementStatus.Cancelled.ToString(),
                ScheduleChangeReasons.Describe(proposal.ReasonCategory), Nothing).ConfigureAwait(False)

            Return ServiceResult.Ok("Request withdrawn. Your session is unchanged.")
        End Function

#End Region

#Region "Notification plumbing"

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

        Private Async Function NotifyTutorAsync(tutorId As Integer,
                                                kind As NotificationType,
                                                title As String,
                                                message As String,
                                                linkUrl As String) As Task
            Dim personId As Integer =
                Await _tutors.GetPersonIdForTutorAsync(tutorId).ConfigureAwait(False)

            If personId > 0 Then
                Await _notifications.NotifyAsync(personId, kind, title, message, linkUrl).ConfigureAwait(False)
            End If
        End Function

#End Region

    End Class

End Namespace
