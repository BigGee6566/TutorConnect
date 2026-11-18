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
    ''' Tutor profile, subjects taught, and approval (BR-21, BR-26).
    ''' </summary>
    Public Class TutorService
        Implements ITutorService

        Private ReadOnly _users As UserRepository
        Private ReadOnly _tutors As TutorRepository
        Private ReadOnly _subjects As SubjectRepository
        Private ReadOnly _reviews As ReviewRepository
        Private ReadOnly _notifications As INotificationService

        ''' <summary>
        ''' Resolves a typed subject name to a SubjectId. Shared with the student and
        ''' administrator paths so the three cannot split one subject into three.
        ''' </summary>
        Private ReadOnly _catalogue As ISubjectCatalogueService

        Public Sub New()
            Me.New(New UserRepository(), New TutorRepository(), New SubjectRepository(),
                   New ReviewRepository(), New NotificationService())
        End Sub

        Public Sub New(users As UserRepository,
                       tutors As TutorRepository,
                       subjects As SubjectRepository,
                       reviews As ReviewRepository,
                       notifications As INotificationService)
            Me.New(users, tutors, subjects, reviews, notifications, New SubjectCatalogueService())
        End Sub

        Public Sub New(users As UserRepository,
                       tutors As TutorRepository,
                       subjects As SubjectRepository,
                       reviews As ReviewRepository,
                       notifications As INotificationService,
                       catalogue As ISubjectCatalogueService)
            _users = users
            _tutors = tutors
            _subjects = subjects
            _reviews = reviews
            _notifications = notifications
            _catalogue = catalogue
        End Sub

        ''' <inheritdoc/>
        Public Async Function GetProfileAsync(personId As Integer) As Task(Of ServiceResult(Of TutorProfile)) _
            Implements ITutorService.GetProfileAsync

            Dim profile As TutorProfile =
                Await _users.GetTutorByPersonIdAsync(personId).ConfigureAwait(False)

            If profile Is Nothing Then
                Return ServiceResult(Of TutorProfile).Fail("No tutor profile is linked to this account.")
            End If

            Return ServiceResult(Of TutorProfile).Ok(profile)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-24 / AT-22. The tutor record written is the one belonging to actingPersonId.
        ''' A TutorId arriving on the posted object is ignored, so posting another tutor's
        ''' id changes nothing of theirs.
        ''' </remarks>
        Public Async Function UpdateProfileAsync(actingPersonId As Integer,
                                                 account As Person,
                                                 tutorDetails As TutorProfile) As Task(Of ServiceResult) _
            Implements ITutorService.UpdateProfileAsync

            If account Is Nothing Then Throw New ArgumentNullException(NameOf(account))
            If tutorDetails Is Nothing Then Throw New ArgumentNullException(NameOf(tutorDetails))
            If actingPersonId <= 0 Then Return ServiceResult.Fail("You are not signed in.")

            Dim owned As TutorProfile =
                Await _users.GetTutorByPersonIdAsync(actingPersonId).ConfigureAwait(False)

            If owned Is Nothing Then
                Return ServiceResult.Fail("No tutor profile is linked to this account.")
            End If

            account.PersonId = actingPersonId
            tutorDetails.TutorId = owned.TutorId

            Dim validation As ValidationResult = UserValidator.ValidateAccount(
                account.UserName, account.FirstName, account.LastName, account.Gender,
                account.EmailAddress, account.CellNumber, account.Location)

            For Each item In UserValidator.ValidateTutorDetails(
                tutorDetails.Qualification, tutorDetails.ExperienceYears, tutorDetails.RatePerSession).Errors
                validation.AddError(item.FieldName, item.Message)
            Next

            If tutorDetails.Bio IsNot Nothing AndAlso tutorDetails.Bio.Length > 1000 Then
                validation.AddError("Bio", "Your description must be 1000 characters or fewer.")
            End If

            If Not validation.IsValid Then
                Return ServiceResult.Fail(validation.ToSummary())
            End If

            If Await _users.EmailExistsAsync(account.EmailAddress, actingPersonId).ConfigureAwait(False) Then
                Return ServiceResult.Fail("Another account already uses that email address.")
            End If

            Dim savedAccount As Boolean = Await _users.UpdateProfileAsync(account).ConfigureAwait(False)
            Dim savedTutor As Boolean = Await _tutors.UpdateTutorDetailsAsync(tutorDetails).ConfigureAwait(False)

            Return If(savedAccount AndAlso savedTutor,
                      ServiceResult.Ok("Your profile has been saved."),
                      ServiceResult.Fail("Your profile could not be saved. Try again."))
        End Function

        ''' <inheritdoc/>
        Public Async Function GetSubjectsAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of TutorSubject))) _
            Implements ITutorService.GetSubjectsAsync

            Dim rows As IList(Of TutorSubject) =
                Await _subjects.GetTutorSubjectsAsync(tutorId).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of TutorSubject)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function AddSubjectAsync(tutorId As Integer,
                                              subjectId As Integer,
                                              studyLevelId As Integer) As Task(Of ServiceResult) _
            Implements ITutorService.AddSubjectAsync

            If tutorId <= 0 Then Return ServiceResult.Fail("You are not signed in as a tutor.")

            If subjectId <= 0 OrElse studyLevelId <= 0 Then
                Return ServiceResult.Fail("Choose both a subject and a study level.")
            End If

            ' Part 12.1, and the same check the student path has always had. The dropdown
            ' only offers active subjects, but that is presentation - a post naming a
            ' deactivated subject would otherwise be accepted, and because
            ' TutorRepository.SearchAsync does not filter on Subject.IsActive, the withdrawn
            ' subject would then be live in Find Tutor.
            If Not Await _subjects.IsSubjectActiveAsync(subjectId).ConfigureAwait(False) Then
                Return ServiceResult.Fail("That subject is no longer offered.")
            End If

            If Await _subjects.TutorSubjectExistsAsync(tutorId, subjectId, studyLevelId).ConfigureAwait(False) Then
                Return ServiceResult.Fail("You already teach that subject at that level.")
            End If

            Try
                Await _subjects.AddTutorSubjectAsync(tutorId, subjectId, studyLevelId).ConfigureAwait(False)
                Return ServiceResult.Ok("Subject added.")

            Catch ex As System.Data.SqlClient.SqlException When ex.Number = 2601 OrElse ex.Number = 2627
                Return ServiceResult.Fail("You already teach that subject at that level.")
            End Try
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' Resolves the name to a subject - reusing an existing one where the name matches,
        ''' creating it otherwise - then hands off to the id overload above, so BR-21's
        ''' siblings, the duplicate check and the unique-index race catch all still apply
        ''' exactly as they do when the subject was chosen from the dropdown.
        ''' </remarks>
        Public Async Function AddSubjectByNameAsync(tutorId As Integer,
                                                    actingPersonId As Integer,
                                                    subjectName As String,
                                                    studyLevelId As Integer) As Task(Of ServiceResult) _
            Implements ITutorService.AddSubjectByNameAsync

            If tutorId <= 0 Then Return ServiceResult.Fail("You are not signed in as a tutor.")

            If studyLevelId <= 0 Then
                Return ServiceResult.Fail("Choose a study level for the new subject.")
            End If

            Dim resolved As ServiceResult(Of SubjectOutcome) = Await _catalogue.EnsureSubjectAsync(
                actingPersonId, UserRole.Tutor, subjectName).ConfigureAwait(False)

            If Not resolved.Success Then Return ServiceResult.Fail(resolved.Message)

            Dim added As ServiceResult = Await AddSubjectAsync(
                tutorId, resolved.Data.SubjectId, studyLevelId).ConfigureAwait(False)

            If Not added.Success Then Return added

            ' The catalogue's message is the informative half - it says whether the subject
            ' was created or matched to one that already existed.
            Return ServiceResult.Ok(resolved.Message)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-21. Removal is refused - with the reason - while confirmed requests or
        ''' scheduled bookings still point at the subject. Failing silently here would
        ''' leave a student with a session whose subject no longer exists.
        ''' </remarks>
        Public Async Function RemoveSubjectAsync(tutorId As Integer,
                                                 tutorSubjectId As Integer) As Task(Of ServiceResult) _
            Implements ITutorService.RemoveSubjectAsync

            If Await _subjects.TutorSubjectHasCommitmentsAsync(tutorSubjectId).ConfigureAwait(False) Then
                Return ServiceResult.Fail(
                    "This subject cannot be removed because it has confirmed requests or " &
                    "scheduled sessions. Deal with those first.")
            End If

            Dim removed As Boolean =
                Await _subjects.RemoveTutorSubjectAsync(tutorSubjectId, tutorId).ConfigureAwait(False)

            If Not removed Then
                ' Availability rows reference the subject, so the delete can also fail on a
                ' foreign key. Say what to do rather than reporting a database error.
                Return ServiceResult.Fail(
                    "That subject could not be removed. Delete its availability slots first.")
            End If

            Return ServiceResult.Ok("Subject removed.")
        End Function

        ''' <inheritdoc/>
        Public Async Function SetApprovedAsync(tutorId As Integer,
                                               isApproved As Boolean) As Task(Of ServiceResult) _
            Implements ITutorService.SetApprovedAsync

            Dim changed As Boolean =
                Await _tutors.SetApprovedAsync(tutorId, isApproved).ConfigureAwait(False)

            If Not changed Then
                Return ServiceResult.Fail("That tutor could not be updated.")
            End If

            If isApproved Then
                Dim personId As Integer =
                    Await ScalarPersonIdAsync(tutorId).ConfigureAwait(False)

                If personId > 0 Then
                    Await _notifications.NotifyAsync(
                        personId,
                        NotificationType.TutorApproved,
                        "Your tutor account is approved",
                        "Students can now find you and request sessions.",
                        "~/Tutor/Dashboard.aspx").ConfigureAwait(False)
                End If
            End If

            Return ServiceResult.Ok(If(isApproved, "Tutor approved.", "Tutor approval withdrawn."))
        End Function

        ''' <inheritdoc/>
        Public Async Function GetRatingAsync(tutorId As Integer) As Task(Of ServiceResult(Of TutorRatingSummary)) _
            Implements ITutorService.GetRatingAsync

            Dim summary As TutorRatingSummary =
                Await _reviews.GetTutorRatingAsync(tutorId).ConfigureAwait(False)

            Return ServiceResult(Of TutorRatingSummary).Ok(summary)
        End Function

        Private Async Function ScalarPersonIdAsync(tutorId As Integer) As Task(Of Integer)
            Dim subjects As IList(Of TutorSubject) =
                Await _subjects.GetTutorSubjectsAsync(tutorId).ConfigureAwait(False)

            If subjects.Count = 0 Then Return 0

            Return Await _tutors.GetPersonIdForTutorSubjectAsync(
                subjects(0).TutorSubjectId).ConfigureAwait(False)
        End Function

    End Class

End Namespace
