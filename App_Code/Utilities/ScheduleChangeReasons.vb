Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports TutorConnect.Enums

Namespace Utilities

    ''' <summary>
    ''' The reason categories offered for cancelling or postponing a session, and their
    ''' display text.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' One list, used by the dropdowns and by the server-side validation. A page that built
    ''' its own list could offer a value the service then rejected, or - worse - the service
    ''' could accept a value no page offers, which is what a tampered post would send.
    ''' </para>
    ''' <para>
    ''' The stored value is the enum member name, matching CK_Booking_CancelCategory and
    ''' CK_Postponement_Category. The display text is separate so the wording can change
    ''' without a data migration.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class ScheduleChangeReasons

        Private Sub New()
        End Sub

        ''' <summary>Display text for each category, in the order they are offered.</summary>
        Private Shared ReadOnly Labels As New Dictionary(Of ScheduleChangeReason, String) From {
            {ScheduleChangeReason.TimetableClash, "Timetable or lecture clash"},
            {ScheduleChangeReason.Medical, "Medical issue"},
            {ScheduleChangeReason.ExamPreparation, "Exam preparation"},
            {ScheduleChangeReason.AssignmentDeadline, "Assignment deadline"},
            {ScheduleChangeReason.Other, "Other valid reason"}
        }

        ''' <summary>The maximum length of the optional note, matching NVARCHAR(200).</summary>
        Public Const MaximumNoteLength As Integer = 200

        ''' <summary>The categories in display order, for binding to a dropdown.</summary>
        Public Shared Function All() As IList(Of KeyValuePair(Of String, String))
            Dim items As New List(Of KeyValuePair(Of String, String))()

            For Each pair In Labels
                items.Add(New KeyValuePair(Of String, String)(pair.Key.ToString(), pair.Value))
            Next

            Return items
        End Function

        ''' <summary>Display text for one category.</summary>
        Public Shared Function Describe(reason As ScheduleChangeReason) As String
            Dim text As String = Nothing
            Return If(Labels.TryGetValue(reason, text), text, reason.ToString())
        End Function

        ''' <summary>Display text for a category that may be absent.</summary>
        Public Shared Function Describe(reason As ScheduleChangeReason?) As String
            Return If(reason.HasValue, Describe(reason.Value), String.Empty)
        End Function

        ''' <summary>
        ''' Converts a submitted value into a category, refusing anything not on the list.
        ''' </summary>
        ''' <remarks>
        ''' Returns False rather than throwing: an unrecognised value here is a tampered or
        ''' empty form field, which is a message to the user, not a defect to surface. That
        ''' is the opposite of EnumHelper.Parse, which reads values the database has already
        ''' constrained and so treats a bad one as a code/schema divergence.
        ''' </remarks>
        Public Shared Function TryParse(value As String, ByRef reason As ScheduleChangeReason) As Boolean
            reason = ScheduleChangeReason.Other

            If String.IsNullOrWhiteSpace(value) Then Return False

            Dim parsed As ScheduleChangeReason
            If Not [Enum].TryParse(value.Trim(), False, parsed) Then Return False
            If Not Labels.ContainsKey(parsed) Then Return False

            reason = parsed
            Return True
        End Function

    End Class

End Namespace
