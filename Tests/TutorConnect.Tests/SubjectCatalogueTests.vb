Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Services
Imports TutorConnect.Validation

Namespace TutorConnect.Tests

    ''' <summary>
    ''' Students, tutors and administrators adding their own subjects.
    ''' </summary>
    ''' <remarks>
    ''' The test that matters most is <see cref="ATutorAndAStudentTypingTheSameNameShareOneSubject"/>.
    ''' Find Tutor matches on SubjectId equality, so two spellings of one subject are two
    ''' subjects that can never meet - free text would quietly break the core feature of the
    ''' application if the name were not normalised and reused.
    ''' </remarks>
    <TestClass>
    Public Class SubjectCatalogueTests

        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _subjects As New SubjectRepository()
        Private ReadOnly _catalogue As New SubjectCatalogueService()
        Private ReadOnly _students As New StudentService()
        Private ReadOnly _tutors As New TutorService()
        Private ReadOnly _admin As New AdminService()
        Private ReadOnly _search As New TutorSearchService()

        Private _scenario As TestScenario
        Private _administratorPersonId As Integer

        <TestInitialize>
        Public Sub Setup()
            SetupAsync().GetAwaiter().GetResult()
        End Sub

        Private Async Function SetupAsync() As Task
            _scenario = Await TestScenario.CreateAsync()
            _administratorPersonId = (Await _users.FindByUserNameOrEmailAsync("admin")).PersonId
        End Function

        ''' <summary>
        ''' Puts back any subject a test deactivated. Leaving one off would fail the next
        ''' test that tried to use it and turn one failure into several.
        ''' </summary>
        <TestCleanup>
        Public Sub Restore()
            TestDatabase.ExecuteAsync("UPDATE dbo.[Subject] SET IsActive = 1;").GetAwaiter().GetResult()
        End Sub

#Region "Reuse - the rule that protects Find Tutor"

        ''' <summary>
        ''' A tutor types "Accounting"; a student later types "accounting". They must land on
        ''' one SubjectId, or the student's search will never return that tutor.
        ''' </summary>
        <TestMethod>
        Public Async Function ATutorAndAStudentTypingTheSameNameShareOneSubject() As Task
            Dim name As String = "Accounting " & TestDatabase.UniqueSuffix()

            Dim byTutor = Await _catalogue.EnsureSubjectAsync(
                _scenario.TutorPerson.PersonId, UserRole.Tutor, name)

            Assert.IsTrue(byTutor.Success, byTutor.Message)
            Assert.IsTrue(byTutor.Data.WasCreated, "nothing matched, so it was created")

            Dim byStudent = Await _catalogue.EnsureSubjectAsync(
                _scenario.Student.PersonId, UserRole.Student, name.ToLowerInvariant())

            Assert.IsTrue(byStudent.Success, byStudent.Message)
            Assert.IsFalse(byStudent.Data.WasCreated, "the existing subject must be reused")
            Assert.AreEqual(byTutor.Data.SubjectId, byStudent.Data.SubjectId,
                            "a different id here means the two would never find each other")
        End Function

        ''' <summary>Leading, trailing and repeated internal spaces are all the same subject.</summary>
        <TestMethod>
        Public Async Function SpacingDifferencesResolveToOneSubject() As Task
            Dim suffix As String = TestDatabase.UniqueSuffix()

            Dim first = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, "Physical Science " & suffix)

            Dim second = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator,
                "   Physical    Science   " & suffix & "  ")

            Assert.IsTrue(first.Success, first.Message)
            Assert.IsTrue(second.Success, second.Message)
            Assert.AreEqual(first.Data.SubjectId, second.Data.SubjectId)
            Assert.IsFalse(second.Data.WasCreated)
        End Function

        ''' <summary>
        ''' The subject name is stored normalised, not as the first person happened to type it.
        ''' </summary>
        <TestMethod>
        Public Async Function TheStoredNameIsNormalised() As Task
            Dim suffix As String = TestDatabase.UniqueSuffix()

            Dim created = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, "  Business   Studies " & suffix & " ")

            Assert.IsTrue(created.Success, created.Message)
            Assert.AreEqual("Business Studies " & suffix, created.Data.SubjectName)
        End Function

#End Region

#Region "Validation"

        ''' <summary>
        ''' Refused, not truncated. Before SubjectValidator existed the SqlParameter's size
        ''' silently cut the name to 100 characters, so the person created a subject that was
        ''' not the one they had typed.
        ''' </summary>
        <TestMethod>
        Public Async Function AnOverLongNameIsRefusedRatherThanTruncated() As Task
            Dim tooLong As String = New String("a"c, SubjectValidator.MaximumNameLength + 1)

            Dim result = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, tooLong)

            Assert.IsFalse(result.Success, "an over-long name must be refused")
            Assert.IsNull(Await _subjects.FindByNameAsync(tooLong.Substring(0, SubjectValidator.MaximumNameLength)),
                          "and no truncated row may be left behind")
        End Function

        <TestMethod>
        Public Async Function ANameWithNoLettersIsRefused() As Task
            Dim result = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, "123")

            Assert.IsFalse(result.Success, "'123' is not a subject")
        End Function

        <TestMethod>
        Public Async Function AnEmptyNameIsRefused() As Task
            Dim blank = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, "   ")

            Assert.IsFalse(blank.Success)

            Dim missing = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, Nothing)

            Assert.IsFalse(missing.Success)
        End Function

        ''' <summary>Punctuation that appears in real subject names is accepted.</summary>
        <TestMethod>
        Public Async Function RealisticPunctuationIsAccepted() As Task
            Dim name As String = "Life & Physical Sciences (IT/Networking) " & TestDatabase.UniqueSuffix()

            Dim result = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, name)

            Assert.IsTrue(result.Success, result.Message)
        End Function

#End Region

#Region "Moderation"

        ''' <summary>
        ''' Deactivating a subject is a deliberate administrator decision (AT-25). Retyping
        ''' its name must not undo it.
        ''' </summary>
        <TestMethod>
        Public Async Function AWithdrawnSubjectCannotBeRecreatedByName() As Task
            Dim name As String = "Withdrawn " & TestDatabase.UniqueSuffix()

            Dim created = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, name)

            Assert.IsTrue(created.Success, created.Message)

            Await _admin.SetSubjectActiveAsync(_administratorPersonId, created.Data.SubjectId, False)

            Dim retried = Await _catalogue.EnsureSubjectAsync(
                _scenario.Student.PersonId, UserRole.Student, name)

            Assert.IsFalse(retried.Success, "a withdrawn subject must stay withdrawn")

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.[Subject] WHERE SubjectName = @p0;", name),
                "and no second row may be created under the same name")
        End Function

        ''' <summary>
        ''' The audit entry names the student, not "the system". ActorRole 'Student' is
        ''' already permitted by CK_AuditLog_ActorRole.
        ''' </summary>
        <TestMethod>
        Public Async Function ASubjectCreatedByAStudentIsAuditedAsAStudent() As Task
            Dim name As String = "Audited " & TestDatabase.UniqueSuffix()

            Dim created = Await _catalogue.EnsureSubjectAsync(
                _scenario.Student.PersonId, UserRole.Student, name)

            Assert.IsTrue(created.Success, created.Message)

            Assert.AreEqual(1, Await TestDatabase.ScalarAsync(
                "SELECT COUNT(*) FROM dbo.AuditLog " &
                "WHERE EntityType = 'Subject' AND EntityId = @p0 " &
                "  AND ActionType = 'SubjectCreated' AND ActorRole = 'Student' " &
                "  AND ActorPersonId = @p1;",
                created.Data.SubjectId, _scenario.Student.PersonId))
        End Function

        ''' <summary>
        ''' For the administrator, "it already exists" is still a refusal: the intent there was
        ''' to add a new subject, not to be quietly attached to an old one.
        ''' </summary>
        <TestMethod>
        Public Async Function TheAdministratorIsToldWhenASubjectAlreadyExists() As Task
            Dim result = Await _admin.AddSubjectAsync(_administratorPersonId, "Mathematics")

            Assert.IsFalse(result.Success, "Mathematics is seeded")
        End Function

#End Region

#Region "End to end through the member services"

        ''' <summary>
        ''' A tutor adds a subject nobody has listed before and becomes findable under it.
        ''' </summary>
        <TestMethod>
        Public Async Function ATutorCanAddANewSubjectAndBeFoundUnderIt() As Task
            Dim name As String = "Econometrics " & TestDatabase.UniqueSuffix()
            Dim levelId As Integer = Await TestDatabase.ScalarAsync(
                "SELECT TOP 1 StudyLevelId FROM dbo.StudyLevel ORDER BY SortOrder;")

            Dim added = Await _tutors.AddSubjectByNameAsync(
                _scenario.TutorId, _scenario.TutorPerson.PersonId, name, levelId)

            Assert.IsTrue(added.Success, added.Message)

            Dim subject = Await _subjects.FindByNameAsync(name)
            Assert.IsNotNull(subject, "the subject is in the catalogue")

            Dim found = (Await _search.SearchAsync(subject.SubjectId, Nothing, Nothing, Nothing, 1, 200)).Data
            Assert.IsTrue(found.Any(Function(r) r.TutorId = _scenario.TutorId),
                          "the tutor must be findable under the subject they just added")
        End Function

        <TestMethod>
        Public Async Function AStudentCanAddANewSubjectToTheirList() As Task
            Dim name As String = "Statistics " & TestDatabase.UniqueSuffix()
            Dim levelId As Integer = Await TestDatabase.ScalarAsync(
                "SELECT TOP 1 StudyLevelId FROM dbo.StudyLevel ORDER BY SortOrder;")

            Dim added = Await _students.AddSubjectByNameAsync(
                _scenario.StudentId, _scenario.Student.PersonId, name, levelId)

            Assert.IsTrue(added.Success, added.Message)

            Dim mine = (Await _students.GetSubjectsAsync(_scenario.StudentId)).Data
            Assert.IsTrue(mine.Any(Function(s) s.SubjectName = name),
                          "the new subject is on the student's list")
        End Function

        ''' <summary>
        ''' The tutor path had no active-subject check while the student path did, so a post
        ''' naming a withdrawn subject was accepted - and because the Find Tutor query does
        ''' not filter on Subject.IsActive, that subject went straight back into the results.
        ''' </summary>
        <TestMethod>
        Public Async Function ATutorCannotAddAWithdrawnSubjectById() As Task
            Dim name As String = "Retired " & TestDatabase.UniqueSuffix()
            Dim levelId As Integer = Await TestDatabase.ScalarAsync(
                "SELECT TOP 1 StudyLevelId FROM dbo.StudyLevel ORDER BY SortOrder;")

            Dim created = Await _catalogue.EnsureSubjectAsync(
                _administratorPersonId, UserRole.Administrator, name)

            Await _admin.SetSubjectActiveAsync(_administratorPersonId, created.Data.SubjectId, False)

            Dim added = Await _tutors.AddSubjectAsync(
                _scenario.TutorId, created.Data.SubjectId, levelId)

            Assert.IsFalse(added.Success, "a withdrawn subject must not be teachable")
        End Function

#End Region

    End Class

End Namespace
