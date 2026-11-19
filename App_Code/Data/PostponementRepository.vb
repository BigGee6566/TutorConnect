Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports Npgsql
Imports System.Threading.Tasks
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Utilities

Namespace Data

    ''' <summary>Why an approval did not go through.</summary>
    Public Enum PostponeOutcome
        Success
        ''' <summary>Answered already, withdrawn, or never this tutor's to answer.</summary>
        NotPending
        ''' <summary>The booking is no longer Scheduled - completed or cancelled meanwhile.</summary>
        BookingNotScheduled
        ''' <summary>The tutor has taken another session across the proposed window.</summary>
        TutorConflict
        ''' <summary>The student has taken another session across the proposed window.</summary>
        StudentConflict
    End Enum

    ''' <summary>
    ''' Proposals to move an agreed session (Part 12.4, 12.5).
    ''' </summary>
    Public Class PostponementRepository

        Private Const SelectColumns As String =
            "pr.PostponementId, pr.BookingId, pr.StudentId, pr.TutorId, " &
            "pr.OriginalDate, pr.OriginalStart, pr.OriginalEnd, " &
            "pr.ProposedDate, pr.ProposedStart, pr.ProposedEnd, " &
            "pr.ReasonCategory, pr.StudentNote, pr.Status, pr.CreatedAt, " &
            "pr.TutorResponseNote, pr.RespondedAt, " &
            "sp.FirstName || ' ' || sp.LastName AS StudentName, " &
            "tp.FirstName || ' ' || tp.LastName AS TutorName, " &
            "s.SubjectName, l.StudyLevelName"

        Private Const FromClause As String =
            "FROM dbo.PostponementRequest AS pr " &
            "JOIN dbo.Student AS st ON st.StudentId = pr.StudentId " &
            "JOIN dbo.Person  AS sp ON sp.PersonId  = st.PersonId " &
            "JOIN dbo.Tutor   AS t  ON t.TutorId    = pr.TutorId " &
            "JOIN dbo.Person  AS tp ON tp.PersonId  = t.PersonId " &
            "JOIN dbo.Booking AS b  ON b.BookingId  = pr.BookingId " &
            "JOIN dbo.SessionRequest AS r ON r.RequestId = b.RequestId " &
            "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
            "JOIN dbo.Subject AS s ON s.SubjectId = ts.SubjectId " &
            "JOIN dbo.StudyLevel AS l ON l.StudyLevelId = ts.StudyLevelId "

#Region "Writes"

        ''' <summary>
        ''' Records a proposal against a booking the student owns.
        ''' </summary>
        ''' <remarks>
        ''' The original values are copied from the booking inside the same statement rather
        ''' than passed in, so they cannot be forged by the caller and cannot drift from what
        ''' the booking actually says.
        '''
        ''' Ownership is a condition of the INSERT itself - a booking belonging to someone
        ''' else selects no rows and writes nothing, rather than being caught by a separate
        ''' check that could be raced.
        ''' </remarks>
        ''' <returns>The new identifier, or 0 when nothing was written.</returns>
        Public Async Function CreateAsync(bookingId As Integer,
                                          studentId As Integer,
                                          proposedDate As Date,
                                          proposedStart As TimeSpan,
                                          proposedEnd As TimeSpan,
                                          reason As ScheduleChangeReason,
                                          note As String) As Task(Of Integer)
            Const sql As String =
                "INSERT INTO dbo.PostponementRequest " &
                "(BookingId, StudentId, TutorId, OriginalDate, OriginalStart, OriginalEnd, " &
                " ProposedDate, ProposedStart, ProposedEnd, ReasonCategory, StudentNote) " &
                "SELECT b.BookingId, r.StudentId, ts.TutorId, " &
                "       b.BookingDate, b.StartTime, b.EndTime, " &
                "       @ProposedDate, @ProposedStart, @ProposedEnd, @ReasonCategory, @StudentNote " &
                "FROM dbo.Booking AS b " &
                "JOIN dbo.SessionRequest AS r ON r.RequestId = b.RequestId " &
                "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                "WHERE b.BookingId = @BookingId " &
                "  AND r.StudentId = @StudentId " &
                "  AND b.Status = 'Scheduled' " &
                "RETURNING PostponementId;"

            Using connection As NpgsqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As NpgsqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    DatabaseHelper.AddParameter(command, "@ProposedDate", SqlDbType.Date, proposedDate)
                    DatabaseHelper.AddParameter(command, "@ProposedStart", SqlDbType.Time, proposedStart)
                    DatabaseHelper.AddParameter(command, "@ProposedEnd", SqlDbType.Time, proposedEnd)
                    DatabaseHelper.AddParameter(command, "@ReasonCategory", SqlDbType.VarChar, 30, reason.ToString())
                    DatabaseHelper.AddParameter(command, "@StudentNote", SqlDbType.NVarChar, 200,
                                                DatabaseHelper.ToDbValue(note))

                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    If scalar Is Nothing OrElse Convert.IsDBNull(scalar) Then Return 0
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>
        ''' The tutor accepts: the booking moves to the proposed window, a slot is created for
        ''' it, and the proposal closes - all in one SERIALIZABLE transaction.
        ''' </summary>
        ''' <remarks>
        ''' <para>
        ''' The same shape as <c>BookingRepository.AcceptRequestAsync</c>, and for the same
        ''' reason: two approvals landing together must not both succeed. UPDLOCK, HOLDLOCK
        ''' on the overlap reads is what makes the re-check hold until the commit.
        ''' </para>
        ''' <para>
        ''' A new TutorAvailability row is created and immediately marked booked. The tutor
        ''' pressing Approve IS the availability decision - requiring a slot to have been
        ''' published in advance would mean a student could only ever propose times the tutor
        ''' had already offered, which is not what postponement is for. The old slot is
        ''' released so someone else can take it.
        ''' </para>
        ''' </remarks>
        Public Async Function ApproveAsync(postponementId As Integer,
                                           tutorId As Integer,
                                           responseNote As String) As Task(Of PostponeOutcome)
            Using connection As NpgsqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using transaction As NpgsqlTransaction = connection.BeginTransaction(IsolationLevel.Serializable)
                    Try
                        Const selectProposal As String =
                            "SELECT pr.BookingId, pr.StudentId, pr.ProposedDate, pr.ProposedStart, pr.ProposedEnd, " &
                            "       b.Status AS BookingStatus, b.AvailabilityId, ts.TutorSubjectId " &
                            "FROM dbo.PostponementRequest AS pr " &
                            "JOIN dbo.Booking AS b ON b.BookingId = pr.BookingId " &
                            "JOIN dbo.SessionRequest AS r ON r.RequestId = b.RequestId " &
                            "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                            "WHERE pr.PostponementId = @PostponementId " &
                            "  AND pr.TutorId = @TutorId " &
                            "  AND pr.Status = 'PendingTutorReview' " &
                            "FOR UPDATE OF pr, b;"

                        Dim bookingId, studentId, oldAvailabilityId, tutorSubjectId As Integer
                        Dim proposedDate As Date
                        Dim proposedStart, proposedEnd As TimeSpan
                        Dim bookingStatus As String = Nothing
                        Dim found As Boolean

                        Using command As NpgsqlCommand = DatabaseHelper.CreateCommand(selectProposal, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@PostponementId", SqlDbType.Int, postponementId)
                            DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)

                            Using reader As NpgsqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                                found = Await reader.ReadAsync().ConfigureAwait(False)

                                If found Then
                                    bookingId = DatabaseHelper.GetInt32(reader, "BookingId")
                                    studentId = DatabaseHelper.GetInt32(reader, "StudentId")
                                    proposedDate = DatabaseHelper.GetDate(reader, "ProposedDate")
                                    proposedStart = DatabaseHelper.GetTime(reader, "ProposedStart")
                                    proposedEnd = DatabaseHelper.GetTime(reader, "ProposedEnd")
                                    bookingStatus = DatabaseHelper.GetString(reader, "BookingStatus")
                                    oldAvailabilityId = DatabaseHelper.GetInt32(reader, "AvailabilityId")
                                    tutorSubjectId = DatabaseHelper.GetInt32(reader, "TutorSubjectId")
                                End If
                            End Using
                        End Using

                        ' After the reader is closed, never inside it - rolling back with an
                        ' open reader throws, and turns an ordinary second click into an
                        ' unhandled exception.
                        If Not found Then
                            transaction.Rollback()
                            Return PostponeOutcome.NotPending
                        End If

                        If Not String.Equals(bookingStatus, "Scheduled", StringComparison.Ordinal) Then
                            transaction.Rollback()
                            Return PostponeOutcome.BookingNotScheduled
                        End If

                        ' The booking being moved is excluded from both checks, or it would
                        ' clash with itself and no postponement could ever be approved.
                        If Await BookingRepository.HasOverlapAsync(
                            connection, transaction, True, tutorId,
                            proposedDate, proposedStart, proposedEnd, bookingId).ConfigureAwait(False) Then
                            transaction.Rollback()
                            Return PostponeOutcome.TutorConflict
                        End If

                        If Await BookingRepository.HasOverlapAsync(
                            connection, transaction, False, studentId,
                            proposedDate, proposedStart, proposedEnd, bookingId).ConfigureAwait(False) Then
                            transaction.Rollback()
                            Return PostponeOutcome.StudentConflict
                        End If

                        Const applyApproval As String =
                            "WITH na AS (" &
                            "  INSERT INTO dbo.TutorAvailability " &
                            "  (TutorSubjectId, AvailabilityDate, StartTime, EndTime, IsBooked) " &
                            "  VALUES (@TutorSubjectId, @ProposedDate, @ProposedStart, @ProposedEnd, 1) " &
                            "  RETURNING AvailabilityId) " &
                            "UPDATE dbo.Booking " &
                            "SET BookingDate = @ProposedDate, StartTime = @ProposedStart, " &
                            "    EndTime = @ProposedEnd, AvailabilityId = (SELECT AvailabilityId FROM na) " &
                            "WHERE BookingId = @BookingId; " &
                            "UPDATE dbo.SessionRequest " &
                            "SET RequestedDate = @ProposedDate, StartTime = @ProposedStart, EndTime = @ProposedEnd " &
                            "WHERE RequestId = (SELECT RequestId FROM dbo.Booking WHERE BookingId = @BookingId); " &
                            "UPDATE dbo.TutorAvailability SET IsBooked = 0 WHERE AvailabilityId = @OldAvailabilityId; " &
                            "UPDATE dbo.PostponementRequest " &
                            "SET Status = 'Approved', RespondedAt = SYSUTCDATETIME(), TutorResponseNote = @Note " &
                            "WHERE PostponementId = @PostponementId;"

                        Using command As NpgsqlCommand = DatabaseHelper.CreateCommand(applyApproval, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                            DatabaseHelper.AddParameter(command, "@ProposedDate", SqlDbType.Date, proposedDate)
                            DatabaseHelper.AddParameter(command, "@ProposedStart", SqlDbType.Time, proposedStart)
                            DatabaseHelper.AddParameter(command, "@ProposedEnd", SqlDbType.Time, proposedEnd)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                            DatabaseHelper.AddParameter(command, "@OldAvailabilityId", SqlDbType.Int, oldAvailabilityId)
                            DatabaseHelper.AddParameter(command, "@PostponementId", SqlDbType.Int, postponementId)
                            DatabaseHelper.AddParameter(command, "@Note", SqlDbType.NVarChar, 200,
                                                        DatabaseHelper.ToDbValue(responseNote))
                            Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        transaction.Commit()
                        Return PostponeOutcome.Success

                    Catch
                        Try
                            transaction.Rollback()
                        Catch
                            ' Already dead; nothing further to undo.
                        End Try
                        Throw
                    End Try
                End Using
            End Using
        End Function

        ''' <summary>
        ''' The tutor refuses. The booking is untouched, which is the point.
        ''' </summary>
        ''' <returns>True when this call resolved it.</returns>
        Public Async Function DeclineAsync(postponementId As Integer,
                                           tutorId As Integer,
                                           responseNote As String) As Task(Of Boolean)
            ' Tutor and pending state are conditions of the UPDATE, so a foreign or
            ' already-answered proposal changes nothing rather than being caught separately.
            Const sql As String =
                "UPDATE dbo.PostponementRequest " &
                "SET Status = 'Declined', RespondedAt = SYSUTCDATETIME(), TutorResponseNote = @Note " &
                "WHERE PostponementId = @Id AND TutorId = @TutorId AND Status = 'PendingTutorReview';"

            Using connection As NpgsqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As NpgsqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@Id", SqlDbType.Int, postponementId)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                    DatabaseHelper.AddParameter(command, "@Note", SqlDbType.NVarChar, 200,
                                                DatabaseHelper.ToDbValue(responseNote))

                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) > 0
                End Using
            End Using
        End Function

        ''' <summary>Withdraws the student's own pending proposal.</summary>
        Public Async Function CancelAsync(postponementId As Integer, studentId As Integer) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.PostponementRequest " &
                "SET Status = 'Cancelled', RespondedAt = SYSUTCDATETIME() " &
                "WHERE PostponementId = @Id AND StudentId = @StudentId AND Status = 'PendingTutorReview';"

            Using connection As NpgsqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As NpgsqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@Id", SqlDbType.Int, postponementId)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) > 0
                End Using
            End Using
        End Function

#End Region

#Region "Reads"

        ''' <summary>
        ''' The tutor's queue. Pending proposals sort first: they are the only part of the
        ''' list that needs an answer.
        ''' </summary>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of IList(Of PostponementRequest))
            Dim sql As String =
                "SELECT " & SelectColumns & " " & FromClause &
                "WHERE pr.TutorId = @OwnerId " &
                "ORDER BY CASE WHEN pr.Status = 'PendingTutorReview' THEN 0 ELSE 1 END, pr.CreatedAt DESC;"

            Return Await ReadListAsync(sql, tutorId).ConfigureAwait(False)
        End Function

        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of IList(Of PostponementRequest))
            Dim sql As String =
                "SELECT " & SelectColumns & " " & FromClause &
                "WHERE pr.StudentId = @OwnerId " &
                "ORDER BY pr.CreatedAt DESC;"

            Return Await ReadListAsync(sql, studentId).ConfigureAwait(False)
        End Function

        ''' <summary>The open proposal on a booking, or Nothing.</summary>
        Public Async Function GetPendingForBookingAsync(bookingId As Integer) As Task(Of PostponementRequest)
            Dim sql As String =
                "SELECT " & SelectColumns & " " & FromClause &
                "WHERE pr.BookingId = @OwnerId AND pr.Status = 'PendingTutorReview';"

            Dim rows As IList(Of PostponementRequest) = Await ReadListAsync(sql, bookingId).ConfigureAwait(False)
            Return If(rows.Count > 0, rows(0), Nothing)
        End Function

        ''' <summary>One proposal by identifier, for the service's ownership checks.</summary>
        Public Async Function GetByIdAsync(postponementId As Integer) As Task(Of PostponementRequest)
            Dim sql As String =
                "SELECT " & SelectColumns & " " & FromClause &
                "WHERE pr.PostponementId = @OwnerId;"

            Dim rows As IList(Of PostponementRequest) = Await ReadListAsync(sql, postponementId).ConfigureAwait(False)
            Return If(rows.Count > 0, rows(0), Nothing)
        End Function

        Private Shared Async Function ReadListAsync(sql As String, ownerId As Integer) As Task(Of IList(Of PostponementRequest))
            Dim results As New List(Of PostponementRequest)()

            Using connection As NpgsqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As NpgsqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@OwnerId", SqlDbType.Int, ownerId)

                    Using reader As NpgsqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(Map(reader))
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        Private Shared Function Map(reader As NpgsqlDataReader) As PostponementRequest
            Return New PostponementRequest With {
                .PostponementId = DatabaseHelper.GetInt32(reader, "PostponementId"),
                .BookingId = DatabaseHelper.GetInt32(reader, "BookingId"),
                .StudentId = DatabaseHelper.GetInt32(reader, "StudentId"),
                .TutorId = DatabaseHelper.GetInt32(reader, "TutorId"),
                .OriginalDate = DatabaseHelper.GetDate(reader, "OriginalDate"),
                .OriginalStart = DatabaseHelper.GetTime(reader, "OriginalStart"),
                .OriginalEnd = DatabaseHelper.GetTime(reader, "OriginalEnd"),
                .ProposedDate = DatabaseHelper.GetDate(reader, "ProposedDate"),
                .ProposedStart = DatabaseHelper.GetTime(reader, "ProposedStart"),
                .ProposedEnd = DatabaseHelper.GetTime(reader, "ProposedEnd"),
                .ReasonCategory = EnumHelper.Parse(Of ScheduleChangeReason)(
                    DatabaseHelper.GetString(reader, "ReasonCategory")),
                .StudentNote = DatabaseHelper.GetStringOrNothing(reader, "StudentNote"),
                .Status = EnumHelper.Parse(Of PostponementStatus)(DatabaseHelper.GetString(reader, "Status")),
                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt"),
                .TutorResponseNote = DatabaseHelper.GetStringOrNothing(reader, "TutorResponseNote"),
                .RespondedAt = DatabaseHelper.GetNullableDate(reader, "RespondedAt"),
                .StudentName = DatabaseHelper.GetString(reader, "StudentName"),
                .TutorName = DatabaseHelper.GetString(reader, "TutorName"),
                .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                .StudyLevelName = DatabaseHelper.GetString(reader, "StudyLevelName")
            }
        End Function

#End Region

    End Class

End Namespace
