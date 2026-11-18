Option Strict On
Option Explicit On

Namespace Validation

    ''' <summary>
    ''' Shape rules for a session request (BR-08).
    ''' </summary>
    ''' <remarks>
    ''' Only what can be judged from the submission itself. BR-09 (inside the tutor's
    ''' availability), BR-10 (slot not taken) and BR-12 (student not double-booked) all
    ''' need other rows and are enforced in RequestService.
    ''' </remarks>
    Public NotInheritable Class RequestValidator

        Public Const MaximumMessageLength As Integer = 500

        Private Sub New()
        End Sub

        ''' <summary>BR-08. A request must carry a slot, a date and a valid time window.</summary>
        Public Shared Function Validate(tutorSubjectId As Integer,
                                        availabilityId As Integer,
                                        requestedDate As Date,
                                        startTime As TimeSpan,
                                        endTime As TimeSpan,
                                        studentMessage As String) As ValidationResult
            Dim result As New ValidationResult()

            If tutorSubjectId <= 0 Then
                result.AddError("TutorSubjectId", "Choose a subject and study level.")
            End If

            If availabilityId <= 0 Then
                result.AddError("AvailabilityId", "Choose one of the tutor's available time slots.")
            End If

            For Each item In BookingValidator.ValidateTimes(startTime, endTime).Errors
                result.AddError(item.FieldName, item.Message)
            Next

            ' BR-11, checked here so the message is immediate; re-checked in the service
            ' against the slot actually loaded from the database.
            For Each item In BookingValidator.ValidateNotInPast(requestedDate, startTime, "RequestedDate").Errors
                result.AddError(item.FieldName, item.Message)
            Next

            For Each item In BookingValidator.ValidateWithinPlanningHorizon(requestedDate, "RequestedDate").Errors
                result.AddError(item.FieldName, item.Message)
            Next

            If studentMessage IsNot Nothing AndAlso studentMessage.Length > MaximumMessageLength Then
                result.AddError("StudentMessage",
                    $"Your message must be {MaximumMessageLength} characters or fewer.")
            End If

            Return result
        End Function

    End Class

End Namespace
