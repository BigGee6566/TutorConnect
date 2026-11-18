Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Validation

Namespace Services

    ''' <summary>
    ''' Session requests, from creation to cancellation.
    ''' </summary>
    ''' <remarks>
    ''' The rule checks in <see cref="CreateAsync"/> run in a fixed order and stop at the
    ''' first failure, so the student is told the most specific reason rather than a
    ''' generic refusal. Every one of them is server-side: the same checks hold with
    ''' JavaScript disabled and against a hand-crafted POST.
    ''' </remarks>
    Public Class RequestService
        Implements IRequestService

        Private ReadOnly _requests As RequestRepository
        Private ReadOnly _bookings As BookingRepository
        Private ReadOnly _tutors As TutorRepository
        Private ReadOnly _notifications As INotificationService

        Private ReadOnly _subjects As SubjectRepository

        Public Sub New()
            Me.New(New RequestRepository(), New BookingRepository(), New TutorRepository(), New NotificationService())
        End Sub

        Public Sub New(requests As RequestRepository,
                       bookings As BookingRepository,
                       tutors As TutorRepository,
                       notifications As INotificationService)
            _requests = requests
            _bookings = bookings
            _tutors = tutors
            _notifications = notifications
            _subjects = New SubjectRepository()
        End Sub

        ''' <inheritdoc/>
        Public Async Function CreateAsync(studentId As Integer,
                                          tutorSubjectId As Integer,
                                          availabilityId As Integer,
                                          requestedDate As Date,
                                          startTime As TimeSpan,
                                          endTime As TimeSpan,
                                          studentMessage As String) As Task(Of ServiceResult(Of Integer)) _
            Implements IRequestService.CreateAsync

            If studentId <= 0 Then
                Return ServiceResult(Of Integer).Fail("You are not signed in as a student.")
            End If

            ' BR-08: the submission itself must be well formed. Also covers BR-06 and an
            ' early BR-11.
            Dim validation As ValidationResult = RequestValidator.Validate(
                tutorSubjectId, availabilityId, requestedDate, startTime, endTime, studentMessage)

            If Not validation.IsValid Then
                Return ServiceResult(Of Integer).Fail(validation.ToSummary())
            End If

            ' Part 12.6: an inactive subject cannot be used for NEW work. Existing requests
            ' and bookings against it are left alone - deactivation is not retroactive, and
            ' cancelling agreed sessions because an administrator tidied a list would be a
            ' surprising thing for the system to do on its own.
            If Not Await _subjects.IsTutorSubjectActiveAsync(tutorSubjectId).ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail(
                    "That subject is no longer offered, so new sessions cannot be requested for it.")
            End If

            ' BR-09 + first half of BR-10: the slot must exist, belong to this TutorSubject,
            ' still be open, and actually contain the requested window. One query, so the
            ' answer cannot be assembled from inconsistent reads.
            Dim slotUsable As Boolean = Await _requests.SlotIsOpenAndContainsAsync(
                availabilityId, tutorSubjectId, requestedDate, startTime, endTime).ConfigureAwait(False)

            If Not slotUsable Then
                Return ServiceResult(Of Integer).Fail(
                    "That time is not inside one of this tutor's available slots. " &
                    "Choose one of the listed times.")
            End If

            ' BR-10: a Scheduled booking may already hold the slot even though IsBooked has
            ' not been set yet.
            If Await _requests.SlotHasScheduledBookingAsync(availabilityId).ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail("That slot has just been booked by someone else.")
            End If

            ' BR-12: the student's own diary must be free. Re-checked inside the
            ' serialisable transaction when the tutor accepts; this is the early warning.
            If Await _bookings.StudentHasOverlapAsync(studentId, requestedDate, startTime, endTime).ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail(
                    "You already have a booking that overlaps this time.")
            End If

            Dim requestId As Integer = Await _requests.CreateAsync(New SessionRequest With {
                .StudentId = studentId,
                .TutorSubjectId = tutorSubjectId,
                .AvailabilityId = availabilityId,
                .RequestedDate = requestedDate,
                .StartTime = startTime,
                .EndTime = endTime,
                .StudentMessage = studentMessage
            }).ConfigureAwait(False)

            ' BR-27. Advisory: the request is already saved, so a failure here is not
            ' allowed to turn a successful request into an error.
            Dim tutorPersonId As Integer =
                Await _tutors.GetPersonIdForTutorSubjectAsync(tutorSubjectId).ConfigureAwait(False)

            If tutorPersonId > 0 Then
                Await _notifications.NotifyAsync(
                    tutorPersonId,
                    NotificationType.RequestReceived,
                    "New session request",
                    $"A student has requested a session on {requestedDate:dd/MM/yyyy} " &
                    $"from {startTime:hh\:mm} to {endTime:hh\:mm}.",
                    "~/Tutor/Requests/StudentRequests.aspx").ConfigureAwait(False)
            End If

            Return ServiceResult(Of Integer).Ok(requestId, "Your request has been sent to the tutor.")
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of SessionRequest))) _
            Implements IRequestService.GetForStudentAsync

            Dim rows As IList(Of SessionRequest) =
                Await _requests.GetForStudentAsync(studentId).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of SessionRequest)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of SessionRequest))) _
            Implements IRequestService.GetForTutorAsync

            Dim rows As IList(Of SessionRequest) =
                Await _requests.GetForTutorAsync(tutorId).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of SessionRequest)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-13. Both conditions - Pending, and owned by this student - are in the UPDATE's
        ''' WHERE clause, so a request that changed status a moment ago simply updates
        ''' nothing instead of being cancelled out from under the tutor.
        ''' </remarks>
        Public Async Function CancelAsync(studentId As Integer,
                                          requestId As Integer) As Task(Of ServiceResult) _
            Implements IRequestService.CancelAsync

            Dim cancelled As Boolean =
                Await _requests.CancelByStudentAsync(requestId, studentId).ConfigureAwait(False)

            If Not cancelled Then
                Return ServiceResult.Fail(
                    "That request can no longer be cancelled. Only a pending request can be withdrawn.")
            End If

            Dim request As SessionRequest = Await _requests.GetByIdAsync(requestId).ConfigureAwait(False)

            If request IsNot Nothing Then
                Dim tutorPersonId As Integer =
                    Await _tutors.GetPersonIdForTutorSubjectAsync(request.TutorSubjectId).ConfigureAwait(False)

                If tutorPersonId > 0 Then
                    Await _notifications.NotifyAsync(
                        tutorPersonId,
                        NotificationType.RequestCancelled,
                        "Request withdrawn",
                        $"A student has withdrawn their request for {request.RequestedDate:dd/MM/yyyy}.",
                        "~/Tutor/Requests/StudentRequests.aspx").ConfigureAwait(False)
                End If
            End If

            Return ServiceResult.Ok("Your request has been cancelled.")
        End Function

        ''' <inheritdoc/>
        Public Async Function CountPendingForTutorAsync(tutorId As Integer) As Task(Of Integer) _
            Implements IRequestService.CountPendingForTutorAsync

            Return Await _requests.CountPendingForTutorAsync(tutorId).ConfigureAwait(False)
        End Function

    End Class

End Namespace
