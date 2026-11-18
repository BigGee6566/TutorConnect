Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports TutorConnect.Models

Namespace Data

    ''' <summary>
    ''' Tutor search, approval, and published availability.
    ''' </summary>
    Public Class TutorRepository

#Region "Search"

        ''' <summary>
        ''' The "Available Tutors" table. One row per TutorSubject match, paged server-side.
        ''' </summary>
        ''' <remarks>
        ''' BR-26 is enforced in the WHERE clause, not by the caller: an unapproved or
        ''' deactivated tutor can never appear in a search result, whatever the filters say.
        ''' </remarks>
        Public Async Function SearchAsync(subjectId As Integer?,
                                          studyLevelId As Integer?,
                                          qualificationContains As String,
                                          minimumExperience As Integer?,
                                          pageNumber As Integer,
                                          pageSize As Integer) As Task(Of IList(Of TutorSearchResult))
            Const sql As String =
                "SELECT ts.TutorSubjectId, t.TutorId, p.PersonId, " &
                "       p.FirstName + ' ' + p.LastName AS FullName, " &
                "       sub.SubjectName, sl.StudyLevelName, " &
                "       t.Qualification, t.ExperienceYears, t.RatePerSession, " &
                "       r.AverageRating, ISNULL(r.ReviewCount, 0) AS ReviewCount " &
                "FROM dbo.TutorSubject AS ts " &
                "JOIN dbo.Tutor      AS t   ON t.TutorId = ts.TutorId " &
                "JOIN dbo.Person     AS p   ON p.PersonId = t.PersonId " &
                "JOIN dbo.[Subject]  AS sub ON sub.SubjectId = ts.SubjectId " &
                "JOIN dbo.StudyLevel AS sl  ON sl.StudyLevelId = ts.StudyLevelId " &
                "OUTER APPLY (" &
                "    SELECT AVG(CAST(rv.Rating AS DECIMAL(3,2))) AS AverageRating, COUNT(*) AS ReviewCount " &
                "    FROM dbo.Review AS rv " &
                "    JOIN dbo.Booking        AS b  ON b.BookingId = rv.BookingId " &
                "    JOIN dbo.SessionRequest AS rq ON rq.RequestId = b.RequestId " &
                "    JOIN dbo.TutorSubject   AS ts2 ON ts2.TutorSubjectId = rq.TutorSubjectId " &
                "    WHERE ts2.TutorId = t.TutorId AND rv.IsHidden = 0) AS r " &
                "WHERE p.IsActive = 1 AND t.IsApproved = 1 " &
                "  AND (@SubjectId    IS NULL OR ts.SubjectId = @SubjectId) " &
                "  AND (@StudyLevelId IS NULL OR ts.StudyLevelId = @StudyLevelId) " &
                "  AND (@Qualification IS NULL OR t.Qualification LIKE '%' + @Qualification + '%') " &
                "  AND (@MinExperience IS NULL OR t.ExperienceYears >= @MinExperience) " &
                "ORDER BY p.LastName, p.FirstName, sub.SubjectName " &
                "OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;"

            Dim results As New List(Of TutorSearchResult)()
            Dim skip As Integer = Math.Max(0, (pageNumber - 1) * pageSize)

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    AddSearchParameters(command, subjectId, studyLevelId, qualificationContains, minimumExperience)
                    DatabaseHelper.AddParameter(command, "@Skip", SqlDbType.Int, skip)
                    DatabaseHelper.AddParameter(command, "@Take", SqlDbType.Int, pageSize)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New TutorSearchResult With {
                                .TutorSubjectId = DatabaseHelper.GetInt32(reader, "TutorSubjectId"),
                                .TutorId = DatabaseHelper.GetInt32(reader, "TutorId"),
                                .PersonId = DatabaseHelper.GetInt32(reader, "PersonId"),
                                .FullName = DatabaseHelper.GetString(reader, "FullName"),
                                .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                                .StudyLevelName = DatabaseHelper.GetString(reader, "StudyLevelName"),
                                .Qualification = DatabaseHelper.GetString(reader, "Qualification"),
                                .ExperienceYears = DatabaseHelper.GetInt32(reader, "ExperienceYears"),
                                .RatePerSession = DatabaseHelper.GetDecimal(reader, "RatePerSession"),
                                .AverageRating = DatabaseHelper.GetNullableDecimal(reader, "AverageRating"),
                                .ReviewCount = DatabaseHelper.GetInt32(reader, "ReviewCount")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        ''' <summary>Total matching rows, for the pager. Uses the same filters as SearchAsync.</summary>
        Public Async Function SearchCountAsync(subjectId As Integer?,
                                               studyLevelId As Integer?,
                                               qualificationContains As String,
                                               minimumExperience As Integer?) As Task(Of Integer)
            Const sql As String =
                "SELECT COUNT(*) " &
                "FROM dbo.TutorSubject AS ts " &
                "JOIN dbo.Tutor  AS t ON t.TutorId = ts.TutorId " &
                "JOIN dbo.Person AS p ON p.PersonId = t.PersonId " &
                "WHERE p.IsActive = 1 AND t.IsApproved = 1 " &
                "  AND (@SubjectId    IS NULL OR ts.SubjectId = @SubjectId) " &
                "  AND (@StudyLevelId IS NULL OR ts.StudyLevelId = @StudyLevelId) " &
                "  AND (@Qualification IS NULL OR t.Qualification LIKE '%' + @Qualification + '%') " &
                "  AND (@MinExperience IS NULL OR t.ExperienceYears >= @MinExperience);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    AddSearchParameters(command, subjectId, studyLevelId, qualificationContains, minimumExperience)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        Private Shared Sub AddSearchParameters(command As SqlCommand,
                                               subjectId As Integer?,
                                               studyLevelId As Integer?,
                                               qualificationContains As String,
                                               minimumExperience As Integer?)
            DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, DatabaseHelper.ToDbValue(subjectId))
            DatabaseHelper.AddParameter(command, "@StudyLevelId", SqlDbType.Int, DatabaseHelper.ToDbValue(studyLevelId))
            DatabaseHelper.AddParameter(command, "@Qualification", SqlDbType.NVarChar, 100,
                                        DatabaseHelper.ToDbValue(qualificationContains))
            DatabaseHelper.AddParameter(command, "@MinExperience", SqlDbType.Int, DatabaseHelper.ToDbValue(minimumExperience))
        End Sub

        ''' <summary>
        ''' The tutor detail screen. Returns Nothing when the TutorSubject does not exist or
        ''' its tutor is not approved and active - a student must not reach a hidden tutor
        ''' by guessing an id.
        ''' </summary>
        Public Async Function GetSearchResultAsync(tutorSubjectId As Integer) As Task(Of TutorSearchResult)
            Const sql As String =
                "SELECT ts.TutorSubjectId, t.TutorId, p.PersonId, " &
                "       p.FirstName + ' ' + p.LastName AS FullName, " &
                "       sub.SubjectName, sl.StudyLevelName, " &
                "       t.Qualification, t.ExperienceYears, t.RatePerSession, t.Bio, " &
                "       NULL AS AverageRating, 0 AS ReviewCount " &
                "FROM dbo.TutorSubject AS ts " &
                "JOIN dbo.Tutor      AS t   ON t.TutorId = ts.TutorId " &
                "JOIN dbo.Person     AS p   ON p.PersonId = t.PersonId " &
                "JOIN dbo.[Subject]  AS sub ON sub.SubjectId = ts.SubjectId " &
                "JOIN dbo.StudyLevel AS sl  ON sl.StudyLevelId = ts.StudyLevelId " &
                "WHERE ts.TutorSubjectId = @TutorSubjectId AND p.IsActive = 1 AND t.IsApproved = 1;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then Return Nothing

                        Return New TutorSearchResult With {
                            .TutorSubjectId = DatabaseHelper.GetInt32(reader, "TutorSubjectId"),
                            .TutorId = DatabaseHelper.GetInt32(reader, "TutorId"),
                            .PersonId = DatabaseHelper.GetInt32(reader, "PersonId"),
                            .FullName = DatabaseHelper.GetString(reader, "FullName"),
                            .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                            .StudyLevelName = DatabaseHelper.GetString(reader, "StudyLevelName"),
                            .Qualification = DatabaseHelper.GetString(reader, "Qualification"),
                            .ExperienceYears = DatabaseHelper.GetInt32(reader, "ExperienceYears"),
                            .RatePerSession = DatabaseHelper.GetDecimal(reader, "RatePerSession"),
                            .Bio = DatabaseHelper.GetStringOrNothing(reader, "Bio"),
                            .AverageRating = DatabaseHelper.GetNullableDecimal(reader, "AverageRating"),
                            .ReviewCount = DatabaseHelper.GetInt32(reader, "ReviewCount")
                        }
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>
        ''' The PersonId behind a TutorSubject, for addressing a notification.
        ''' </summary>
        ''' <remarks>
        ''' Unlike the search queries this does not filter on approval or active state: a
        ''' tutor still needs to be told about activity on their account even while their
        ''' approval is pending. Returns 0 when the TutorSubject does not exist.
        ''' </remarks>
        Public Async Function GetPersonIdForTutorSubjectAsync(tutorSubjectId As Integer) As Task(Of Integer)
            Const sql As String =
                "SELECT t.PersonId FROM dbo.TutorSubject AS ts " &
                "JOIN dbo.Tutor AS t ON t.TutorId = ts.TutorId " &
                "WHERE ts.TutorSubjectId = @TutorSubjectId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)

                    If scalar Is Nothing OrElse Convert.IsDBNull(scalar) Then Return 0
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>The PersonId behind a StudentId, for addressing a notification.</summary>
        Public Async Function GetPersonIdForStudentAsync(studentId As Integer) As Task(Of Integer)
            Const sql As String = "SELECT PersonId FROM dbo.Student WHERE StudentId = @StudentId;"
            Return Await ScalarIdAsync(sql, "@StudentId", studentId).ConfigureAwait(False)
        End Function

        ''' <summary>The PersonId behind a TutorId, for addressing a notification.</summary>
        Public Async Function GetPersonIdForTutorAsync(tutorId As Integer) As Task(Of Integer)
            Const sql As String = "SELECT PersonId FROM dbo.Tutor WHERE TutorId = @TutorId;"
            Return Await ScalarIdAsync(sql, "@TutorId", tutorId).ConfigureAwait(False)
        End Function

        ''' <summary>
        ''' The StudentId for a signed-in person, or 0 when they are not a student.
        ''' </summary>
        ''' <remarks>
        ''' Takes the identity from the caller, which takes it from the authentication
        ''' ticket - never from a form field or the URL (BR-24).
        ''' </remarks>
        Public Async Function GetStudentIdForPersonAsync(personId As Integer) As Task(Of Integer)
            Const sql As String = "SELECT StudentId FROM dbo.Student WHERE PersonId = @PersonId;"
            Return Await ScalarIdAsync(sql, "@PersonId", personId).ConfigureAwait(False)
        End Function

        ''' <summary>The TutorId for a signed-in person, or 0 when they are not a tutor.</summary>
        Public Async Function GetTutorIdForPersonAsync(personId As Integer) As Task(Of Integer)
            Const sql As String = "SELECT TutorId FROM dbo.Tutor WHERE PersonId = @PersonId;"
            Return Await ScalarIdAsync(sql, "@PersonId", personId).ConfigureAwait(False)
        End Function

        ''' <summary>One integer by one integer, the shape every lookup above shares.</summary>
        Private Shared Async Function ScalarIdAsync(sql As String,
                                                    parameterName As String,
                                                    value As Integer) As Task(Of Integer)
            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, parameterName, SqlDbType.Int, value)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)

                    If scalar Is Nothing OrElse Convert.IsDBNull(scalar) Then Return 0
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

#End Region

#Region "Profile and approval"

        Public Async Function UpdateTutorDetailsAsync(tutor As TutorProfile) As Task(Of Boolean)
            If tutor Is Nothing Then Throw New ArgumentNullException(NameOf(tutor))

            Const sql As String =
                "UPDATE dbo.Tutor SET Qualification = @Qualification, ExperienceYears = @ExperienceYears, " &
                "RatePerSession = @RatePerSession, Bio = @Bio WHERE TutorId = @TutorId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@Qualification", SqlDbType.NVarChar, 100, tutor.Qualification)
                    DatabaseHelper.AddParameter(command, "@ExperienceYears", SqlDbType.Int, tutor.ExperienceYears)
                    DatabaseHelper.AddDecimalParameter(command, "@RatePerSession", tutor.RatePerSession)
                    DatabaseHelper.AddParameter(command, "@Bio", SqlDbType.NVarChar, 1000, DatabaseHelper.ToDbValue(tutor.Bio))
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutor.TutorId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        ''' <summary>AT-23. An approved tutor appears in Find Tutor immediately.</summary>
        Public Async Function SetApprovedAsync(tutorId As Integer, isApproved As Boolean) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.Tutor SET IsApproved = @IsApproved, " &
                "ApprovedAt = CASE WHEN @IsApproved = 1 THEN SYSUTCDATETIME() ELSE NULL END " &
                "WHERE TutorId = @TutorId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@IsApproved", SqlDbType.Bit, isApproved)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

#End Region

#Region "Availability"

        Private Const AvailabilityColumns As String =
            "a.AvailabilityId, a.TutorSubjectId, a.AvailabilityDate, a.StartTime, a.EndTime, " &
            "a.IsBooked, a.CreatedAt, sub.SubjectName, sl.StudyLevelName"

        Private Const AvailabilityJoins As String =
            "FROM dbo.TutorAvailability AS a " &
            "JOIN dbo.TutorSubject AS ts  ON ts.TutorSubjectId = a.TutorSubjectId " &
            "JOIN dbo.[Subject]    AS sub ON sub.SubjectId = ts.SubjectId " &
            "JOIN dbo.StudyLevel   AS sl  ON sl.StudyLevelId = ts.StudyLevelId "

        ''' <summary>Every slot a tutor has published, newest date last.</summary>
        Public Async Function GetAvailabilityForTutorAsync(tutorId As Integer) As Task(Of IList(Of TutorAvailability))
            Const sql As String =
                "SELECT " & AvailabilityColumns & " " & AvailabilityJoins &
                "WHERE ts.TutorId = @TutorId " &
                "ORDER BY a.AvailabilityDate, a.StartTime;"

            Return Await ReadAvailabilityAsync(sql, Sub(command)
                                                        DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                                                    End Sub).ConfigureAwait(False)
        End Function

        ''' <summary>
        ''' Future, unbooked slots for one TutorSubject - what the student picks from on the
        ''' tutor detail screen.
        ''' </summary>
        Public Async Function GetOpenAvailabilityAsync(tutorSubjectId As Integer) As Task(Of IList(Of TutorAvailability))
            Const sql As String =
                "SELECT " & AvailabilityColumns & " " & AvailabilityJoins &
                "WHERE a.TutorSubjectId = @TutorSubjectId " &
                "  AND a.IsBooked = 0 " &
                "  AND a.AvailabilityDate >= CAST(GETDATE() AS DATE) " &
                "ORDER BY a.AvailabilityDate, a.StartTime;"

            Return Await ReadAvailabilityAsync(sql, Sub(command)
                                                        DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                                                    End Sub).ConfigureAwait(False)
        End Function

        Public Async Function GetAvailabilityByIdAsync(availabilityId As Integer) As Task(Of TutorAvailability)
            Const sql As String =
                "SELECT " & AvailabilityColumns & " " & AvailabilityJoins &
                "WHERE a.AvailabilityId = @AvailabilityId;"

            Dim rows As IList(Of TutorAvailability) =
                Await ReadAvailabilityAsync(sql, Sub(command)
                                                    DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, availabilityId)
                                                End Sub).ConfigureAwait(False)

            Return If(rows.Count = 0, Nothing, rows(0))
        End Function

        ''' <summary>
        ''' BR-20. True when the proposed slot overlaps one the tutor has already published.
        ''' Half-open comparison: a slot ending at 12:00 does not clash with one starting
        ''' at 12:00.
        ''' </summary>
        Public Async Function HasOverlappingAvailabilityAsync(tutorId As Integer,
                                                              availabilityDate As Date,
                                                              startTime As TimeSpan,
                                                              endTime As TimeSpan,
                                                              Optional excludingAvailabilityId As Integer = 0) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.TutorAvailability AS a " &
                "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = a.TutorSubjectId " &
                "WHERE ts.TutorId = @TutorId " &
                "  AND a.AvailabilityDate = @AvailabilityDate " &
                "  AND a.AvailabilityId <> @ExcludingAvailabilityId " &
                "  AND a.StartTime < @EndTime AND @StartTime < a.EndTime;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    DatabaseHelper.AddParameter(command, "@AvailabilityDate", SqlDbType.Date, availabilityDate)
                    DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, startTime)
                    DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, endTime)
                    DatabaseHelper.AddParameter(command, "@ExcludingAvailabilityId", SqlDbType.Int, excludingAvailabilityId)

                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        Public Async Function AddAvailabilityAsync(tutorSubjectId As Integer,
                                                   availabilityDate As Date,
                                                   startTime As TimeSpan,
                                                   endTime As TimeSpan) As Task(Of Integer)
            Const sql As String =
                "INSERT INTO dbo.TutorAvailability (TutorSubjectId, AvailabilityDate, StartTime, EndTime) " &
                "VALUES (@TutorSubjectId, @AvailabilityDate, @StartTime, @EndTime); " &
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                    DatabaseHelper.AddParameter(command, "@AvailabilityDate", SqlDbType.Date, availabilityDate)
                    DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, startTime)
                    DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, endTime)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>BR-22. True when a Scheduled booking depends on this slot.</summary>
        Public Async Function AvailabilityBacksScheduledBookingAsync(availabilityId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.Booking " &
                "WHERE AvailabilityId = @AvailabilityId AND [Status] = 'Scheduled';"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, availabilityId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        ''' <summary>Scoped by TutorId so one tutor cannot delete another's slot (BR-24).</summary>
        Public Async Function DeleteAvailabilityAsync(availabilityId As Integer, tutorId As Integer) As Task(Of Boolean)
            Const sql As String =
                "DELETE a FROM dbo.TutorAvailability AS a " &
                "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = a.TutorSubjectId " &
                "WHERE a.AvailabilityId = @AvailabilityId AND ts.TutorId = @TutorId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, availabilityId)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        Private Shared Async Function ReadAvailabilityAsync(sql As String,
                                                            bindParameters As Action(Of SqlCommand)) As Task(Of IList(Of TutorAvailability))
            Dim results As New List(Of TutorAvailability)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    bindParameters(command)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New TutorAvailability With {
                                .AvailabilityId = DatabaseHelper.GetInt32(reader, "AvailabilityId"),
                                .TutorSubjectId = DatabaseHelper.GetInt32(reader, "TutorSubjectId"),
                                .AvailabilityDate = DatabaseHelper.GetDate(reader, "AvailabilityDate"),
                                .StartTime = DatabaseHelper.GetTime(reader, "StartTime"),
                                .EndTime = DatabaseHelper.GetTime(reader, "EndTime"),
                                .IsBooked = DatabaseHelper.GetBoolean(reader, "IsBooked"),
                                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt"),
                                .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                                .StudyLevelName = DatabaseHelper.GetString(reader, "StudyLevelName")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

#End Region

    End Class

End Namespace
