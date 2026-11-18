Option Strict On
Option Explicit On

Namespace Enums

    ''' <summary>
    ''' The three roles a Person may hold. Stored in Person.Role and carried in the
    ''' Forms Authentication ticket's UserData.
    ''' </summary>
    ''' <remarks>
    ''' Member names are spelled exactly as the database CHECK constraint expects, so
    ''' ToString() round-trips to a valid column value. Do not rename a member without
    ''' changing CK_Person_Role in 01_CreateSchema.sql.
    ''' </remarks>
    Public Enum UserRole
        Student
        Tutor
        Administrator
    End Enum

    ''' <summary>
    ''' Lifecycle of a student's request for a session.
    ''' Pending is the only status a student may cancel (BR-13), and the only status a
    ''' tutor may accept or decline.
    ''' </summary>
    Public Enum RequestStatus
        Pending
        Confirmed
        Declined
        Cancelled
    End Enum

    ''' <summary>
    ''' Lifecycle of an agreed booking. A booking becomes Completed only after its end
    ''' time has passed and only at the hand of its own tutor (BR-23).
    ''' </summary>
    Public Enum BookingStatus
        Scheduled
        Completed
        Cancelled
    End Enum

    ''' <summary>
    ''' Categories of in-app notification (BR-27). Notifications are in-app only; no
    ''' email or SMS delivery is in scope.
    ''' </summary>
    Public Enum NotificationType
        RequestReceived
        RequestAccepted
        RequestDeclined
        RequestCancelled
        BookingCompleted
        BookingCancelled
        ReviewReceived
        TutorApproved
        AccountDeactivated
        BookingPostponeRequested
        BookingPostponeApproved
        BookingPostponeDeclined
        BookingPostponeWithdrawn
        BookingRescheduled
    End Enum

    ''' <summary>
    ''' Why a session was cancelled or a postponement asked for.
    ''' </summary>
    ''' <remarks>
    ''' A fixed list rather than free text, so the reason can be reported on and so the
    ''' server can reject anything else. The optional note beside it carries the detail.
    ''' Names match CK_Booking_CancelCategory and CK_Postponement_Category.
    ''' </remarks>
    Public Enum ScheduleChangeReason
        TimetableClash
        Medical
        ExamPreparation
        AssignmentDeadline
        Other
    End Enum

    ''' <summary>
    ''' Where a postponement proposal has got to.
    ''' </summary>
    ''' <remarks>
    ''' The booking itself is untouched while a proposal is PendingTutorReview. Only an
    ''' Approved proposal moves it, which is what keeps a declined or unanswered proposal
    ''' from quietly changing an agreed session.
    ''' </remarks>
    Public Enum PostponementStatus
        PendingTutorReview
        Approved
        Declined
        Cancelled
    End Enum

    ''' <summary>
    ''' What an audit record describes. Stored as text in AuditLog.ActionType.
    ''' </summary>
    Public Enum AuditAction
        BookingCancelled
        BookingRescheduled
        PostponementRequested
        PostponementApproved
        PostponementDeclined
        PostponementWithdrawn
        TutorApproved
        TutorRejected
        UserActivated
        UserDeactivated
        AdministratorCreated
        SubjectCreated
        SubjectDeleted
        SubjectActivated
        SubjectDeactivated
        ReviewHidden
        ReviewRestored
    End Enum

End Namespace
