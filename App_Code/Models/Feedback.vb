Option Strict On
Option Explicit On

Imports TutorConnect.Enums

Namespace Models

    ''' <summary>
    ''' A student's rating and comment against one Completed booking.
    ''' </summary>
    ''' <remarks>
    ''' There is deliberately no StudentId property, because the table has no such column.
    ''' The reviewing student is derived through Booking -> SessionRequest -> Student, so a
    ''' review cannot be attributed to someone who did not own the booking. StudentName
    ''' below is a display value filled in by that join, never something the caller sets.
    ''' </remarks>
    Public Class Review

        Public Property ReviewId As Integer
        Public Property BookingId As Integer

        ''' <summary>1 to 5. Enforced by CK_Review_Rating as well as by the validator.</summary>
        Public Property Rating As Byte

        ''' <summary>Optional, 500 characters maximum. Highest-risk field for XSS - always encode on output.</summary>
        Public Property Comment As String

        Public Property CreatedAt As Date

        ''' <summary>
        ''' Hidden by an administrator. Excluded from the tutor's list and from every
        ''' rating average, but still shown to the student who wrote it - concealing
        ''' someone's own words from them, without explanation, would not be honest.
        ''' </summary>
        Public Property IsHidden As Boolean

        Public Property HiddenReason As String
        Public Property HiddenAt As Date?

        Public Property StudentName As String
        Public Property TutorName As String
        Public Property SubjectName As String
        Public Property StudyLevelName As String
        Public Property SessionDate As Date
        Public Property StartTime As TimeSpan
        Public Property EndTime As TimeSpan

        ''' <summary>The label shown beside the stars, e.g. "5 (Excellent)".</summary>
        Public ReadOnly Property RatingLabel As String
            Get
                Select Case Rating
                    Case 1 : Return "1 (Poor)"
                    Case 2 : Return "2 (Fair)"
                    Case 3 : Return "3 (Good)"
                    Case 4 : Return "4 (Very good)"
                    Case 5 : Return "5 (Excellent)"
                    Case Else : Return Rating.ToString()
                End Select
            End Get
        End Property

    End Class

    ''' <summary>A tutor's aggregate rating, shown on their profile and in search.</summary>
    Public Class TutorRatingSummary

        Public Property TutorId As Integer

        ''' <summary>Null when the tutor has no reviews. Displayed to one decimal place.</summary>
        Public Property AverageRating As Decimal?

        Public Property ReviewCount As Integer

    End Class

    ''' <summary>The six figures on the administrator dashboard.</summary>
    Public Class PlatformStatistics

        Public Property TotalStudents As Integer
        Public Property TotalTutors As Integer
        Public Property TutorsAwaitingApproval As Integer
        Public Property RequestsToday As Integer
        Public Property BookingsThisWeek As Integer

        ''' <summary>Null until at least one review exists. Displayed to one decimal.</summary>
        Public Property AverageRating As Decimal?

    End Class

    ''' <summary>An in-app notification (BR-27). No email or SMS delivery is in scope.</summary>
    Public Class Notification

        Public Property NotificationId As Integer
        Public Property PersonId As Integer
        Public Property Title As String
        Public Property Message As String
        ' Fully qualified: an unqualified NotificationType here would bind to the
        ' property being declared rather than to the enum.
        Public Property NotificationType As Enums.NotificationType
        Public Property LinkUrl As String
        Public Property IsRead As Boolean
        Public Property CreatedAt As Date

    End Class

End Namespace
