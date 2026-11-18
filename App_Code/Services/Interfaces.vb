Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Enums
Imports TutorConnect.Models

Namespace Services

    ''' <summary>
    ''' Signs users in and out, and enforces the rules that decide whether a sign-in is
    ''' allowed at all.
    ''' </summary>
    Public Interface IAuthenticationService

        ''' <summary>
        ''' Verifies a credential pair (AT-04, AT-05, AT-06).
        ''' </summary>
        ''' <remarks>
        ''' On failure the message is always the same generic text, whether the account
        ''' does not exist, the password is wrong, or the account is locked out. Revealing
        ''' which one is an account-enumeration hole. A deactivated account is the single
        ''' exception, because the user needs to know to contact the administrator.
        ''' </remarks>
        Function AuthenticateAsync(userNameOrEmail As String, password As String) As Task(Of ServiceResult(Of Person))

        ''' <summary>Creates a Person plus its Student or Tutor row in one transaction (AT-01, AT-03).</summary>
        Function RegisterAsync(account As Person,
                               password As String,
                               confirmPassword As String,
                               studentNo As String,
                               tutorDetails As TutorProfile) As Task(Of ServiceResult(Of Integer))

        ''' <summary>BR-25. Re-checks that an already-signed-in account is still active.</summary>
        Function IsStillActiveAsync(personId As Integer) As Task(Of Boolean)

        ''' <summary>True while the system has no administrator and Setup.aspx may be used.</summary>
        Function SetupRequiredAsync() As Task(Of Boolean)

        ''' <summary>
        ''' Creates the very first administrator, and only while none exists.
        ''' </summary>
        ''' <remarks>
        ''' A freshly built database has subjects and study levels but no people, and
        ''' registration deliberately refuses to create an administrator - so without this
        ''' there is no way into a new installation at all. It refuses the moment one
        ''' administrator exists, which is what stops it becoming a back door on a running
        ''' system.
        ''' </remarks>
        Function CreateFirstAdministratorAsync(account As Person,
                                               password As String,
                                               confirmPassword As String) As Task(Of ServiceResult(Of Integer))

        ''' <summary>
        ''' Loads the signed-in account for display - the name and role the sidebar shows.
        ''' </summary>
        ''' <remarks>
        ''' Exists so master pages and controls never reach into a repository directly.
        ''' The caller supplies the id from the auth ticket, never from the URL (BR-24).
        ''' </remarks>
        Function GetAccountAsync(personId As Integer) As Task(Of ServiceResult(Of Person))

        Function ChangePasswordAsync(personId As Integer,
                                     currentPassword As String,
                                     newPassword As String,
                                     confirmPassword As String) As Task(Of ServiceResult)

    End Interface

    ''' <summary>
    ''' Records what changed, who changed it and why (Part 12.7).
    ''' </summary>
    Public Interface IAuditService

        ''' <summary>
        ''' Appends one record. Pass the caller's connection and transaction where the change
        ''' being described is itself transactional, so the two stand or fall together.
        ''' </summary>
        Function RecordAsync(actorPersonId As Integer?,
                             actorRole As String,
                             action As AuditAction,
                             entityType As String,
                             entityId As Integer,
                             previousState As String,
                             newState As String,
                             reason As String,
                             responseNote As String,
                             Optional connection As System.Data.SqlClient.SqlConnection = Nothing,
                             Optional transaction As System.Data.SqlClient.SqlTransaction = Nothing) As Task

        ''' <summary>Newest first, optionally narrowed. Administrator use only.</summary>
        Function SearchAsync(actionType As String,
                             entityType As String,
                             actorPersonId As Integer) As Task(Of ServiceResult(Of IList(Of AuditEntry)))

    End Interface

    ''' <summary>
    ''' Proposals to move an agreed session, and the tutor's answer to them (Part 12.4, 12.5).
    ''' </summary>
    Public Interface IPostponementService

        ''' <summary>
        ''' A student proposes a new window. The booking is not touched; the proposal is
        ''' recorded against it and waits for the tutor.
        ''' </summary>
        Function ProposeAsync(actingPersonId As Integer,
                              bookingId As Integer,
                              proposedDate As Date,
                              proposedStart As TimeSpan,
                              proposedEnd As TimeSpan,
                              reason As ScheduleChangeReason,
                              note As String) As Task(Of ServiceResult(Of Integer))

        ''' <summary>The tutor's queue: pending first, then what has been resolved.</summary>
        Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of PostponementRequest)))

        ''' <summary>What the student has proposed, and how it was answered.</summary>
        Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of PostponementRequest)))

        ''' <summary>The proposal attached to one booking, if any is still open.</summary>
        Function GetPendingForBookingAsync(bookingId As Integer) As Task(Of PostponementRequest)

        ''' <summary>
        ''' The tutor accepts. Moves the booking and creates the slot, in one transaction,
        ''' after re-checking that neither party has since been booked into that window.
        ''' </summary>
        Function ApproveAsync(actingPersonId As Integer,
                              postponementId As Integer,
                              responseNote As String) As Task(Of ServiceResult)

        ''' <summary>The tutor refuses. The booking keeps its agreed date and time.</summary>
        Function DeclineAsync(actingPersonId As Integer,
                              postponementId As Integer,
                              responseNote As String) As Task(Of ServiceResult)

        ''' <summary>
        ''' The student changes their mind and takes back their own pending proposal.
        ''' </summary>
        ''' <remarks>
        ''' The tutor is told, because it disappears from the queue they were about to answer.
        ''' </remarks>
        Function WithdrawAsync(actingPersonId As Integer,
                               postponementId As Integer) As Task(Of ServiceResult)

    End Interface

    ''' <summary>Student profile and preferred subjects.</summary>
    Public Interface IStudentService

        Function GetProfileAsync(personId As Integer) As Task(Of ServiceResult(Of StudentProfile))

        ''' <summary>BR-24. The identity comes from the caller's ticket, never from the page.</summary>
        Function UpdateProfileAsync(actingPersonId As Integer, account As Person) As Task(Of ServiceResult)

        Function GetSubjectsAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of StudentSubject)))

        ''' <summary>BR-05. Rejects a duplicate subject + level with a clear message.</summary>
        Function AddSubjectAsync(studentId As Integer, subjectId As Integer, studyLevelId As Integer) As Task(Of ServiceResult)

        ''' <summary>
        ''' Adds a subject by name, creating it if the catalogue does not have it yet.
        ''' </summary>
        ''' <param name="actingPersonId">
        ''' Needed in addition to studentId because the audit entry for a newly created
        ''' subject records a PersonId, and a StudentId is not one.
        ''' </param>
        Function AddSubjectByNameAsync(studentId As Integer,
                                       actingPersonId As Integer,
                                       subjectName As String,
                                       studyLevelId As Integer) As Task(Of ServiceResult)

        Function RemoveSubjectAsync(studentId As Integer, studentSubjectId As Integer) As Task(Of ServiceResult)

    End Interface

    ''' <summary>Tutor profile, subjects taught, and approval state.</summary>
    Public Interface ITutorService

        Function GetProfileAsync(personId As Integer) As Task(Of ServiceResult(Of TutorProfile))

        Function UpdateProfileAsync(actingPersonId As Integer,
                                    account As Person,
                                    tutorDetails As TutorProfile) As Task(Of ServiceResult)

        Function GetSubjectsAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of TutorSubject)))

        Function AddSubjectAsync(tutorId As Integer, subjectId As Integer, studyLevelId As Integer) As Task(Of ServiceResult)

        ''' <summary>
        ''' Adds a subject by name, creating it if the catalogue does not have it yet.
        ''' </summary>
        ''' <param name="actingPersonId">
        ''' Needed in addition to tutorId because the audit entry for a newly created
        ''' subject records a PersonId, and a TutorId is not one.
        ''' </param>
        Function AddSubjectByNameAsync(tutorId As Integer,
                                       actingPersonId As Integer,
                                       subjectName As String,
                                       studyLevelId As Integer) As Task(Of ServiceResult)

        ''' <summary>BR-21. Refused, with the reason, when the subject has Confirmed requests or Scheduled bookings.</summary>
        Function RemoveSubjectAsync(tutorId As Integer, tutorSubjectId As Integer) As Task(Of ServiceResult)

        ''' <summary>AT-23. Administrator only.</summary>
        Function SetApprovedAsync(tutorId As Integer, isApproved As Boolean) As Task(Of ServiceResult)

        Function GetRatingAsync(tutorId As Integer) As Task(Of ServiceResult(Of TutorRatingSummary))

    End Interface

    ''' <summary>The Find Tutor screen.</summary>
    Public Interface ITutorSearchService

        ''' <summary>BR-26. Only active, approved tutors are ever returned (AT-08).</summary>
        Function SearchAsync(subjectId As Integer?,
                             studyLevelId As Integer?,
                             qualificationContains As String,
                             minimumExperience As Integer?,
                             pageNumber As Integer,
                             pageSize As Integer) As Task(Of ServiceResult(Of IList(Of TutorSearchResult)))

        Function CountAsync(subjectId As Integer?,
                            studyLevelId As Integer?,
                            qualificationContains As String,
                            minimumExperience As Integer?) As Task(Of Integer)

        ''' <summary>Returns Nothing for a tutor who is hidden, so an id cannot be guessed into view.</summary>
        Function GetDetailAsync(tutorSubjectId As Integer) As Task(Of ServiceResult(Of TutorSearchResult))

        Function GetOpenSlotsAsync(tutorSubjectId As Integer) As Task(Of ServiceResult(Of IList(Of TutorAvailability)))

    End Interface

    ''' <summary>Session requests, from creation to cancellation.</summary>
    Public Interface IRequestService

        ''' <summary>
        ''' Creates a Pending request, enforcing BR-08 to BR-12 in that order and stopping
        ''' at the first failure (AT-09, AT-10, AT-11, AT-12).
        ''' </summary>
        Function CreateAsync(studentId As Integer,
                             tutorSubjectId As Integer,
                             availabilityId As Integer,
                             requestedDate As Date,
                             startTime As TimeSpan,
                             endTime As TimeSpan,
                             studentMessage As String) As Task(Of ServiceResult(Of Integer))

        Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of SessionRequest)))

        Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of SessionRequest)))

        ''' <summary>BR-13. Only a Pending request, and only its own student (AT-13).</summary>
        Function CancelAsync(studentId As Integer, requestId As Integer) As Task(Of ServiceResult)

        Function CountPendingForTutorAsync(tutorId As Integer) As Task(Of Integer)

    End Interface

    ''' <summary>Bookings: accept, decline, complete, cancel.</summary>
    Public Interface IBookingService

        ''' <summary>
        ''' Accepts a Pending request and creates its booking (AT-14, AT-15).
        ''' Enforces BR-14 and BR-15 inside a serialisable transaction; of two concurrent
        ''' accepts exactly one succeeds and the other is told why it did not.
        ''' </summary>
        Function AcceptRequestAsync(tutorId As Integer, requestId As Integer) As Task(Of ServiceResult(Of Integer))

        ''' <summary>AT-16.</summary>
        Function DeclineRequestAsync(tutorId As Integer, requestId As Integer, declineReason As String) As Task(Of ServiceResult)

        Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of Booking)))

        Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of Booking)))

        ''' <summary>BR-24. Returns a failure rather than the record when the caller does not own it.</summary>
        Function GetForViewerAsync(bookingId As Integer, actingPersonId As Integer) As Task(Of ServiceResult(Of Booking))

        ''' <summary>BR-23. Only after the end time has passed, and only by its own tutor (AT-17, AT-18).</summary>
        Function MarkCompletedAsync(tutorId As Integer, bookingId As Integer) As Task(Of ServiceResult)

        ''' <summary>
        ''' Cancels a scheduled session (Part 12.3). The student, the tutor or an
        ''' administrator may all do it; the other party is notified and it is audited.
        ''' </summary>
        ''' <remarks>
        ''' Distinct from <c>IRequestService.CancelAsync</c>, which is BR-13 and withdraws a
        ''' request that has not been accepted yet.
        ''' </remarks>
        Function CancelAsync(actingPersonId As Integer,
                             bookingId As Integer,
                             reasonCategory As ScheduleChangeReason,
                             cancelReason As String) As Task(Of ServiceResult)

        ''' <summary>
        ''' An administrator moves a scheduled session (Part 12.7). Both parties are told and
        ''' the change is audited, because neither of them asked for it.
        ''' </summary>
        Function RescheduleAsync(actingPersonId As Integer,
                                 bookingId As Integer,
                                 newDate As Date,
                                 newStart As TimeSpan,
                                 newEnd As TimeSpan,
                                 reason As String) As Task(Of ServiceResult)

    End Interface

    ''' <summary>Published availability.</summary>
    Public Interface IAvailabilityService

        Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of TutorAvailability)))

        ''' <summary>Enforces BR-06 (end after start), BR-07 (not in the past) and BR-20 (no self-overlap).</summary>
        Function AddAsync(tutorId As Integer,
                          tutorSubjectId As Integer,
                          availabilityDate As Date,
                          startTime As TimeSpan,
                          endTime As TimeSpan) As Task(Of ServiceResult(Of Integer))

        ''' <summary>BR-22. Refused, with the reason, when a Scheduled booking depends on the slot.</summary>
        Function DeleteAsync(tutorId As Integer, availabilityId As Integer) As Task(Of ServiceResult)

    End Interface

    ''' <summary>Reviews of completed bookings.</summary>
    Public Interface IReviewService

        ''' <summary>
        ''' Re-checks BR-16 to BR-19 server-side before inserting, so URL tampering is
        ''' rejected even with JavaScript disabled (AT-19, AT-20, AT-21).
        ''' </summary>
        Function CreateAsync(studentId As Integer,
                             bookingId As Integer,
                             rating As Byte,
                             comment As String) As Task(Of ServiceResult(Of Integer))

        ''' <summary>True when the Write Review screen may be shown for this booking.</summary>
        Function CanReviewAsync(studentId As Integer, bookingId As Integer) As Task(Of Boolean)

        Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of Review)))

        Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of Review)))

    End Interface

    ''' <summary>
    ''' The shared subject catalogue. Every path that turns a typed subject name into a
    ''' SubjectId - student, tutor and administrator - goes through this one interface, so
    ''' the three cannot normalise names differently and split one subject into three.
    ''' </summary>
    Public Interface ISubjectCatalogueService

        ''' <summary>
        ''' Returns the id of the subject with this name, creating it if nothing matches.
        ''' </summary>
        ''' <param name="actorRole">
        ''' Recorded on the audit entry, so an administrator can see whether a subject came
        ''' from a student, a tutor or another administrator.
        ''' </param>
        Function EnsureSubjectAsync(actingPersonId As Integer,
                                    actorRole As UserRole,
                                    subjectName As String) As Task(Of ServiceResult(Of SubjectOutcome))

    End Interface

    ''' <summary>
    ''' Administrator operations.
    ''' </summary>
    ''' <remarks>
    ''' Not in Appendix B. Added because pages must not reach into repositories and none of
    ''' the listed services owns user management or moderation.
    ''' </remarks>
    Public Interface IAdminService

        Function SearchUsersAsync(searchTerm As String,
                                  role As UserRole?,
                                  isActive As Boolean?) As Task(Of ServiceResult(Of IList(Of Person)))

        ''' <summary>BR-25 / AT-24. Blocks the next sign-in and ends any live session.</summary>
        Function SetUserActiveAsync(actingPersonId As Integer,
                                    personId As Integer,
                                    isActive As Boolean) As Task(Of ServiceResult)

        ''' <summary>
        ''' AT-23. Delegates to ITutorService so approval lives in one place, and records who
        ''' approved (Part 12.7).
        ''' </summary>
        ''' <param name="actingPersonId">
        ''' Required, not optional: an audit record with no actor answers none of the
        ''' questions an audit log exists to answer, and an optional parameter is how that
        ''' happens by accident.
        ''' </param>
        Function ApproveTutorAsync(actingPersonId As Integer,
                                   tutorId As Integer,
                                   isApproved As Boolean) As Task(Of ServiceResult)

        ''' <summary>
        ''' Creates a second (or third...) administrator. RegisterAsync refuses the
        ''' Administrator role and Setup.aspx closes after the first one, so without this
        ''' the promise made in AuthenticationService - "only an existing administrator can
        ''' create another" - had no implementation behind it.
        ''' </summary>
        ''' <param name="actingPersonId">
        ''' The administrator doing the creating. Required, and verified against the
        ''' database rather than trusted: creating an administrator is the one action that
        ''' can escalate privilege, so the caller's claim to be one is checked here and not
        ''' only by the URL rules.
        ''' </param>
        Function CreateAdministratorAsync(actingPersonId As Integer,
                                          account As Person,
                                          password As String,
                                          confirmPassword As String) As Task(Of ServiceResult(Of Integer))

        ''' <summary>Resolves the tutor row behind a Person, for the approve action.</summary>
        Function GetTutorProfileAsync(personId As Integer) As Task(Of TutorProfile)

        Function GetSubjectsAsync() As Task(Of ServiceResult(Of IList(Of Subject)))

        ''' <summary>
        ''' The subject mutations take the acting administrator so they can be audited
        ''' (Part 12.7). Renaming is not audited and so does not take one.
        ''' </summary>
        Function AddSubjectAsync(actingPersonId As Integer, subjectName As String) As Task(Of ServiceResult)
        Function RenameSubjectAsync(subjectId As Integer, subjectName As String) As Task(Of ServiceResult)
        Function SetSubjectActiveAsync(actingPersonId As Integer, subjectId As Integer, isActive As Boolean) As Task(Of ServiceResult)

        ''' <summary>AT-25. Blocked when in use, with deactivation offered instead.</summary>
        Function DeleteSubjectAsync(actingPersonId As Integer, subjectId As Integer) As Task(Of ServiceResult)

        Function GetStudyLevelsAsync() As Task(Of ServiceResult(Of IList(Of StudyLevel)))
        Function AddStudyLevelAsync(name As String, sortOrder As Integer) As Task(Of ServiceResult)
        Function UpdateStudyLevelAsync(studyLevelId As Integer, name As String, sortOrder As Integer) As Task(Of ServiceResult)

        Function GetAllBookingsAsync() As Task(Of ServiceResult(Of IList(Of Booking)))
        Function GetAllRequestsAsync(status As String) As Task(Of ServiceResult(Of IList(Of SessionRequest)))
        Function GetAllReviewsAsync() As Task(Of ServiceResult(Of IList(Of Review)))

        ''' <summary>Hide-with-reason instead of delete, preserving the audit trail.</summary>
        Function SetReviewHiddenAsync(actingPersonId As Integer,
                                      reviewId As Integer,
                                      isHidden As Boolean,
                                      reason As String) As Task(Of ServiceResult)

    End Interface

    ''' <summary>In-app notifications.</summary>
    Public Interface INotificationService

        ''' <summary>BR-27. Called by the services that change state, not by pages.</summary>
        Function NotifyAsync(personId As Integer,
                             notificationType As NotificationType,
                             title As String,
                             message As String,
                             linkUrl As String) As Task(Of ServiceResult)

        Function GetForPersonAsync(personId As Integer, unreadOnly As Boolean) As Task(Of ServiceResult(Of IList(Of Notification)))

        Function CountUnreadAsync(personId As Integer) As Task(Of Integer)

        Function MarkReadAsync(personId As Integer, notificationId As Integer) As Task(Of ServiceResult)

        Function MarkAllReadAsync(personId As Integer) As Task(Of ServiceResult)

    End Interface

End Namespace
