Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports TutorConnect.Models

Namespace Data

    ''' <summary>
    ''' Subjects, study levels, and the two link tables that attach them to students and
    ''' tutors.
    ''' </summary>
    Public Class SubjectRepository

#Region "Lookups"

        ''' <summary>Subjects for a dropdown. Inactive subjects are excluded by default.</summary>
        Public Async Function GetSubjectsAsync(Optional includeInactive As Boolean = False) As Task(Of IList(Of Subject))
            Const sql As String =
                "SELECT SubjectId, SubjectName, IsActive FROM dbo.[Subject] " &
                "WHERE (@IncludeInactive = 1 OR IsActive = 1) ORDER BY SubjectName;"

            Dim results As New List(Of Subject)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@IncludeInactive", SqlDbType.Bit, includeInactive)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New Subject With {
                                .SubjectId = DatabaseHelper.GetInt32(reader, "SubjectId"),
                                .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                                .IsActive = DatabaseHelper.GetBoolean(reader, "IsActive")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        ''' <summary>Study levels in teaching order, not alphabetical order.</summary>
        Public Async Function GetStudyLevelsAsync() As Task(Of IList(Of StudyLevel))
            Const sql As String =
                "SELECT StudyLevelId, StudyLevelName, SortOrder FROM dbo.StudyLevel ORDER BY SortOrder;"

            Dim results As New List(Of StudyLevel)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New StudyLevel With {
                                .StudyLevelId = DatabaseHelper.GetInt32(reader, "StudyLevelId"),
                                .StudyLevelName = DatabaseHelper.GetString(reader, "StudyLevelName"),
                                .SortOrder = DatabaseHelper.GetInt32(reader, "SortOrder")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        ''' <summary>
        ''' Finds a subject by name, or Nothing. Used to reuse an existing subject rather
        ''' than create a second row for the same thing.
        ''' </summary>
        ''' <remarks>
        ''' The default collation is case-insensitive and ignores trailing spaces, so this
        ''' matches "maths" and "Maths " to "Maths" with no extra work. Leading and internal
        ''' whitespace are not handled by the collation, which is why the caller must pass a
        ''' name that has already been through <c>SubjectValidator.Normalise</c>.
        '''
        ''' Returns inactive subjects too: the caller needs to tell "no such subject" apart
        ''' from "an administrator withdrew that one", and those deserve different answers.
        ''' </remarks>
        Public Async Function FindByNameAsync(subjectName As String) As Task(Of Subject)
            Const sql As String =
                "SELECT TOP 1 SubjectId, SubjectName, IsActive FROM dbo.[Subject] " &
                "WHERE SubjectName = @SubjectName;"

            If String.IsNullOrWhiteSpace(subjectName) Then Return Nothing

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@SubjectName", SqlDbType.NVarChar, 100, subjectName)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then Return Nothing

                        Return New Subject With {
                            .SubjectId = DatabaseHelper.GetInt32(reader, "SubjectId"),
                            .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                            .IsActive = DatabaseHelper.GetBoolean(reader, "IsActive")
                        }
                    End Using
                End Using
            End Using
        End Function

        Public Async Function CreateSubjectAsync(subjectName As String) As Task(Of Integer)
            Const sql As String =
                "INSERT INTO dbo.[Subject] (SubjectName, IsActive) VALUES (@SubjectName, 1); " &
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@SubjectName", SqlDbType.NVarChar, 100, subjectName)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        Public Async Function RenameSubjectAsync(subjectId As Integer, subjectName As String) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.[Subject] SET SubjectName = @SubjectName WHERE SubjectId = @SubjectId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@SubjectName", SqlDbType.NVarChar, 100, subjectName)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Removes a subject outright. Only safe once IsSubjectInUseAsync says nobody
        ''' references it; the foreign keys would refuse otherwise.
        ''' </summary>
        Public Async Function DeleteSubjectAsync(subjectId As Integer) As Task(Of Boolean)
            Const sql As String = "DELETE FROM dbo.[Subject] WHERE SubjectId = @SubjectId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        Public Async Function CreateStudyLevelAsync(name As String, sortOrder As Integer) As Task(Of Integer)
            Const sql As String =
                "INSERT INTO dbo.StudyLevel (StudyLevelName, SortOrder) VALUES (@Name, @SortOrder); " &
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@Name", SqlDbType.NVarChar, 50, name)
                    DatabaseHelper.AddParameter(command, "@SortOrder", SqlDbType.Int, sortOrder)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        Public Async Function UpdateStudyLevelAsync(studyLevelId As Integer,
                                                    name As String,
                                                    sortOrder As Integer) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.StudyLevel SET StudyLevelName = @Name, SortOrder = @SortOrder " &
                "WHERE StudyLevelId = @StudyLevelId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@Name", SqlDbType.NVarChar, 50, name)
                    DatabaseHelper.AddParameter(command, "@SortOrder", SqlDbType.Int, sortOrder)
                    DatabaseHelper.AddParameter(command, "@StudyLevelId", SqlDbType.Int, studyLevelId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        ''' <summary>True when any student or tutor has taken up this study level.</summary>
        Public Async Function IsStudyLevelInUseAsync(studyLevelId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 WHERE EXISTS (SELECT 1 FROM dbo.TutorSubject   WHERE StudyLevelId = @StudyLevelId) " &
                "         OR EXISTS (SELECT 1 FROM dbo.StudentSubject WHERE StudyLevelId = @StudyLevelId);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@StudyLevelId", SqlDbType.Int, studyLevelId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        ''' <summary>AT-25. A subject in use is deactivated, never deleted.</summary>
        Public Async Function SetSubjectActiveAsync(subjectId As Integer, isActive As Boolean) As Task(Of Boolean)
            Const sql As String = "UPDATE dbo.[Subject] SET IsActive = @IsActive WHERE SubjectId = @SubjectId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@IsActive", SqlDbType.Bit, isActive)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        ''' <summary>
        ''' True when any student or tutor has taken up this subject. The admin screen uses
        ''' this to block a delete and offer deactivation instead.
        ''' </summary>
        Public Async Function IsSubjectInUseAsync(subjectId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 WHERE EXISTS (SELECT 1 FROM dbo.TutorSubject   WHERE SubjectId = @SubjectId) " &
                "         OR EXISTS (SELECT 1 FROM dbo.StudentSubject WHERE SubjectId = @SubjectId);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

#End Region

#Region "Student subjects"

        Public Async Function GetStudentSubjectsAsync(studentId As Integer) As Task(Of IList(Of StudentSubject))
            Const sql As String =
                "SELECT ss.StudentSubjectId, ss.StudentId, ss.SubjectId, ss.StudyLevelId, ss.CreatedAt, " &
                "       sub.SubjectName, sl.StudyLevelName " &
                "FROM dbo.StudentSubject AS ss " &
                "JOIN dbo.[Subject]  AS sub ON sub.SubjectId = ss.SubjectId " &
                "JOIN dbo.StudyLevel AS sl  ON sl.StudyLevelId = ss.StudyLevelId " &
                "WHERE ss.StudentId = @StudentId " &
                "ORDER BY sub.SubjectName, sl.SortOrder;"

            Dim results As New List(Of StudentSubject)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New StudentSubject With {
                                .StudentSubjectId = DatabaseHelper.GetInt32(reader, "StudentSubjectId"),
                                .StudentId = DatabaseHelper.GetInt32(reader, "StudentId"),
                                .SubjectId = DatabaseHelper.GetInt32(reader, "SubjectId"),
                                .StudyLevelId = DatabaseHelper.GetInt32(reader, "StudyLevelId"),
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

        ''' <summary>
        ''' True when the subject exists and is still active.
        ''' </summary>
        ''' <remarks>
        ''' Deactivating a subject removes it from the dropdowns, which is presentation. This
        ''' is what stops a post that names an inactive subject anyway - the same principle
        ''' as every other rule here: hiding a control is not enforcing a rule.
        ''' </remarks>
        Public Async Function IsSubjectActiveAsync(subjectId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT TOP 1 1 FROM dbo.[Subject] WHERE SubjectId = @SubjectId AND IsActive = 1;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        ''' <summary>
        ''' True when the subject behind a TutorSubject is still active.
        ''' </summary>
        ''' <remarks>
        ''' Used where the caller names a TutorSubject rather than a Subject - requesting a
        ''' session, and publishing availability.
        ''' </remarks>
        Public Async Function IsTutorSubjectActiveAsync(tutorSubjectId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT TOP 1 1 FROM dbo.TutorSubject AS ts " &
                "JOIN dbo.[Subject] AS s ON s.SubjectId = ts.SubjectId " &
                "WHERE ts.TutorSubjectId = @TutorSubjectId AND s.IsActive = 1;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        ''' <summary>BR-05. The unique index is the real guard; this is the friendly pre-check.</summary>
        Public Async Function StudentSubjectExistsAsync(studentId As Integer,
                                                        subjectId As Integer,
                                                        studyLevelId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.StudentSubject " &
                "WHERE StudentId = @StudentId AND SubjectId = @SubjectId AND StudyLevelId = @StudyLevelId;"
            Return Await ExistsAsync(sql, "@StudentId", studentId, subjectId, studyLevelId).ConfigureAwait(False)
        End Function

        Public Async Function AddStudentSubjectAsync(studentId As Integer,
                                                     subjectId As Integer,
                                                     studyLevelId As Integer) As Task(Of Integer)
            Const sql As String =
                "INSERT INTO dbo.StudentSubject (StudentId, SubjectId, StudyLevelId) " &
                "VALUES (@StudentId, @SubjectId, @StudyLevelId); SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    DatabaseHelper.AddParameter(command, "@StudyLevelId", SqlDbType.Int, studyLevelId)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Scoped by StudentId as well as by key, so a tampered id cannot remove another
        ''' student's row (BR-24).
        ''' </summary>
        Public Async Function RemoveStudentSubjectAsync(studentSubjectId As Integer,
                                                        studentId As Integer) As Task(Of Boolean)
            Const sql As String =
                "DELETE FROM dbo.StudentSubject " &
                "WHERE StudentSubjectId = @StudentSubjectId AND StudentId = @StudentId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@StudentSubjectId", SqlDbType.Int, studentSubjectId)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

#End Region

#Region "Tutor subjects"

        Public Async Function GetTutorSubjectsAsync(tutorId As Integer) As Task(Of IList(Of TutorSubject))
            Const sql As String =
                "SELECT ts.TutorSubjectId, ts.TutorId, ts.SubjectId, ts.StudyLevelId, ts.CreatedAt, " &
                "       sub.SubjectName, sl.StudyLevelName " &
                "FROM dbo.TutorSubject AS ts " &
                "JOIN dbo.[Subject]  AS sub ON sub.SubjectId = ts.SubjectId " &
                "JOIN dbo.StudyLevel AS sl  ON sl.StudyLevelId = ts.StudyLevelId " &
                "WHERE ts.TutorId = @TutorId " &
                "ORDER BY sub.SubjectName, sl.SortOrder;"

            Dim results As New List(Of TutorSubject)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New TutorSubject With {
                                .TutorSubjectId = DatabaseHelper.GetInt32(reader, "TutorSubjectId"),
                                .TutorId = DatabaseHelper.GetInt32(reader, "TutorId"),
                                .SubjectId = DatabaseHelper.GetInt32(reader, "SubjectId"),
                                .StudyLevelId = DatabaseHelper.GetInt32(reader, "StudyLevelId"),
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

        Public Async Function TutorSubjectExistsAsync(tutorId As Integer,
                                                      subjectId As Integer,
                                                      studyLevelId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.TutorSubject " &
                "WHERE TutorId = @TutorId AND SubjectId = @SubjectId AND StudyLevelId = @StudyLevelId;"
            Return Await ExistsAsync(sql, "@TutorId", tutorId, subjectId, studyLevelId).ConfigureAwait(False)
        End Function

        Public Async Function AddTutorSubjectAsync(tutorId As Integer,
                                                   subjectId As Integer,
                                                   studyLevelId As Integer) As Task(Of Integer)
            Const sql As String =
                "INSERT INTO dbo.TutorSubject (TutorId, SubjectId, StudyLevelId) " &
                "VALUES (@TutorId, @SubjectId, @StudyLevelId); SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    DatabaseHelper.AddParameter(command, "@StudyLevelId", SqlDbType.Int, studyLevelId)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>
        ''' BR-21. True when the TutorSubject has Confirmed requests or Scheduled bookings,
        ''' in which case removal must be refused with a reason rather than failing silently.
        ''' </summary>
        Public Async Function TutorSubjectHasCommitmentsAsync(tutorSubjectId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 WHERE EXISTS (" &
                "    SELECT 1 FROM dbo.SessionRequest AS r " &
                "    WHERE r.TutorSubjectId = @TutorSubjectId AND r.[Status] = 'Confirmed') " &
                "  OR EXISTS (" &
                "    SELECT 1 FROM dbo.Booking AS b " &
                "    JOIN dbo.SessionRequest AS r2 ON r2.RequestId = b.RequestId " &
                "    WHERE r2.TutorSubjectId = @TutorSubjectId AND b.[Status] = 'Scheduled');"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        Public Async Function RemoveTutorSubjectAsync(tutorSubjectId As Integer,
                                                      tutorId As Integer) As Task(Of Boolean)
            Const sql As String =
                "DELETE FROM dbo.TutorSubject WHERE TutorSubjectId = @TutorSubjectId AND TutorId = @TutorId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

#End Region

        ''' <summary>
        ''' Shared by the two duplicate checks. The owner parameter is named by the caller
        ''' so each statement can use its own natural column name.
        ''' </summary>
        Private Shared Async Function ExistsAsync(sql As String,
                                                  ownerParameterName As String,
                                                  ownerId As Integer,
                                                  subjectId As Integer,
                                                  studyLevelId As Integer) As Task(Of Boolean)
            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, ownerParameterName, SqlDbType.Int, ownerId)
                    DatabaseHelper.AddParameter(command, "@SubjectId", SqlDbType.Int, subjectId)
                    DatabaseHelper.AddParameter(command, "@StudyLevelId", SqlDbType.Int, studyLevelId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

    End Class

End Namespace
