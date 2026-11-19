Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Validation

Namespace Services

    ''' <summary>
    ''' Administrator operations: users, subjects, and moderation.
    ''' </summary>
    ''' <remarks>
    ''' Appendix B lists no administrator service. It exists because pages must not call
    ''' repositories directly and none of the listed services owns "manage users" - a
    ''' deliberate addition, recorded here rather than silently made.
    '''
    ''' Nothing here ever reads, resets or reveals a password. Deactivation is the only
    ''' account action available, and it is reversible.
    ''' </remarks>
    Public Class AdminService
        Implements IAdminService

        Private ReadOnly _users As UserRepository
        Private ReadOnly _subjects As SubjectRepository
        Private ReadOnly _reviews As ReviewRepository
        Private ReadOnly _requests As RequestRepository
        Private ReadOnly _bookings As BookingRepository
        Private ReadOnly _tutors As ITutorService
        Private ReadOnly _notifications As INotificationService

        Public Sub New()
            Me.New(New UserRepository(), New SubjectRepository(), New ReviewRepository(),
                   New RequestRepository(), New BookingRepository(),
                   New TutorService(), New NotificationService())
        End Sub

        Public Sub New(users As UserRepository,
                       subjects As SubjectRepository,
                       reviews As ReviewRepository,
                       requests As RequestRepository,
                       bookings As BookingRepository,
                       tutors As ITutorService,
                       notifications As INotificationService)
            _users = users
            _subjects = subjects
            _reviews = reviews
            _requests = requests
            _bookings = bookings
            _tutors = tutors
            _notifications = notifications
            _audit = New AuditService()
            _catalogue = New SubjectCatalogueService()
        End Sub

        ''' <summary>
        ''' Shared with the student and tutor paths, so all three roles add subjects under
        ''' the same name rules and the same reuse behaviour.
        ''' </summary>
        Private ReadOnly _catalogue As ISubjectCatalogueService

        ''' <summary>
        ''' Records administrator actions (Part 12.7).
        ''' </summary>
        ''' <remarks>
        ''' Added because the audit page offered filters - tutor approval, account activation,
        ''' review moderation - for actions nothing ever wrote. A filter that can never match
        ''' is the same defect as a button that does nothing.
        ''' </remarks>
        Private ReadOnly _audit As AuditService

#Region "Users"

        ''' <inheritdoc/>
        Public Async Function SearchUsersAsync(searchTerm As String,
                                               role As UserRole?,
                                               isActive As Boolean?) As Task(Of ServiceResult(Of IList(Of Person))) _
            Implements IAdminService.SearchUsersAsync

            Dim rows As IList(Of Person) =
                Await _users.SearchAsync(searchTerm, role, isActive).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of Person)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-25 / AT-24. Deactivating blocks the next sign-in, and BasePage refuses the
        ''' account's next request, so a live session ends rather than running until its
        ''' ticket expires.
        ''' </remarks>
        Public Async Function SetUserActiveAsync(actingPersonId As Integer,
                                                 personId As Integer,
                                                 isActive As Boolean) As Task(Of ServiceResult) _
            Implements IAdminService.SetUserActiveAsync

            ' An administrator locking themselves out would need another administrator to
            ' undo it, and there may not be one.
            If personId = actingPersonId AndAlso Not isActive Then
                Return ServiceResult.Fail("You cannot deactivate your own account.")
            End If

            Dim changed As Boolean = Await _users.SetActiveAsync(personId, isActive).ConfigureAwait(False)

            If Not changed Then
                Return ServiceResult.Fail("That account could not be updated.")
            End If

            If Not isActive Then
                Await _notifications.NotifyAsync(
                    personId,
                    NotificationType.AccountDeactivated,
                    "Your account has been deactivated",
                    "You can no longer sign in. Contact the administrator if you think this is wrong.",
                    Nothing).ConfigureAwait(False)
            End If

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Administrator.ToString(),
                If(isActive, AuditAction.UserActivated, AuditAction.UserDeactivated),
                AuditService.PersonEntity, personId,
                If(isActive, "Inactive", "Active"),
                If(isActive, "Active", "Inactive"),
                Nothing, Nothing).ConfigureAwait(False)

            Return ServiceResult.Ok(If(isActive, "Account activated.", "Account deactivated."))
        End Function

        ''' <summary>Refused when the acting account is not, in fact, an administrator.</summary>
        Public Const NotAnAdministratorMessage As String =
            "Only an administrator can create another administrator account."

        ''' <inheritdoc/>
        ''' <remarks>
        ''' <para>
        ''' This mirrors <c>AuthenticationService.CreateFirstAdministratorAsync</c> rather
        ''' than calling it: that method exists only while the installation has no
        ''' administrator and refuses outright once one does, which is the opposite of the
        ''' precondition here.
        ''' </para>
        ''' <para>
        ''' The acting person's role is re-read from the database. The Web.config
        ''' &lt;location&gt; rule and RolePageBase both already gate the page, but this is
        ''' the one operation whose result is another account with full privilege, and a
        ''' service must not assume it is only ever reached through the page that is
        ''' supposed to call it.
        ''' </para>
        ''' <para>
        ''' No profile row is written: an administrator has neither a Student nor a Tutor
        ''' record (D-05).
        ''' </para>
        ''' </remarks>
        Public Async Function CreateAdministratorAsync(actingPersonId As Integer,
                                                       account As Person,
                                                       password As String,
                                                       confirmPassword As String) As Task(Of ServiceResult(Of Integer)) _
            Implements IAdminService.CreateAdministratorAsync

            If account Is Nothing Then Throw New ArgumentNullException(NameOf(account))

            ' Checked before anything else, so a caller without the right cannot learn from
            ' the validation messages what the form would have accepted.
            Dim actor As Person = Await _users.GetByIdAsync(actingPersonId).ConfigureAwait(False)

            If actor Is Nothing OrElse actor.Role <> UserRole.Administrator OrElse Not actor.IsActive Then
                Return ServiceResult(Of Integer).Fail(NotAnAdministratorMessage)
            End If

            Dim validation As ValidationResult = UserValidator.ValidateAccount(
                account.UserName, account.FirstName, account.LastName, account.Gender,
                account.EmailAddress, account.CellNumber, account.Location)

            For Each item In UserValidator.ValidatePasswordConfirmation(password, confirmPassword).Errors
                validation.AddError(item.FieldName, item.Message)
            Next

            If Not validation.IsValid Then
                Return ServiceResult(Of Integer).Fail(validation.ToSummary())
            End If

            ' BR-01. The UNIQUE constraints are the real guard; this produces a usable
            ' message instead of an exception. Unlike the public Register form, both
            ' clashes are named separately - the person reading this screen is already
            ' trusted with the full user list, so there is no oracle to protect.
            If Await _users.UserNameExistsAsync(account.UserName).ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail("That username is already taken.")
            End If

            If Await _users.EmailExistsAsync(account.EmailAddress).ConfigureAwait(False) Then
                Return ServiceResult(Of Integer).Fail("That email address is already registered.")
            End If

            Dim passwordHash As Byte() = Nothing
            Dim passwordSalt As Byte() = Nothing
            Dim hashIterations As Integer
            PasswordHelper.CreateHash(password, passwordHash, passwordSalt, hashIterations)

            ' Set here, not taken from the caller: the page must not be able to choose the
            ' role of the account this method creates.
            account.Role = UserRole.Administrator
            account.IsActive = True

            Dim personId As Integer = Await _users.CreateAccountAsync(
                account, passwordHash, passwordSalt, hashIterations, Nothing, Nothing).ConfigureAwait(False)

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Administrator.ToString(),
                AuditAction.AdministratorCreated,
                AuditService.PersonEntity, personId,
                Nothing,
                $"Administrator '{account.UserName}' created",
                Nothing, Nothing).ConfigureAwait(False)

            Return ServiceResult(Of Integer).Ok(
                personId,
                $"The administrator account '{account.UserName}' was created. " &
                "Give them the password you chose and ask them to change it after signing in.")
        End Function

        ''' <inheritdoc/>
        Public Async Function ApproveTutorAsync(actingPersonId As Integer,
                                                tutorId As Integer,
                                                isApproved As Boolean) As Task(Of ServiceResult) _
            Implements IAdminService.ApproveTutorAsync

            ' Delegated: the tutor service already owns approval, including the
            ' notification, and duplicating it here would let the two drift.
            Dim result As ServiceResult =
                Await _tutors.SetApprovedAsync(tutorId, isApproved).ConfigureAwait(False)

            If Not result.Success Then Return result

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Administrator.ToString(),
                If(isApproved, AuditAction.TutorApproved, AuditAction.TutorRejected),
                AuditService.TutorEntity, tutorId,
                If(isApproved, "Awaiting approval", "Approved"),
                If(isApproved, "Approved", "Not approved"),
                Nothing, Nothing).ConfigureAwait(False)

            Return result
        End Function

        ''' <inheritdoc/>
        Public Async Function GetTutorProfileAsync(personId As Integer) As Task(Of TutorProfile) _
            Implements IAdminService.GetTutorProfileAsync

            Return Await _users.GetTutorByPersonIdAsync(personId).ConfigureAwait(False)
        End Function

#End Region

#Region "Subjects and study levels"

        ''' <inheritdoc/>
        Public Async Function GetSubjectsAsync() As Task(Of ServiceResult(Of IList(Of Subject))) _
            Implements IAdminService.GetSubjectsAsync

            Dim rows As IList(Of Subject) =
                Await _subjects.GetSubjectsAsync(includeInactive:=True).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of Subject)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function AddSubjectAsync(actingPersonId As Integer,
                                              subjectName As String) As Task(Of ServiceResult) _
            Implements IAdminService.AddSubjectAsync

            ' Routed through the shared catalogue rather than straight to the repository, so
            ' the administrator gets the same name rules and the same reuse-an-existing-name
            ' behaviour as a student or a tutor. Before this, an over-long name was silently
            ' truncated by the parameter size instead of being refused.
            Dim resolved As ServiceResult(Of SubjectOutcome) = Await _catalogue.EnsureSubjectAsync(
                actingPersonId, UserRole.Administrator, subjectName).ConfigureAwait(False)

            If Not resolved.Success Then Return ServiceResult.Fail(resolved.Message)

            If Not resolved.Data.WasCreated Then
                ' Kept as a refusal for the administrator. On the member pages reusing an
                ' existing subject is the helpful outcome; here the whole intent was to add
                ' a new one, so saying "it already exists" is the honest answer.
                Return ServiceResult.Fail("A subject with that name already exists.")
            End If

            Return ServiceResult.Ok("Subject added.")
        End Function

        ''' <inheritdoc/>
        Public Async Function RenameSubjectAsync(subjectId As Integer,
                                                 subjectName As String) As Task(Of ServiceResult) _
            Implements IAdminService.RenameSubjectAsync

            If String.IsNullOrWhiteSpace(subjectName) Then
                Return ServiceResult.Fail("Enter a subject name.")
            End If

            Try
                Dim ok As Boolean =
                    Await _subjects.RenameSubjectAsync(subjectId, subjectName.Trim()).ConfigureAwait(False)

                Return If(ok, ServiceResult.Ok("Subject renamed."), ServiceResult.Fail("That subject no longer exists."))

            Catch ex As Npgsql.PostgresException When TutorConnect.Data.DatabaseHelper.IsUniqueViolation(ex)
                Return ServiceResult.Fail("A subject with that name already exists.")
            End Try
        End Function

        ''' <inheritdoc/>
        Public Async Function SetSubjectActiveAsync(actingPersonId As Integer,
                                                    subjectId As Integer,
                                                    isActive As Boolean) As Task(Of ServiceResult) _
            Implements IAdminService.SetSubjectActiveAsync

            Dim ok As Boolean =
                Await _subjects.SetSubjectActiveAsync(subjectId, isActive).ConfigureAwait(False)

            If Not ok Then Return ServiceResult.Fail("That subject no longer exists.")

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Administrator.ToString(),
                If(isActive, AuditAction.SubjectActivated, AuditAction.SubjectDeactivated),
                AuditService.SubjectEntity, subjectId,
                If(isActive, "Inactive", "Active"),
                If(isActive, "Active", "Inactive"),
                Nothing, Nothing).ConfigureAwait(False)

            Return ServiceResult.Ok(If(isActive, "Subject activated.", "Subject deactivated."))
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' AT-25. A subject anyone is using is never deleted: the historical bookings that
        ''' reference it would lose their name. Deactivation is offered instead, in the
        ''' same message.
        ''' </remarks>
        Public Async Function DeleteSubjectAsync(actingPersonId As Integer,
                                                 subjectId As Integer) As Task(Of ServiceResult) _
            Implements IAdminService.DeleteSubjectAsync

            If Await _subjects.IsSubjectInUseAsync(subjectId).ConfigureAwait(False) Then
                Return ServiceResult.Fail(
                    "This subject is in use by a student or a tutor and cannot be deleted. " &
                    "Deactivate it instead - it will disappear from the dropdowns while " &
                    "existing sessions keep their subject.")
            End If

            Try
                Dim ok As Boolean = Await _subjects.DeleteSubjectAsync(subjectId).ConfigureAwait(False)

                If Not ok Then Return ServiceResult.Fail("That subject no longer exists.")

                ' Deleting is the one destructive action on this screen, so it is the one that
                ' most needs a record of who did it.
                Await _audit.RecordAsync(
                    actingPersonId, UserRole.Administrator.ToString(), AuditAction.SubjectDeleted,
                    AuditService.SubjectEntity, subjectId,
                    "Existed", "Deleted", Nothing, Nothing).ConfigureAwait(False)

                Return ServiceResult.Ok("Subject deleted.")

            Catch ex As Npgsql.PostgresException When TutorConnect.Data.DatabaseHelper.IsForeignKeyViolation(ex)
                ' A foreign key caught a reference the check above missed - a race, or a
                ' table the check does not cover. Refuse rather than report a raw error.
                Return ServiceResult.Fail(
                    "This subject is still referenced elsewhere and cannot be deleted. Deactivate it instead.")
            End Try
        End Function

        ''' <inheritdoc/>
        Public Async Function GetStudyLevelsAsync() As Task(Of ServiceResult(Of IList(Of StudyLevel))) _
            Implements IAdminService.GetStudyLevelsAsync

            Dim rows As IList(Of StudyLevel) = Await _subjects.GetStudyLevelsAsync().ConfigureAwait(False)
            Return ServiceResult(Of IList(Of StudyLevel)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function AddStudyLevelAsync(name As String, sortOrder As Integer) As Task(Of ServiceResult) _
            Implements IAdminService.AddStudyLevelAsync

            If String.IsNullOrWhiteSpace(name) Then
                Return ServiceResult.Fail("Enter a study level name.")
            End If

            Try
                Await _subjects.CreateStudyLevelAsync(name.Trim(), sortOrder).ConfigureAwait(False)
                Return ServiceResult.Ok("Study level added.")

            Catch ex As Npgsql.PostgresException When TutorConnect.Data.DatabaseHelper.IsUniqueViolation(ex)
                Return ServiceResult.Fail("A study level with that name already exists.")
            End Try
        End Function

        ''' <inheritdoc/>
        Public Async Function UpdateStudyLevelAsync(studyLevelId As Integer,
                                                    name As String,
                                                    sortOrder As Integer) As Task(Of ServiceResult) _
            Implements IAdminService.UpdateStudyLevelAsync

            If String.IsNullOrWhiteSpace(name) Then
                Return ServiceResult.Fail("Enter a study level name.")
            End If

            Try
                Dim ok As Boolean = Await _subjects.UpdateStudyLevelAsync(
                    studyLevelId, name.Trim(), sortOrder).ConfigureAwait(False)

                Return If(ok, ServiceResult.Ok("Study level saved."), ServiceResult.Fail("That study level no longer exists."))

            Catch ex As Npgsql.PostgresException When TutorConnect.Data.DatabaseHelper.IsUniqueViolation(ex)
                Return ServiceResult.Fail("A study level with that name already exists.")
            End Try
        End Function

#End Region

#Region "Bookings, requests and moderation"

        ''' <inheritdoc/>
        Public Async Function GetAllBookingsAsync() As Task(Of ServiceResult(Of IList(Of Booking))) _
            Implements IAdminService.GetAllBookingsAsync

            Dim rows As IList(Of Booking) = Await _bookings.GetAllAsync().ConfigureAwait(False)
            Return ServiceResult(Of IList(Of Booking)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function GetAllRequestsAsync(status As String) As Task(Of ServiceResult(Of IList(Of SessionRequest))) _
            Implements IAdminService.GetAllRequestsAsync

            Dim rows As IList(Of SessionRequest) =
                Await _requests.GetAllAsync(If(String.IsNullOrWhiteSpace(status), Nothing, status)).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of SessionRequest)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function GetAllReviewsAsync() As Task(Of ServiceResult(Of IList(Of Review))) _
            Implements IAdminService.GetAllReviewsAsync

            Dim rows As IList(Of Review) = Await _reviews.GetAllAsync().ConfigureAwait(False)
            Return ServiceResult(Of IList(Of Review)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' A reason is required when hiding: "hidden by an administrator" with no
        ''' explanation is not an audit trail, and the student who wrote it is shown the
        ''' reason on My Reviews.
        ''' </remarks>
        Public Async Function SetReviewHiddenAsync(actingPersonId As Integer,
                                                   reviewId As Integer,
                                                   isHidden As Boolean,
                                                   reason As String) As Task(Of ServiceResult) _
            Implements IAdminService.SetReviewHiddenAsync

            If isHidden AndAlso String.IsNullOrWhiteSpace(reason) Then
                Return ServiceResult.Fail("Give a reason for hiding this review.")
            End If

            Dim trimmed As String = If(reason Is Nothing, Nothing, reason.Trim())

            If trimmed IsNot Nothing AndAlso trimmed.Length > 200 Then
                Return ServiceResult.Fail("The reason must be 200 characters or fewer.")
            End If

            Dim ok As Boolean = Await _reviews.SetHiddenAsync(
                reviewId, isHidden, trimmed, actingPersonId).ConfigureAwait(False)

            If Not ok Then
                Return ServiceResult.Fail("That review no longer exists.")
            End If

            Await _audit.RecordAsync(
                actingPersonId, UserRole.Administrator.ToString(),
                If(isHidden, AuditAction.ReviewHidden, AuditAction.ReviewRestored),
                AuditService.ReviewEntity, reviewId,
                If(isHidden, "Visible", "Hidden"),
                If(isHidden, "Hidden", "Visible"),
                trimmed, Nothing).ConfigureAwait(False)

            Return ServiceResult.Ok(If(isHidden,
                                       "Review hidden. It is no longer shown to students or counted in the tutor's rating.",
                                       "Review restored."))
        End Function

#End Region

    End Class

End Namespace
