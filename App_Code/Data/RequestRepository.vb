Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Utilities

Namespace Data

    ''' <summary>
    ''' Session requests, plus the slot checks that BR-09 to BR-12 depend on.
    ''' </summary>
    Public Class RequestRepository

        Private Const RequestColumns As String =
            "r.RequestId, r.StudentId, r.TutorSubjectId, r.AvailabilityId, r.RequestedDate, " &
            "r.StartTime, r.EndTime, r.[Status], r.StudentMessage, r.CreatedAt, r.RespondedAt, " &
            "r.DeclineReason, sub.SubjectName, sl.StudyLevelName, " &
            "sp.FirstName + ' ' + sp.LastName AS StudentName, " &
            "tp.FirstName + ' ' + tp.LastName AS TutorName"

        Private Const RequestJoins As String =
            "FROM dbo.SessionRequest AS r " &
            "JOIN dbo.TutorSubject AS ts  ON ts.TutorSubjectId = r.TutorSubjectId " &
            "JOIN dbo.[Subject]    AS sub ON sub.SubjectId = ts.SubjectId " &
            "JOIN dbo.StudyLevel   AS sl  ON sl.StudyLevelId = ts.StudyLevelId " &
            "JOIN dbo.Tutor        AS t   ON t.TutorId = ts.TutorId " &
            "JOIN dbo.Person       AS tp  ON tp.PersonId = t.PersonId " &
            "JOIN dbo.Student      AS s   ON s.StudentId = r.StudentId " &
            "JOIN dbo.Person       AS sp  ON sp.PersonId = s.PersonId "

#Region "Reads"

        ''' <summary>The student's "My Tutor Requests" table.</summary>
        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of IList(Of SessionRequest))
            Const sql As String =
                "SELECT " & RequestColumns & " " & RequestJoins &
                "WHERE r.StudentId = @StudentId ORDER BY r.CreatedAt DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                                        End Sub).ConfigureAwait(False)
        End Function

        ''' <summary>The tutor's "Student Requests" table.</summary>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of IList(Of SessionRequest))
            Const sql As String =
                "SELECT " & RequestColumns & " " & RequestJoins &
                "WHERE ts.TutorId = @TutorId ORDER BY r.CreatedAt DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                                        End Sub).ConfigureAwait(False)
        End Function

        Public Async Function GetByIdAsync(requestId As Integer) As Task(Of SessionRequest)
            Const sql As String =
                "SELECT " & RequestColumns & " " & RequestJoins & "WHERE r.RequestId = @RequestId;"

            Dim rows As IList(Of SessionRequest) =
                Await ReadAsync(sql, Sub(command)
                                         DatabaseHelper.AddParameter(command, "@RequestId", SqlDbType.Int, requestId)
                                     End Sub).ConfigureAwait(False)

            Return If(rows.Count = 0, Nothing, rows(0))
        End Function

        ''' <summary>Every request, for the administrator's read-only view.</summary>
        Public Async Function GetAllAsync(status As String) As Task(Of IList(Of SessionRequest))
            Const sql As String =
                "SELECT " & RequestColumns & " " & RequestJoins &
                "WHERE (@Status IS NULL OR r.[Status] = @Status) " &
                "ORDER BY r.CreatedAt DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            DatabaseHelper.AddParameter(command, "@Status", SqlDbType.VarChar, 10,
                                                                        DatabaseHelper.ToDbValue(status))
                                        End Sub).ConfigureAwait(False)
        End Function

        ''' <summary>Drives the pending-count pill on the tutor dashboard.</summary>
        Public Async Function CountPendingForTutorAsync(tutorId As Integer) As Task(Of Integer)
            Const sql As String =
                "SELECT COUNT(*) FROM dbo.SessionRequest AS r " &
                "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                "WHERE ts.TutorId = @TutorId AND r.[Status] = 'Pending';"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

#End Region

#Region "Slot rules"

        ''' <summary>
        ''' BR-09. True when the requested window sits inside the published slot, the slot
        ''' belongs to the given TutorSubject, and the slot is not already consumed.
        ''' </summary>
        Public Async Function SlotIsOpenAndContainsAsync(availabilityId As Integer,
                                                         tutorSubjectId As Integer,
                                                         requestedDate As Date,
                                                         startTime As TimeSpan,
                                                         endTime As TimeSpan) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.TutorAvailability AS a " &
                "WHERE a.AvailabilityId = @AvailabilityId " &
                "  AND a.TutorSubjectId = @TutorSubjectId " &
                "  AND a.IsBooked = 0 " &
                "  AND a.AvailabilityDate = @RequestedDate " &
                "  AND @StartTime >= a.StartTime AND @EndTime <= a.EndTime;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, availabilityId)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                    DatabaseHelper.AddParameter(command, "@RequestedDate", SqlDbType.Date, requestedDate)
                    DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, startTime)
                    DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, endTime)

                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        ''' <summary>BR-10. True when a Scheduled booking already occupies the slot.</summary>
        Public Async Function SlotHasScheduledBookingAsync(availabilityId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.Booking WHERE AvailabilityId = @AvailabilityId AND [Status] = 'Scheduled';"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, availabilityId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

#End Region

#Region "Writes"

        Public Async Function CreateAsync(request As SessionRequest) As Task(Of Integer)
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))

            Const sql As String =
                "INSERT INTO dbo.SessionRequest (StudentId, TutorSubjectId, AvailabilityId, " &
                "RequestedDate, StartTime, EndTime, [Status], StudentMessage) " &
                "VALUES (@StudentId, @TutorSubjectId, @AvailabilityId, @RequestedDate, " &
                "@StartTime, @EndTime, @Status, @StudentMessage); " &
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, request.StudentId)
                    DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, request.TutorSubjectId)
                    DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, request.AvailabilityId)
                    DatabaseHelper.AddParameter(command, "@RequestedDate", SqlDbType.Date, request.RequestedDate)
                    DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, request.StartTime)
                    DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, request.EndTime)
                    DatabaseHelper.AddParameter(command, "@Status", SqlDbType.VarChar, 10, RequestStatus.Pending.ToString())
                    DatabaseHelper.AddParameter(command, "@StudentMessage", SqlDbType.NVarChar, 500,
                                                DatabaseHelper.ToDbValue(request.StudentMessage))

                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>
        ''' BR-13. Only a Pending request may be cancelled, and only by the student who
        ''' created it. Both conditions are in the WHERE clause, so a losing race simply
        ''' updates nothing and returns False.
        ''' </summary>
        Public Async Function CancelByStudentAsync(requestId As Integer, studentId As Integer) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.SessionRequest SET [Status] = 'Cancelled', RespondedAt = SYSUTCDATETIME() " &
                "WHERE RequestId = @RequestId AND StudentId = @StudentId AND [Status] = 'Pending';"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@RequestId", SqlDbType.Int, requestId)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Declines a Pending request. Scoped by tutor so one tutor cannot answer another's
        ''' request.
        ''' </summary>
        Public Async Function DeclineAsync(requestId As Integer,
                                           tutorId As Integer,
                                           declineReason As String) As Task(Of Boolean)
            Const sql As String =
                "UPDATE r SET r.[Status] = 'Declined', r.RespondedAt = SYSUTCDATETIME(), " &
                "             r.DeclineReason = @DeclineReason " &
                "FROM dbo.SessionRequest AS r " &
                "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                "WHERE r.RequestId = @RequestId AND ts.TutorId = @TutorId AND r.[Status] = 'Pending';"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@RequestId", SqlDbType.Int, requestId)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    DatabaseHelper.AddParameter(command, "@DeclineReason", SqlDbType.NVarChar, 200,
                                                DatabaseHelper.ToDbValue(declineReason))
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

#End Region

        Private Shared Async Function ReadAsync(sql As String,
                                                bindParameters As Action(Of SqlCommand)) As Task(Of IList(Of SessionRequest))
            Dim results As New List(Of SessionRequest)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    bindParameters(command)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New SessionRequest With {
                                .RequestId = DatabaseHelper.GetInt32(reader, "RequestId"),
                                .StudentId = DatabaseHelper.GetInt32(reader, "StudentId"),
                                .TutorSubjectId = DatabaseHelper.GetInt32(reader, "TutorSubjectId"),
                                .AvailabilityId = DatabaseHelper.GetInt32(reader, "AvailabilityId"),
                                .RequestedDate = DatabaseHelper.GetDate(reader, "RequestedDate"),
                                .StartTime = DatabaseHelper.GetTime(reader, "StartTime"),
                                .EndTime = DatabaseHelper.GetTime(reader, "EndTime"),
                                .Status = EnumHelper.Parse(Of RequestStatus)(DatabaseHelper.GetString(reader, "Status")),
                                .StudentMessage = DatabaseHelper.GetStringOrNothing(reader, "StudentMessage"),
                                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt"),
                                .RespondedAt = DatabaseHelper.GetNullableDate(reader, "RespondedAt"),
                                .DeclineReason = DatabaseHelper.GetStringOrNothing(reader, "DeclineReason"),
                                .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                                .StudyLevelName = DatabaseHelper.GetString(reader, "StudyLevelName"),
                                .StudentName = DatabaseHelper.GetString(reader, "StudentName"),
                                .TutorName = DatabaseHelper.GetString(reader, "TutorName")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

    End Class

End Namespace
