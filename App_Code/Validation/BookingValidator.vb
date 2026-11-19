Option Strict On
Option Explicit On

Namespace Validation

    ''' <summary>
    ''' Time-window rules shared by availability, requests and bookings.
    ''' </summary>
    ''' <remarks>
    ''' No database and no HTTP here, so these rules are directly unit-testable. Anything
    ''' needing to read other rows - overlaps, availability containment - belongs in a
    ''' service, because it cannot be decided from the submitted values alone.
    ''' </remarks>
    Public NotInheritable Class BookingValidator

        ''' <summary>Longest single session the system will accept.</summary>
        Public Const MaximumHours As Double = 8

        Private Sub New()
        End Sub

        ''' <summary>BR-06. End must be strictly later than start.</summary>
        Public Shared Function ValidateTimes(startTime As TimeSpan, endTime As TimeSpan) As ValidationResult
            Dim result As New ValidationResult()

            If endTime <= startTime Then
                result.AddError("EndTime", "The end time must be later than the start time.")
                Return result
            End If

            If (endTime - startTime).TotalHours > MaximumHours Then
                result.AddError("EndTime", $"A session cannot be longer than {MaximumHours} hours.")
            End If

            Return result
        End Function

        ''' <summary>
        ''' BR-07 / BR-11. The slot must not already have started.
        ''' </summary>
        ''' <remarks>
        ''' Compared against local time: the date and time are wall-clock values in SAST,
        ''' not UTC instants. Comparing them to UtcNow would let a slot be booked up to two
        ''' hours after it began.
        ''' </remarks>
        Public Shared Function ValidateNotInPast(slotDate As Date,
                                                 startTime As TimeSpan,
                                                 fieldName As String) As ValidationResult
            Dim result As New ValidationResult()

            If slotDate.Date.Add(startTime) <= TutorConnect.Utilities.Clock.Now Then
                result.AddError(fieldName, "That time has already passed. Choose a future date and time.")
            End If

            Return result
        End Function

        ''' <summary>Rejects a date so far ahead it is almost certainly a typing error.</summary>
        Public Shared Function ValidateWithinPlanningHorizon(slotDate As Date,
                                                             fieldName As String,
                                                             Optional maximumDaysAhead As Integer = 365) As ValidationResult
            Dim result As New ValidationResult()

            If slotDate.Date > TutorConnect.Utilities.Clock.Today.AddDays(maximumDaysAhead) Then
                result.AddError(fieldName, $"Choose a date within the next {maximumDaysAhead} days.")
            End If

            Return result
        End Function

    End Class

End Namespace
