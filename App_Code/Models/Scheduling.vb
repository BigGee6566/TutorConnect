Option Strict On
Option Explicit On

Imports TutorConnect.Enums

Namespace Models

    ''' <summary>A date and time range a tutor has published for one TutorSubject.</summary>
    Public Class TutorAvailability

        Public Property AvailabilityId As Integer
        Public Property TutorSubjectId As Integer
        Public Property AvailabilityDate As Date

        ''' <summary>
        ''' TIME(0) maps to TimeSpan, not Date. Treating it as a Date invents an
        ''' arbitrary day component and breaks every comparison.
        ''' </summary>
        Public Property StartTime As TimeSpan

        Public Property EndTime As TimeSpan

        ''' <summary>Set when a booking consumes the slot; cleared if that booking is cancelled.</summary>
        Public Property IsBooked As Boolean

        Public Property CreatedAt As Date

        Public Property SubjectName As String
        Public Property StudyLevelName As String

        ''' <summary>Renders as "10:00 - 12:00" for display.</summary>
        Public ReadOnly Property TimeRange As String
            Get
                Return $"{StartTime:hh\:mm} - {EndTime:hh\:mm}"
            End Get
        End Property

    End Class

    ''' <summary>
    ''' A student's intent to book a slot.
    ''' </summary>
    ''' <remarks>
    ''' The requested date and times are stored on the request itself, not read through
    ''' AvailabilityId. That denormalisation is deliberate: a tutor may later delete or
    ''' edit the availability row, and the request must still show what was asked for.
    ''' </remarks>
    Public Class SessionRequest

        Public Property RequestId As Integer
        Public Property StudentId As Integer
        Public Property TutorSubjectId As Integer
        Public Property AvailabilityId As Integer
        Public Property RequestedDate As Date
        Public Property StartTime As TimeSpan
        Public Property EndTime As TimeSpan
        Public Property Status As RequestStatus
        Public Property StudentMessage As String

        ''' <summary>The "Date Requested" column in the UI - when the request was made.</summary>
        Public Property CreatedAt As Date

        Public Property RespondedAt As Date?
        Public Property DeclineReason As String

        Public Property StudentName As String
        Public Property TutorName As String
        Public Property SubjectName As String
        Public Property StudyLevelName As String

        Public ReadOnly Property TimeRange As String
            Get
                Return $"{StartTime:hh\:mm} - {EndTime:hh\:mm}"
            End Get
        End Property

    End Class

    ''' <summary>The confirmed slot, created when a tutor accepts a request.</summary>
    Public Class Booking

        Public Property BookingId As Integer
        Public Property RequestId As Integer
        Public Property AvailabilityId As Integer
        Public Property BookingDate As Date
        Public Property StartTime As TimeSpan
        Public Property EndTime As TimeSpan
        Public Property Status As BookingStatus
        Public Property CompletedAt As Date?
        Public Property CancelledAt As Date?
        Public Property CancelledByPersonId As Integer?
        Public Property CancelledByName As String
        Public Property CancelReasonCategory As ScheduleChangeReason?
        Public Property CancelReason As String
        Public Property CreatedAt As Date

        Public Property StudentId As Integer
        Public Property TutorId As Integer
        Public Property StudentName As String
        Public Property TutorName As String
        Public Property SubjectName As String
        Public Property StudyLevelName As String

        ''' <summary>The tutor's rate, shown on the session detail (Part 12.2).</summary>
        Public Property RatePerSession As Decimal

        ''' <summary>True when a review already exists for this booking (BR-17).</summary>
        Public Property HasReview As Boolean

        Public ReadOnly Property TimeRange As String
            Get
                Return $"{StartTime:hh\:mm} - {EndTime:hh\:mm}"
            End Get
        End Property

        ''' <summary>
        ''' BR-23. A booking may only be marked Completed once its end time has passed.
        ''' Compared in local (SAST) time because the slot itself is a local wall-clock time.
        ''' </summary>
        Public ReadOnly Property HasEnded As Boolean
            Get
                Return BookingDate.Add(EndTime) <= Date.Now
            End Get
        End Property

    End Class

    ''' <summary>
    ''' Evidence that a booking was delivered. One row, written only when the tutor marks
    ''' the booking Completed (D-03 Option A). It carries no status of its own - the
    ''' booking's status is the single source of truth.
    ''' </summary>
    Public Class TutoringSession

        Public Property SessionId As Integer
        Public Property BookingId As Integer
        Public Property SessionDate As Date
        Public Property TutorNotes As String
        Public Property CreatedAt As Date

    End Class

    ''' <summary>
    ''' A student's proposal to move an agreed session, and the tutor's answer to it.
    ''' </summary>
    ''' <remarks>
    ''' Carries both the original and the proposed window. The booking is not touched while
    ''' the proposal is PendingTutorReview, so a proposal that is declined - or never
    ''' answered - leaves the agreed session exactly as it was, and an approved one still
    ''' leaves a record of what the session used to be.
    ''' </remarks>
    Public Class PostponementRequest

        Public Property PostponementId As Integer
        Public Property BookingId As Integer
        Public Property StudentId As Integer
        Public Property TutorId As Integer

        Public Property OriginalDate As Date
        Public Property OriginalStart As TimeSpan
        Public Property OriginalEnd As TimeSpan

        Public Property ProposedDate As Date
        Public Property ProposedStart As TimeSpan
        Public Property ProposedEnd As TimeSpan

        Public Property ReasonCategory As ScheduleChangeReason
        Public Property StudentNote As String

        Public Property Status As PostponementStatus
        Public Property CreatedAt As Date
        Public Property TutorResponseNote As String
        Public Property RespondedAt As Date?

        ' Filled by the joins the queues use, so a list can be rendered without a second read.
        Public Property StudentName As String
        Public Property TutorName As String
        Public Property SubjectName As String
        Public Property StudyLevelName As String

        Public ReadOnly Property OriginalRange As String
            Get
                Return $"{OriginalDate:dd/MM/yyyy} {OriginalStart:hh\:mm} - {OriginalEnd:hh\:mm}"
            End Get
        End Property

        Public ReadOnly Property ProposedRange As String
            Get
                Return $"{ProposedDate:dd/MM/yyyy} {ProposedStart:hh\:mm} - {ProposedEnd:hh\:mm}"
            End Get
        End Property

        Public ReadOnly Property IsPending As Boolean
            Get
                Return Status = PostponementStatus.PendingTutorReview
            End Get
        End Property

    End Class

    ''' <summary>
    ''' One immutable record of something that changed. Written, never updated.
    ''' </summary>
    Public Class AuditEntry

        Public Property AuditId As Integer
        Public Property ActorPersonId As Integer?
        ''' <summary>
        ''' A string, not <see cref="UserRole"/>: the column also allows 'System' for an
        ''' action no person took, and parsing that into UserRole would throw by design.
        ''' </summary>
        Public Property ActorRole As String
        Public Property ActorName As String
        Public Property ActionType As AuditAction
        Public Property EntityType As String
        Public Property EntityId As Integer
        Public Property PreviousState As String
        Public Property NewState As String
        Public Property Reason As String
        Public Property ResponseNote As String
        Public Property IpAddress As String
        Public Property CreatedAt As Date

    End Class

End Namespace
