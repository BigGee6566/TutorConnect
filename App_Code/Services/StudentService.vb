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
    ''' Student profile and preferred subjects.
    ''' </summary>
    Public Class StudentService
        Implements IStudentService

        Private ReadOnly _users As UserRepository
        Private ReadOnly _subjects As SubjectRepository

        ''' <summary>
        ''' Resolves a typed subject name to a SubjectId. Shared with the tutor and
        ''' administrator paths so the three cannot split one subject into three.
        ''' </summary>
        Private ReadOnly _catalogue As ISubjectCatalogueService

        Public Sub New()
            Me.New(New UserRepository(), New SubjectRepository())
        End Sub

        Public Sub New(users As UserRepository, subjects As SubjectRepository)
            Me.New(users, subjects, New SubjectCatalogueService())
        End Sub

        Public Sub New(users As UserRepository,
                       subjects As SubjectRepository,
                       catalogue As ISubjectCatalogueService)
            If users Is Nothing Then Throw New ArgumentNullException(NameOf(users))
            If subjects Is Nothing Then Throw New ArgumentNullException(NameOf(subjects))
            If catalogue Is Nothing Then Throw New ArgumentNullException(NameOf(catalogue))
            _users = users
            _subjects = subjects
            _catalogue = catalogue
        End Sub

        ''' <inheritdoc/>
        Public Async Function GetProfileAsync(personId As Integer) As Task(Of ServiceResult(Of StudentProfile)) _
            Implements IStudentService.GetProfileAsync

            Dim profile As StudentProfile =
                Await _users.GetStudentByPersonIdAsync(personId).ConfigureAwait(False)

            If profile Is Nothing Then
                Return ServiceResult(Of StudentProfile).Fail("No student profile is linked to this account.")
            End If

            Return ServiceResult(Of StudentProfile).Ok(profile)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' BR-24. The record updated is the one belonging to actingPersonId. Whatever
        ''' PersonId the posted object carries is overwritten, so a tampered form cannot
        ''' edit someone else's profile.
        ''' </remarks>
        Public Async Function UpdateProfileAsync(actingPersonId As Integer,
                                                 account As Person) As Task(Of ServiceResult) _
            Implements IStudentService.UpdateProfileAsync

            If account Is Nothing Then Throw New ArgumentNullException(NameOf(account))
            If actingPersonId <= 0 Then Return ServiceResult.Fail("You are not signed in.")

            account.PersonId = actingPersonId

            Dim validation As ValidationResult = UserValidator.ValidateAccount(
                account.UserName, account.FirstName, account.LastName, account.Gender,
                account.EmailAddress, account.CellNumber, account.Location)

            If Not validation.IsValid Then
                Return ServiceResult.Fail(validation.ToSummary())
            End If

            ' BR-01, excluding this account so saving an unchanged email is not a clash.
            If Await _users.EmailExistsAsync(account.EmailAddress, actingPersonId).ConfigureAwait(False) Then
                Return ServiceResult.Fail("Another account already uses that email address.")
            End If

            Dim saved As Boolean = Await _users.UpdateProfileAsync(account).ConfigureAwait(False)

            Return If(saved,
                      ServiceResult.Ok("Your profile has been saved."),
                      ServiceResult.Fail("Your profile could not be saved. Try again."))
        End Function

        ''' <inheritdoc/>
        Public Async Function GetSubjectsAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of StudentSubject))) _
            Implements IStudentService.GetSubjectsAsync

            Dim items As IList(Of StudentSubject) =
                Await _subjects.GetStudentSubjectsAsync(studentId).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of StudentSubject)).Ok(items)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>BR-05. The unique index is the real guard; this produces a readable message.</remarks>
        Public Async Function AddSubjectAsync(studentId As Integer,
                                              subjectId As Integer,
                                              studyLevelId As Integer) As Task(Of ServiceResult) _
            Implements IStudentService.AddSubjectAsync

            If studentId <= 0 Then Return ServiceResult.Fail("You are not signed in.")

            If subjectId <= 0 OrElse studyLevelId <= 0 Then
                Return ServiceResult.Fail("Choose both a subject and a study level.")
            End If

            ' Part 12.1. The dropdown only offers active subjects, but that is presentation -
            ' a post naming a deactivated subject would otherwise be accepted.
            If Not Await _subjects.IsSubjectActiveAsync(subjectId).ConfigureAwait(False) Then
                Return ServiceResult.Fail("That subject is no longer offered.")
            End If

            If Await _subjects.StudentSubjectExistsAsync(studentId, subjectId, studyLevelId).ConfigureAwait(False) Then
                Return ServiceResult.Fail("That subject and study level is already on your list.")
            End If

            Try
                Await _subjects.AddStudentSubjectAsync(studentId, subjectId, studyLevelId).ConfigureAwait(False)
                Return ServiceResult.Ok("Subject added.")

            Catch ex As Npgsql.PostgresException When TutorConnect.Data.DatabaseHelper.IsUniqueViolation(ex)
                ' Two submissions raced past the check above; the unique index caught it.
                Return ServiceResult.Fail("That subject and study level is already on your list.")
            End Try
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' Resolves the name to a subject - reusing an existing one where the name matches,
        ''' creating it otherwise - then hands off to the id overload above, so BR-05, the
        ''' active-subject gate and the unique-index race catch all still apply exactly as
        ''' they do when the subject was chosen from the dropdown.
        ''' </remarks>
        Public Async Function AddSubjectByNameAsync(studentId As Integer,
                                                    actingPersonId As Integer,
                                                    subjectName As String,
                                                    studyLevelId As Integer) As Task(Of ServiceResult) _
            Implements IStudentService.AddSubjectByNameAsync

            If studentId <= 0 Then Return ServiceResult.Fail("You are not signed in.")

            If studyLevelId <= 0 Then
                Return ServiceResult.Fail("Choose a study level for the new subject.")
            End If

            Dim resolved As ServiceResult(Of SubjectOutcome) = Await _catalogue.EnsureSubjectAsync(
                actingPersonId, UserRole.Student, subjectName).ConfigureAwait(False)

            If Not resolved.Success Then Return ServiceResult.Fail(resolved.Message)

            Dim added As ServiceResult = Await AddSubjectAsync(
                studentId, resolved.Data.SubjectId, studyLevelId).ConfigureAwait(False)

            If Not added.Success Then Return added

            ' The catalogue's message is the informative half - it says whether the subject
            ' was created or matched to one that already existed.
            Return ServiceResult.Ok(resolved.Message)
        End Function

        ''' <inheritdoc/>
        Public Async Function RemoveSubjectAsync(studentId As Integer,
                                                 studentSubjectId As Integer) As Task(Of ServiceResult) _
            Implements IStudentService.RemoveSubjectAsync

            ' Scoped by studentId in the DELETE itself, so a tampered id removes nothing.
            Dim removed As Boolean =
                Await _subjects.RemoveStudentSubjectAsync(studentSubjectId, studentId).ConfigureAwait(False)

            Return If(removed,
                      ServiceResult.Ok("Subject removed."),
                      ServiceResult.Fail("That subject is not on your list."))
        End Function

    End Class

End Namespace

