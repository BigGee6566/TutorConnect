Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Models
Imports TutorConnect.Validation

Namespace Services

    ''' <summary>
    ''' Published availability (BR-06, BR-07, BR-20, BR-22).
    ''' </summary>
    Public Class AvailabilityService
        Implements IAvailabilityService

        Private ReadOnly _tutors As TutorRepository
        Private ReadOnly _subjects As SubjectRepository

        Public Sub New()
            Me.New(New TutorRepository(), New SubjectRepository())
        End Sub

        Public Sub New(tutors As TutorRepository, subjects As SubjectRepository)
            _tutors = tutors
            _subjects = subjects
        End Sub

        ''' <inheritdoc/>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of TutorAvailability))) _
            Implements IAvailabilityService.GetForTutorAsync

            Dim rows As IList(Of TutorAvailability) =
                Await _tutors.GetAvailabilityForTutorAsync(tutorId).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of TutorAvailability)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function AddAsync(tutorId As Integer,
                                       tutorSubjectId As Integer,
                                       availabilityDate As Date,
                                       startTime As TimeSpan,
                                       endTime As TimeSpan) As Task(Of ServiceResult(Of Integer)) _
            Implements IAvailabilityService.AddAsync

            If tutorId <= 0 Then
                Return ServiceResult(Of Integer).Fail("You are not signed in as a tutor.")
            End If

            If tutorSubjectId <= 0 Then
                Return ServiceResult(Of Integer).Fail("Choose which subject this slot is for.")
            End If

            ' BR-06 and the maximum session length.
            Dim validation As ValidationResult = BookingValidator.ValidateTimes(startTime, endTime)

            ' BR-07: availability cannot be created in the past.
            For Each item In BookingValidator.ValidateNotInPast(availabilityDate, startTime, "AvailabilityDate").Errors
                validation.AddError(item.FieldName, item.Message)
            Next

            For Each item In BookingValidator.ValidateWithinPlanningHorizon(availabilityDate, "AvailabilityDate").Errors
                validation.AddError(item.FieldName, item.Message)
            Next

            If Not validation.IsValid Then
                Return ServiceResult(Of Integer).Fail(validation.ToSummary())
            End If

            ' Part 12.6: no new availability against a subject that is no longer offered.
            ' Slots already published stay - deactivation is not retroactive.
            If Not Await _subjects.IsTutorSubjectActiveAsync(tutorSubjectId).ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail(
                    "That subject is no longer offered, so new slots cannot be published for it.")
            End If

            ' The subject must actually be one this tutor teaches. Without this a tampered
            ' form could publish availability against someone else's TutorSubject.
            Dim owned As IList(Of TutorSubject) =
                Await _subjects.GetTutorSubjectsAsync(tutorId).ConfigureAwait(False)

            If Not owned.Any(Function(ts) ts.TutorSubjectId = tutorSubjectId) Then
                Return ServiceResult(Of Integer).Fail("That subject is not on your list.")
            End If

            ' BR-20: no overlap with this tutor's own existing slots.
            If Await _tutors.HasOverlappingAvailabilityAsync(
                tutorId, availabilityDate, startTime, endTime).ConfigureAwait(False) Then

                Return ServiceResult(Of Integer).Fail(
                    "That time overlaps a slot you have already published.")
            End If

            Dim id As Integer = Await _tutors.AddAvailabilityAsync(
                tutorSubjectId, availabilityDate, startTime, endTime).ConfigureAwait(False)

            Return ServiceResult(Of Integer).Ok(id, "Availability added.")
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-22. A slot backing a scheduled booking is refused with the reason, rather
        ''' than deleted and leaving a booking pointing at nothing.
        ''' </remarks>
        Public Async Function DeleteAsync(tutorId As Integer,
                                          availabilityId As Integer) As Task(Of ServiceResult) _
            Implements IAvailabilityService.DeleteAsync

            If Await _tutors.AvailabilityBacksScheduledBookingAsync(availabilityId).ConfigureAwait(False) Then
                Return ServiceResult.Fail(
                    "This slot cannot be deleted because a scheduled session depends on it. " &
                    "Cancel the session first.")
            End If

            ' Scoped by tutorId in the DELETE, so a tampered id removes nothing.
            Dim deleted As Boolean =
                Await _tutors.DeleteAvailabilityAsync(availabilityId, tutorId).ConfigureAwait(False)

            Return If(deleted,
                      ServiceResult.Ok("Availability removed."),
                      ServiceResult.Fail("That slot is not one of yours."))
        End Function

    End Class

End Namespace
