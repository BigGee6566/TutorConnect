Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports System.Web
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Security

Namespace Seed

    ''' <summary>
    ''' Creates the development accounts, hashing their passwords at run time (D-07).
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' No password literal appears in any .sql file in this repository, and none appears
    ''' here either. The development password is read from an appSettings key supplied by
    ''' a git-ignored local file; if that key is absent the seeder refuses to run rather
    ''' than inventing a default.
    ''' </para>
    ''' <para>
    ''' Safety guard: when running inside a web request the seeder refuses unless the
    ''' application is compiled with debug="true". Outside a web request - a console test
    ''' harness or a unit test - there is no served endpoint to abuse, so it is allowed.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class SeedRunner

        ''' <summary>appSettings key holding the development password.</summary>
        Public Const DevPasswordKey As String = "Seed:DevPassword"

        Private ReadOnly _users As New UserRepository()

        ''' <summary>Lines describing what the last run did, for display or logging.</summary>
        Public ReadOnly Property Log As New List(Of String)()

        ''' <summary>
        ''' Seeds the four required accounts, and optionally the demo data that makes the
        ''' Find Tutor and Requests screens look like the design mockup.
        ''' </summary>
        Public Async Function RunAsync(Optional includeDemoData As Boolean = True) As Task
            GuardEnvironment()

            Dim password As String = ConfigurationManager.AppSettings(DevPasswordKey)
            If String.IsNullOrWhiteSpace(password) Then
                Throw New ConfigurationErrorsException(
                    $"appSettings key '{DevPasswordKey}' is not set. Add it to the git-ignored " &
                    "local settings file. The seeder will not invent a password.")
            End If

            Await EnsureAccountAsync("admin", "System", "Administrator", "Other",
                                     "admin@tutorconnect.ac.za", "0800000000", "East London",
                                     UserRole.Administrator, password, Nothing, Nothing).ConfigureAwait(False)

            Await EnsureAccountAsync("zanele_k", "Zanele", "Khumalo", "Female",
                                     "zanele@example.com", "0821234567", "East London",
                                     UserRole.Student, password, "STU2026001", Nothing).ConfigureAwait(False)

            Await EnsureAccountAsync("bongani_z", "Bongani", "Zulu", "Male",
                                     "bongani@example.com", "0839876543", "Komani",
                                     UserRole.Tutor, password, Nothing,
                                     NewTutor("BSc Computer Science", 5, 150D,
                                              "I am passionate about helping students understand complex IT " &
                                              "concepts. I have 5 years of tutoring experience.")).ConfigureAwait(False)

            ' A second student exists so the BR-12 overlap rule can be tested.
            Await EnsureAccountAsync("john_doe", "John", "Doe", "Male",
                                     "john@gmail.com", "0721112222", "Mthatha",
                                     UserRole.Student, password, "STU2026002", Nothing).ConfigureAwait(False)

            Await ApproveTutorAsync("bongani_z").ConfigureAwait(False)

            If includeDemoData Then
                Await SeedDemoDataAsync(password).ConfigureAwait(False)
            End If
        End Function

        ''' <summary>
        ''' Extra tutors, students, subjects taught and published slots.
        ''' </summary>
        ''' <remarks>
        ''' The original seed data contains one tutor, which leaves Find Tutor showing a
        ''' single row and Student Requests empty. The mockup shows three tutors and four
        ''' students, so the demo set fills the screens out enough to be reviewable.
        ''' </remarks>
        Private Async Function SeedDemoDataAsync(password As String) As Task
            Await EnsureAccountAsync("lerato_m", "Lerato", "Mbeki", "Female",
                                     "lerato@gmail.com", "0834445555", "East London",
                                     UserRole.Tutor, password, Nothing,
                                     NewTutor("BSc IT", 3, 120D,
                                              "I enjoy breaking difficult topics down into simple steps.")).ConfigureAwait(False)

            Await EnsureAccountAsync("andile_d", "Andile", "Dlamini", "Male",
                                     "andile@gmail.com", "0836667777", "Mthatha",
                                     UserRole.Tutor, password, Nothing,
                                     NewTutor("BSc Computer Science", 4, 130D,
                                              "Patient, practical tutoring with plenty of worked examples.")).ConfigureAwait(False)

            Await ApproveTutorAsync("lerato_m").ConfigureAwait(False)
            Await ApproveTutorAsync("andile_d").ConfigureAwait(False)

            Await EnsureAccountAsync("akhona_n", "Akhona", "Ndlela", "Female",
                                     "akhona@gmail.com", "0728889999", "East London",
                                     UserRole.Student, password, "STU2026003", Nothing).ConfigureAwait(False)

            Await EnsureAccountAsync("luvo_m", "Luvo", "Mzizi", "Male",
                                     "luvo@gmail.com", "0727778888", "Komani",
                                     UserRole.Student, password, "STU2026004", Nothing).ConfigureAwait(False)

            Await EnsureAccountAsync("thabo_j", "Thabo", "Jonas", "Male",
                                     "thabo@gmail.com", "0726665555", "Mthatha",
                                     UserRole.Student, password, "STU2026005", Nothing).ConfigureAwait(False)

            ' Subjects taught, then slots against them.
            Await EnsureTutorSubjectAsync("bongani_z", "Information Systems", "Third Year").ConfigureAwait(False)
            Await EnsureTutorSubjectAsync("lerato_m", "Information Systems", "Third Year").ConfigureAwait(False)
            Await EnsureTutorSubjectAsync("andile_d", "Information Systems", "Third Year").ConfigureAwait(False)
            Await EnsureTutorSubjectAsync("andile_d", "Mathematics", "Second Year").ConfigureAwait(False)

            ' Future slots only, so BR-07 and BR-11 stay satisfiable in a demo.
            Dim day1 As Date = Date.Today.AddDays(3)
            Dim day2 As Date = Date.Today.AddDays(4)

            Await EnsureAvailabilityAsync("bongani_z", "Information Systems", "Third Year", day1, "10:00", "12:00").ConfigureAwait(False)
            Await EnsureAvailabilityAsync("bongani_z", "Information Systems", "Third Year", day1, "14:00", "16:00").ConfigureAwait(False)
            Await EnsureAvailabilityAsync("bongani_z", "Information Systems", "Third Year", day2, "09:00", "11:00").ConfigureAwait(False)
            Await EnsureAvailabilityAsync("lerato_m", "Information Systems", "Third Year", day1, "13:00", "15:00").ConfigureAwait(False)
            Await EnsureAvailabilityAsync("andile_d", "Information Systems", "Third Year", day2, "10:00", "12:00").ConfigureAwait(False)
        End Function

        Private Shared Function NewTutor(qualification As String,
                                         experienceYears As Integer,
                                         ratePerSession As Decimal,
                                         bio As String) As TutorProfile
            Return New TutorProfile With {
                .Qualification = qualification,
                .ExperienceYears = experienceYears,
                .RatePerSession = ratePerSession,
                .Bio = bio
            }
        End Function

        ''' <summary>
        ''' Creates the account when it does not exist; otherwise resets its password so a
        ''' re-run always leaves a usable set of credentials.
        ''' </summary>
        Private Async Function EnsureAccountAsync(userName As String,
                                                  firstName As String,
                                                  lastName As String,
                                                  gender As String,
                                                  email As String,
                                                  cellNumber As String,
                                                  location As String,
                                                  role As UserRole,
                                                  password As String,
                                                  studentNo As String,
                                                  tutorDetails As TutorProfile) As Task
            Dim hash As Byte() = Nothing, salt As Byte() = Nothing, iterations As Integer
            PasswordHelper.CreateHash(password, hash, salt, iterations)

            Dim existing As Person = Await _users.FindByUserNameOrEmailAsync(userName).ConfigureAwait(False)

            If existing IsNot Nothing Then
                Await _users.SetPasswordAsync(existing.PersonId, hash, salt, iterations).ConfigureAwait(False)
                Log.Add($"{userName}: already existed, password reset ({role}).")
                Return
            End If

            Dim account As New Person With {
                .UserName = userName,
                .FirstName = firstName,
                .LastName = lastName,
                .Gender = gender,
                .EmailAddress = email,
                .CellNumber = cellNumber,
                .Location = location,
                .Role = role,
                .IsActive = True
            }

            Dim personId As Integer = Await _users.CreateAccountAsync(
                account, hash, salt, iterations, studentNo, tutorDetails).ConfigureAwait(False)

            Log.Add($"{userName}: created as {role} (PersonId {personId}).")
        End Function

        Private Async Function ApproveTutorAsync(userName As String) As Task
            Const sql As String =
                "UPDATE t SET t.IsApproved = 1, t.ApprovedAt = SYSUTCDATETIME() " &
                "FROM dbo.Tutor AS t JOIN dbo.Person AS p ON p.PersonId = t.PersonId " &
                "WHERE p.UserName = @UserName AND t.IsApproved = 0;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@UserName", SqlDbType.VarChar, 50, userName)
                    Dim affected As Integer = Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                    If affected > 0 Then Log.Add($"{userName}: tutor approved.")
                End Using
            End Using
        End Function

        Private Async Function EnsureTutorSubjectAsync(userName As String,
                                                       subjectName As String,
                                                       studyLevelName As String) As Task
            Const sql As String =
                "INSERT INTO dbo.TutorSubject (TutorId, SubjectId, StudyLevelId) " &
                "SELECT t.TutorId, s.SubjectId, l.StudyLevelId " &
                "FROM dbo.Tutor AS t " &
                "JOIN dbo.Person AS p ON p.PersonId = t.PersonId " &
                "CROSS JOIN dbo.[Subject] AS s " &
                "CROSS JOIN dbo.StudyLevel AS l " &
                "WHERE p.UserName = @UserName AND s.SubjectName = @SubjectName " &
                "  AND l.StudyLevelName = @StudyLevelName " &
                "  AND NOT EXISTS (SELECT 1 FROM dbo.TutorSubject AS ts " &
                "                  WHERE ts.TutorId = t.TutorId AND ts.SubjectId = s.SubjectId " &
                "                    AND ts.StudyLevelId = l.StudyLevelId);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@UserName", SqlDbType.VarChar, 50, userName)
                    DatabaseHelper.AddParameter(command, "@SubjectName", SqlDbType.NVarChar, 100, subjectName)
                    DatabaseHelper.AddParameter(command, "@StudyLevelName", SqlDbType.NVarChar, 50, studyLevelName)
                    Dim affected As Integer = Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                    If affected > 0 Then Log.Add($"{userName}: teaches {subjectName} ({studyLevelName}).")
                End Using
            End Using
        End Function

        Private Async Function EnsureAvailabilityAsync(userName As String,
                                                       subjectName As String,
                                                       studyLevelName As String,
                                                       availabilityDate As Date,
                                                       startTime As String,
                                                       endTime As String) As Task
            Const sql As String =
                "INSERT INTO dbo.TutorAvailability (TutorSubjectId, AvailabilityDate, StartTime, EndTime) " &
                "SELECT ts.TutorSubjectId, @AvailabilityDate, @StartTime, @EndTime " &
                "FROM dbo.TutorSubject AS ts " &
                "JOIN dbo.Tutor      AS t ON t.TutorId = ts.TutorId " &
                "JOIN dbo.Person     AS p ON p.PersonId = t.PersonId " &
                "JOIN dbo.[Subject]  AS s ON s.SubjectId = ts.SubjectId " &
                "JOIN dbo.StudyLevel AS l ON l.StudyLevelId = ts.StudyLevelId " &
                "WHERE p.UserName = @UserName AND s.SubjectName = @SubjectName " &
                "  AND l.StudyLevelName = @StudyLevelName " &
                "  AND NOT EXISTS (SELECT 1 FROM dbo.TutorAvailability AS a " &
                "                  WHERE a.TutorSubjectId = ts.TutorSubjectId " &
                "                    AND a.AvailabilityDate = @AvailabilityDate " &
                "                    AND a.StartTime = @StartTime);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@UserName", SqlDbType.VarChar, 50, userName)
                    DatabaseHelper.AddParameter(command, "@SubjectName", SqlDbType.NVarChar, 100, subjectName)
                    DatabaseHelper.AddParameter(command, "@StudyLevelName", SqlDbType.NVarChar, 50, studyLevelName)
                    DatabaseHelper.AddParameter(command, "@AvailabilityDate", SqlDbType.Date, availabilityDate)
                    DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, TimeSpan.Parse(startTime))
                    DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, TimeSpan.Parse(endTime))
                    Dim affected As Integer = Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                    If affected > 0 Then
                        Log.Add($"{userName}: slot {availabilityDate:dd/MM/yyyy} {startTime}-{endTime}.")
                    End If
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Refuses to run from a non-debug web application. See the class remarks.
        ''' </summary>
        Private Shared Sub GuardEnvironment()
            Dim context As HttpContext = HttpContext.Current
            If context Is Nothing Then Return

            If Not context.IsDebuggingEnabled Then
                Throw New InvalidOperationException(
                    "The seeder only runs when the application is compiled with debug=""true"".")
            End If
        End Sub

    End Class

End Namespace
