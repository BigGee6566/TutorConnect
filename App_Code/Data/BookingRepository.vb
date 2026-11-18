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
    ''' Why accepting a request did or did not produce a booking.
    ''' </summary>
    Public Enum AcceptOutcome
        Success
        ''' <summary>Someone already answered it, or it was cancelled.</summary>
        RequestNotPending
        ''' <summary>BR-15. The window is not inside a slot this tutor published.</summary>
        OutsideAvailability
        ''' <summary>BR-10. The slot was consumed while this request sat waiting.</summary>
        SlotAlreadyTaken
        ''' <summary>BR-14. The tutor already has a Scheduled booking overlapping this window.</summary>
        TutorConflict
        ''' <summary>BR-12. The student already has a Scheduled booking overlapping this window.</summary>
        StudentConflict
    End Enum

    ''' <summary>
    ''' Bookings, including the two guards that no constraint can express.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' The accept and complete operations own their transactions here rather than in the
    ''' service layer. The layering rule says services orchestrate transactions but hold no
    ''' SqlConnection - and a SERIALIZABLE transaction cannot exist without one. The
    ''' resolution: atomicity lives where the connection lives, while the decision of which
    ''' rules apply and what the user is told stays in the service, which interprets the
    ''' <see cref="AcceptOutcome"/> returned here.
    ''' </para>
    ''' </remarks>
    Public Class BookingRepository

        ''' <summary>
        ''' The cancellation category, which is null on a session that was never cancelled.
        ''' </summary>
        ''' <remarks>
        ''' EnumHelper.Parse throws on an empty value by design - it reads columns the
        ''' database has already constrained, so a bad one means the code and the schema have
        ''' diverged. Here the null is ordinary, not a divergence.
        ''' </remarks>
        Private Shared Function ParseReason(value As String) As ScheduleChangeReason?
            If String.IsNullOrWhiteSpace(value) Then Return Nothing
            Return EnumHelper.Parse(Of ScheduleChangeReason)(value)
        End Function

        Private Const BookingColumns As String =
            "b.BookingId, b.RequestId, b.AvailabilityId, b.BookingDate, b.StartTime, b.EndTime, " &
            "b.[Status], b.CompletedAt, b.CancelledAt, b.CancelledByPersonId, " &
            "b.CancelReasonCategory, b.CancelReason, b.CreatedAt, " &
            "r.StudentId, ts.TutorId, sub.SubjectName, sl.StudyLevelName, t.RatePerSession, " &
            "sp.FirstName + ' ' + sp.LastName AS StudentName, " &
            "tp.FirstName + ' ' + tp.LastName AS TutorName, " &
            "cp.FirstName + ' ' + cp.LastName AS CancelledByName, " &
            "CAST(CASE WHEN rv.ReviewId IS NULL THEN 0 ELSE 1 END AS BIT) AS HasReview"

        Private Const BookingJoins As String =
            "FROM dbo.Booking AS b " &
            "JOIN dbo.SessionRequest AS r   ON r.RequestId = b.RequestId " &
            "JOIN dbo.TutorSubject   AS ts  ON ts.TutorSubjectId = r.TutorSubjectId " &
            "JOIN dbo.[Subject]      AS sub ON sub.SubjectId = ts.SubjectId " &
            "JOIN dbo.StudyLevel     AS sl  ON sl.StudyLevelId = ts.StudyLevelId " &
            "JOIN dbo.Tutor          AS t   ON t.TutorId = ts.TutorId " &
            "JOIN dbo.Person         AS tp  ON tp.PersonId = t.PersonId " &
            "JOIN dbo.Student        AS s   ON s.StudentId = r.StudentId " &
            "JOIN dbo.Person         AS sp  ON sp.PersonId = s.PersonId " &
            "LEFT JOIN dbo.Person    AS cp  ON cp.PersonId = b.CancelledByPersonId " &
            "LEFT JOIN dbo.Review    AS rv  ON rv.BookingId = b.BookingId "

#Region "Reads"

        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of IList(Of Booking))
            Const sql As String =
                "SELECT " & BookingColumns & " " & BookingJoins &
                "WHERE r.StudentId = @StudentId ORDER BY b.BookingDate DESC, b.StartTime DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                                        End Sub).ConfigureAwait(False)
        End Function

        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of IList(Of Booking))
            Const sql As String =
                "SELECT " & BookingColumns & " " & BookingJoins &
                "WHERE ts.TutorId = @TutorId ORDER BY b.BookingDate DESC, b.StartTime DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                                        End Sub).ConfigureAwait(False)
        End Function

        Public Async Function GetByIdAsync(bookingId As Integer) As Task(Of Booking)
            Const sql As String =
                "SELECT " & BookingColumns & " " & BookingJoins & "WHERE b.BookingId = @BookingId;"

            Dim rows As IList(Of Booking) =
                Await ReadAsync(sql, Sub(command)
                                         DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                                     End Sub).ConfigureAwait(False)

            Return If(rows.Count = 0, Nothing, rows(0))
        End Function

        ''' <summary>Admin read-only view of every booking.</summary>
        Public Async Function GetAllAsync() As Task(Of IList(Of Booking))
            Const sql As String =
                "SELECT " & BookingColumns & " " & BookingJoins &
                "ORDER BY b.BookingDate DESC, b.StartTime DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            ' No filters.
                                        End Sub).ConfigureAwait(False)
        End Function

        ''' <summary>
        ''' BR-12, checked before a request is created.
        ''' </summary>
        ''' <remarks>
        ''' This is the early, friendly check so a student is told at request time rather
        ''' than after a tutor accepts. It is NOT the guarantee - two requests could still
        ''' be accepted concurrently. The binding check is the identical predicate run
        ''' inside the serialisable transaction in <see cref="AcceptRequestAsync"/>.
        ''' </remarks>
        Public Async Function StudentHasOverlapAsync(studentId As Integer,
                                                     bookingDate As Date,
                                                     startTime As TimeSpan,
                                                     endTime As TimeSpan) As Task(Of Boolean)
            Const sql As String =
                "SELECT TOP 1 b.BookingId " &
                "FROM dbo.Booking AS b " &
                "JOIN dbo.SessionRequest AS r ON r.RequestId = b.RequestId " &
                "WHERE r.StudentId = @StudentId " &
                "  AND b.BookingDate = @BookingDate " &
                "  AND b.[Status] = 'Scheduled' " &
                "  AND b.StartTime < @EndTime AND @StartTime < b.EndTime;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    DatabaseHelper.AddParameter(command, "@BookingDate", SqlDbType.Date, bookingDate)
                    DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, startTime)
                    DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, endTime)

                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

#End Region

#Region "Accept - the serialisable path"

        ''' <summary>
        ''' Turns a Pending request into a Scheduled booking, atomically (AT-14, AT-15).
        ''' </summary>
        ''' <remarks>
        ''' Runs SERIALIZABLE with UPDLOCK/HOLDLOCK so two simultaneous accepts of the same
        ''' slot cannot both pass their checks. Exactly one wins; the other returns a
        ''' conflict outcome and writes nothing.
        ''' </remarks>
        Public Async Function AcceptRequestAsync(requestId As Integer, tutorId As Integer) As Task(Of AcceptOutcome)
            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)

                Using setIsolation As SqlCommand = DatabaseHelper.CreateCommand("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;", connection)
                    Await setIsolation.ExecuteNonQueryAsync().ConfigureAwait(False)
                End Using

                Using transaction As SqlTransaction = connection.BeginTransaction(IsolationLevel.Serializable)
                    Try
                        ' 1. Take the request under an update lock and confirm it is still
                        '    Pending and still belongs to this tutor.
                        Const selectRequest As String =
                            "SELECT r.RequestId, r.StudentId, r.AvailabilityId, r.RequestedDate, " &
                            "       r.StartTime, r.EndTime " &
                            "FROM dbo.SessionRequest AS r WITH (UPDLOCK, HOLDLOCK) " &
                            "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                            "WHERE r.RequestId = @RequestId AND ts.TutorId = @TutorId AND r.[Status] = 'Pending';"

                        Dim studentId As Integer
                        Dim availabilityId As Integer
                        Dim bookingDate As Date
                        Dim startTime As TimeSpan
                        Dim endTime As TimeSpan
                        Dim found As Boolean

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(selectRequest, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@RequestId", SqlDbType.Int, requestId)
                            DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)

                            Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                                found = Await reader.ReadAsync().ConfigureAwait(False)

                                If found Then
                                    studentId = DatabaseHelper.GetInt32(reader, "StudentId")
                                    availabilityId = DatabaseHelper.GetInt32(reader, "AvailabilityId")
                                    bookingDate = DatabaseHelper.GetDate(reader, "RequestedDate")
                                    startTime = DatabaseHelper.GetTime(reader, "StartTime")
                                    endTime = DatabaseHelper.GetTime(reader, "EndTime")
                                End If
                            End Using
                        End Using

                        ' Rolling back has to happen after the reader is closed. Doing it
                        ' inside the Using above throws "There is already an open
                        ' DataReader associated with this Command", which turned the
                        ' ordinary case of a request that is no longer pending - a second
                        ' click, or a second tab - into an unhandled exception.
                        If Not found Then
                            transaction.Rollback()
                            Return AcceptOutcome.RequestNotPending
                        End If

                        ' 2. BR-15 / BR-10: the slot must still be this tutor's, still open,
                        '    and must still contain the requested window.
                        Const checkSlot As String =
                            "SELECT a.IsBooked FROM dbo.TutorAvailability AS a WITH (UPDLOCK, HOLDLOCK) " &
                            "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = a.TutorSubjectId " &
                            "WHERE a.AvailabilityId = @AvailabilityId AND ts.TutorId = @TutorId " &
                            "  AND a.AvailabilityDate = @BookingDate " &
                            "  AND @StartTime >= a.StartTime AND @EndTime <= a.EndTime;"

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(checkSlot, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, availabilityId)
                            DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                            DatabaseHelper.AddParameter(command, "@BookingDate", SqlDbType.Date, bookingDate)
                            DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, startTime)
                            DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, endTime)

                            Dim isBooked As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                            If isBooked Is Nothing OrElse Convert.IsDBNull(isBooked) Then
                                transaction.Rollback()
                                Return AcceptOutcome.OutsideAvailability
                            End If
                            If CBool(isBooked) Then
                                transaction.Rollback()
                                Return AcceptOutcome.SlotAlreadyTaken
                            End If
                        End Using

                        ' 3. BR-14: tutor must not already be booked across this window.
                        If Await HasOverlapAsync(connection, transaction, True, tutorId,
                                                 bookingDate, startTime, endTime).ConfigureAwait(False) Then
                            transaction.Rollback()
                            Return AcceptOutcome.TutorConflict
                        End If

                        ' 4. BR-12: nor may the student be.
                        If Await HasOverlapAsync(connection, transaction, False, studentId,
                                                 bookingDate, startTime, endTime).ConfigureAwait(False) Then
                            transaction.Rollback()
                            Return AcceptOutcome.StudentConflict
                        End If

                        ' 5. Commit the three writes together.
                        Const applyAccept As String =
                            "UPDATE dbo.SessionRequest SET [Status] = 'Confirmed', RespondedAt = SYSUTCDATETIME() " &
                            "WHERE RequestId = @RequestId; " &
                            "UPDATE dbo.TutorAvailability SET IsBooked = 1 WHERE AvailabilityId = @AvailabilityId; " &
                            "INSERT INTO dbo.Booking (RequestId, AvailabilityId, BookingDate, StartTime, EndTime, [Status]) " &
                            "VALUES (@RequestId, @AvailabilityId, @BookingDate, @StartTime, @EndTime, 'Scheduled');"

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(applyAccept, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@RequestId", SqlDbType.Int, requestId)
                            DatabaseHelper.AddParameter(command, "@AvailabilityId", SqlDbType.Int, availabilityId)
                            DatabaseHelper.AddParameter(command, "@BookingDate", SqlDbType.Date, bookingDate)
                            DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, startTime)
                            DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, endTime)
                            Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        transaction.Commit()
                        Return AcceptOutcome.Success

                    Catch
                        ' A deadlock victim lands here. Roll back and let the service decide
                        ' whether to retry or report a conflict.
                        Try
                            transaction.Rollback()
                        Catch
                            ' The transaction was already dead; nothing more to undo.
                        End Try
                        Throw
                    End Try
                End Using
            End Using
        End Function

        ''' <summary>
        ''' The half-open overlap test, keyed on either the tutor or the student.
        ''' A booking ending at 12:00 does not clash with one starting at 12:00.
        ''' </summary>
        ''' <param name="excludeBookingId">
        ''' A booking to ignore. Needed when an existing booking is being MOVED: without it
        ''' the booking overlaps itself and every postponement is refused.
        ''' </param>
        ''' <remarks>
        ''' Friend rather than Private so <see cref="PostponementRepository"/> uses this exact
        ''' test rather than a second copy of it. Two implementations of an overlap rule
        ''' disagree eventually, and the one that matters is whichever the booking path uses.
        ''' </remarks>
        Friend Shared Async Function HasOverlapAsync(connection As SqlConnection,
                                                     transaction As SqlTransaction,
                                                     byTutor As Boolean,
                                                     ownerId As Integer,
                                                     bookingDate As Date,
                                                     startTime As TimeSpan,
                                                     endTime As TimeSpan,
                                                     Optional excludeBookingId As Integer = 0) As Task(Of Boolean)
            Dim ownerPredicate As String = If(byTutor, "ts.TutorId = @OwnerId", "r.StudentId = @OwnerId")

            Dim sql As String =
                "SELECT TOP 1 b.BookingId " &
                "FROM dbo.Booking AS b WITH (UPDLOCK, HOLDLOCK) " &
                "JOIN dbo.SessionRequest AS r  ON r.RequestId = b.RequestId " &
                "JOIN dbo.TutorSubject   AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                "WHERE " & ownerPredicate & " " &
                "  AND b.BookingDate = @BookingDate " &
                "  AND b.[Status] = 'Scheduled' " &
                "  AND (@ExcludeBookingId = 0 OR b.BookingId <> @ExcludeBookingId) " &
                "  AND b.StartTime < @EndTime AND @StartTime < b.EndTime;"

            Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection, transaction)
                DatabaseHelper.AddParameter(command, "@OwnerId", SqlDbType.Int, ownerId)
                DatabaseHelper.AddParameter(command, "@BookingDate", SqlDbType.Date, bookingDate)
                DatabaseHelper.AddParameter(command, "@StartTime", SqlDbType.Time, startTime)
                DatabaseHelper.AddParameter(command, "@EndTime", SqlDbType.Time, endTime)
                DatabaseHelper.AddParameter(command, "@ExcludeBookingId", SqlDbType.Int, excludeBookingId)

                Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
            End Using
        End Function

#End Region

#Region "Complete and cancel"

        ''' <summary>
        ''' BR-23 / AT-18. Marks a Scheduled booking Completed and writes its
        ''' TutoringSession row in one transaction. The end-time and ownership checks are
        ''' in the UPDATE's WHERE clause, so a premature or foreign attempt changes nothing.
        ''' </summary>
        ''' <returns>True when the booking was completed by this call.</returns>
        Public Async Function MarkCompletedAsync(bookingId As Integer, tutorId As Integer) As Task(Of Boolean)
            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using transaction As SqlTransaction = connection.BeginTransaction()
                    Try
                        ' The end-time test folds the TIME into the DATE with DATEADD.
                        ' DATETIME2 has no addition operator - only the legacy DATETIME type
                        ' does - so "CAST(date AS DATETIME2) + CAST(time AS DATETIME2)" fails
                        ' with Msg 8117 at run time, which no amount of compiling would catch.
                        Const complete As String =
                            "UPDATE b SET b.[Status] = 'Completed', b.CompletedAt = SYSUTCDATETIME() " &
                            "FROM dbo.Booking AS b " &
                            "JOIN dbo.SessionRequest AS r  ON r.RequestId = b.RequestId " &
                            "JOIN dbo.TutorSubject   AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                            "WHERE b.BookingId = @BookingId " &
                            "  AND ts.TutorId = @TutorId " &
                            "  AND b.[Status] = 'Scheduled' " &
                            "  AND DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', b.EndTime), " &
                            "              CAST(b.BookingDate AS DATETIME2(0))) <= @NowLocal;"

                        Dim affected As Integer
                        Using command As SqlCommand = DatabaseHelper.CreateCommand(complete, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                            DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                            ' Local time: the slot is a wall-clock time in SAST, not UTC.
                            DatabaseHelper.AddParameter(command, "@NowLocal", SqlDbType.DateTime2, Date.Now)
                            affected = Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        If affected <> 1 Then
                            transaction.Rollback()
                            Return False
                        End If

                        Const createSession As String =
                            "INSERT INTO dbo.TutoringSession (BookingId, SessionDate) " &
                            "SELECT b.BookingId, b.BookingDate FROM dbo.Booking AS b WHERE b.BookingId = @BookingId;"

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(createSession, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                            Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        transaction.Commit()
                        Return True

                    Catch
                        Try
                            transaction.Rollback()
                        Catch
                            ' Already rolled back.
                        End Try
                        Throw
                    End Try
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Moves a Scheduled booking to a new window on an administrator's instruction.
        ''' </summary>
        ''' <remarks>
        ''' <para>
        ''' The same shape as a tutor approving a postponement, and for the same reason: it
        ''' moves a booking, so it must re-check under lock that neither party has since been
        ''' booked across the new window. The booking being moved is excluded from both
        ''' checks or it would clash with itself.
        ''' </para>
        ''' <para>
        ''' A new availability row is created and marked booked, and the old one released.
        ''' An administrator instructing the move is the authority here, exactly as the
        ''' tutor's approval is on the postponement path.
        ''' </para>
        ''' </remarks>
        Public Async Function RescheduleAsync(bookingId As Integer,
                                              newDate As Date,
                                              newStart As TimeSpan,
                                              newEnd As TimeSpan,
                                              auditEntry As AuditEntry) As Task(Of PostponeOutcome)
            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using transaction As SqlTransaction = connection.BeginTransaction(IsolationLevel.Serializable)
                    Try
                        Const selectBooking As String =
                            "SELECT b.[Status], b.AvailabilityId, r.StudentId, ts.TutorId, ts.TutorSubjectId " &
                            "FROM dbo.Booking AS b WITH (UPDLOCK, HOLDLOCK) " &
                            "JOIN dbo.SessionRequest AS r ON r.RequestId = b.RequestId " &
                            "JOIN dbo.TutorSubject AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                            "WHERE b.BookingId = @BookingId;"

                        Dim status As String = Nothing
                        Dim oldAvailabilityId, studentId, tutorId, tutorSubjectId As Integer
                        Dim found As Boolean

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(selectBooking, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)

                            Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                                found = Await reader.ReadAsync().ConfigureAwait(False)

                                If found Then
                                    status = DatabaseHelper.GetString(reader, "Status")
                                    oldAvailabilityId = DatabaseHelper.GetInt32(reader, "AvailabilityId")
                                    studentId = DatabaseHelper.GetInt32(reader, "StudentId")
                                    tutorId = DatabaseHelper.GetInt32(reader, "TutorId")
                                    tutorSubjectId = DatabaseHelper.GetInt32(reader, "TutorSubjectId")
                                End If
                            End Using
                        End Using

                        ' After the reader is closed, never inside it.
                        If Not found OrElse Not String.Equals(status, "Scheduled", StringComparison.Ordinal) Then
                            transaction.Rollback()
                            Return PostponeOutcome.BookingNotScheduled
                        End If

                        If Await HasOverlapAsync(connection, transaction, True, tutorId,
                                                 newDate, newStart, newEnd, bookingId).ConfigureAwait(False) Then
                            transaction.Rollback()
                            Return PostponeOutcome.TutorConflict
                        End If

                        If Await HasOverlapAsync(connection, transaction, False, studentId,
                                                 newDate, newStart, newEnd, bookingId).ConfigureAwait(False) Then
                            transaction.Rollback()
                            Return PostponeOutcome.StudentConflict
                        End If

                        Const applyMove As String =
                            "DECLARE @NewAvailabilityId INT; " &
                            "INSERT INTO dbo.TutorAvailability " &
                            "(TutorSubjectId, AvailabilityDate, StartTime, EndTime, IsBooked) " &
                            "VALUES (@TutorSubjectId, @NewDate, @NewStart, @NewEnd, 1); " &
                            "SET @NewAvailabilityId = CAST(SCOPE_IDENTITY() AS INT); " &
                            "UPDATE dbo.Booking " &
                            "SET BookingDate = @NewDate, StartTime = @NewStart, EndTime = @NewEnd, " &
                            "    AvailabilityId = @NewAvailabilityId " &
                            "WHERE BookingId = @BookingId; " &
                            "UPDATE dbo.SessionRequest " &
                            "SET RequestedDate = @NewDate, StartTime = @NewStart, EndTime = @NewEnd " &
                            "WHERE RequestId = (SELECT RequestId FROM dbo.Booking WHERE BookingId = @BookingId); " &
                            "UPDATE dbo.TutorAvailability SET IsBooked = 0 WHERE AvailabilityId = @OldAvailabilityId; " &
                            "UPDATE dbo.PostponementRequest " &
                            "SET [Status] = 'Cancelled', RespondedAt = SYSUTCDATETIME() " &
                            "WHERE BookingId = @BookingId AND [Status] = 'PendingTutorReview';"

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(applyMove, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@TutorSubjectId", SqlDbType.Int, tutorSubjectId)
                            DatabaseHelper.AddParameter(command, "@NewDate", SqlDbType.Date, newDate)
                            DatabaseHelper.AddParameter(command, "@NewStart", SqlDbType.Time, newStart)
                            DatabaseHelper.AddParameter(command, "@NewEnd", SqlDbType.Time, newEnd)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                            DatabaseHelper.AddParameter(command, "@OldAvailabilityId", SqlDbType.Int, oldAvailabilityId)
                            Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        If auditEntry IsNot Nothing Then
                            Await New AuditRepository().AppendAsync(
                                auditEntry, connection, transaction).ConfigureAwait(False)
                        End If

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
        ''' Cancels a Scheduled booking and releases its slot so someone else can take it.
        ''' </summary>
        ''' <param name="cancelledByPersonId">
        ''' Who cancelled - a student, the tutor or an administrator can all do it, and
        ''' "the session was cancelled" is not useful history without saying by whom.
        ''' </param>
        ''' <param name="reasonCategory">One of the fixed categories; the column constrains it.</param>
        ''' <param name="cancelReason">The optional free-text note beside the category.</param>
        ''' <param name="auditEntry">
        ''' Written inside this transaction. An audit record written outside it survives a
        ''' rollback and claims a cancellation that never happened.
        ''' </param>
        ''' <remarks>
        ''' Still Scheduled is a condition of the UPDATE, so a second click, a completed
        ''' session or an already-cancelled one changes nothing and returns False, rather
        ''' than being caught by a separate check that could be raced.
        ''' </remarks>
        Public Async Function CancelAsync(bookingId As Integer,
                                          cancelledByPersonId As Integer,
                                          reasonCategory As ScheduleChangeReason,
                                          cancelReason As String,
                                          Optional auditEntry As AuditEntry = Nothing,
                                          Optional audit As AuditRepository = Nothing) As Task(Of Boolean)
            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using transaction As SqlTransaction = connection.BeginTransaction()
                    Try
                        Const cancel As String =
                            "UPDATE dbo.Booking SET [Status] = 'Cancelled', CancelledAt = SYSUTCDATETIME(), " &
                            "CancelledByPersonId = @CancelledBy, CancelReasonCategory = @Category, " &
                            "CancelReason = @CancelReason " &
                            "WHERE BookingId = @BookingId AND [Status] = 'Scheduled';"

                        Dim affected As Integer
                        Using command As SqlCommand = DatabaseHelper.CreateCommand(cancel, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                            DatabaseHelper.AddParameter(command, "@CancelledBy", SqlDbType.Int, cancelledByPersonId)
                            DatabaseHelper.AddParameter(command, "@Category", SqlDbType.VarChar, 30,
                                                        reasonCategory.ToString())
                            DatabaseHelper.AddParameter(command, "@CancelReason", SqlDbType.NVarChar, 200,
                                                        DatabaseHelper.ToDbValue(cancelReason))
                            affected = Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        If affected <> 1 Then
                            transaction.Rollback()
                            Return False
                        End If

                        Const releaseSlot As String =
                            "UPDATE a SET a.IsBooked = 0 FROM dbo.TutorAvailability AS a " &
                            "JOIN dbo.Booking AS b ON b.AvailabilityId = a.AvailabilityId " &
                            "WHERE b.BookingId = @BookingId;"

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(releaseSlot, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                            Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        ' Any pending proposal to move this session is moot now, and leaving
                        ' it open would let a tutor later "approve" a cancelled booking.
                        Const closeProposals As String =
                            "UPDATE dbo.PostponementRequest " &
                            "SET [Status] = 'Cancelled', RespondedAt = SYSUTCDATETIME() " &
                            "WHERE BookingId = @BookingId AND [Status] = 'PendingTutorReview';"

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(closeProposals, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                            Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                        End Using

                        If auditEntry IsNot Nothing Then
                            Dim writer As AuditRepository = If(audit, New AuditRepository())
                            Await writer.AppendAsync(auditEntry, connection, transaction).ConfigureAwait(False)
                        End If

                        transaction.Commit()
                        Return True

                    Catch
                        Try
                            transaction.Rollback()
                        Catch
                            ' Already rolled back.
                        End Try
                        Throw
                    End Try
                End Using
            End Using
        End Function

#End Region

        Private Shared Async Function ReadAsync(sql As String,
                                                bindParameters As Action(Of SqlCommand)) As Task(Of IList(Of Booking))
            Dim results As New List(Of Booking)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    bindParameters(command)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New Booking With {
                                .BookingId = DatabaseHelper.GetInt32(reader, "BookingId"),
                                .RequestId = DatabaseHelper.GetInt32(reader, "RequestId"),
                                .AvailabilityId = DatabaseHelper.GetInt32(reader, "AvailabilityId"),
                                .BookingDate = DatabaseHelper.GetDate(reader, "BookingDate"),
                                .StartTime = DatabaseHelper.GetTime(reader, "StartTime"),
                                .EndTime = DatabaseHelper.GetTime(reader, "EndTime"),
                                .Status = EnumHelper.Parse(Of BookingStatus)(DatabaseHelper.GetString(reader, "Status")),
                                .CompletedAt = DatabaseHelper.GetNullableDate(reader, "CompletedAt"),
                                .CancelledAt = DatabaseHelper.GetNullableDate(reader, "CancelledAt"),
                                .CancelledByPersonId = DatabaseHelper.GetNullableInt32(reader, "CancelledByPersonId"),
                                .CancelledByName = DatabaseHelper.GetStringOrNothing(reader, "CancelledByName"),
                                .CancelReasonCategory = ParseReason(
                                    DatabaseHelper.GetStringOrNothing(reader, "CancelReasonCategory")),
                                .CancelReason = DatabaseHelper.GetStringOrNothing(reader, "CancelReason"),
                                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt"),
                                .StudentId = DatabaseHelper.GetInt32(reader, "StudentId"),
                                .TutorId = DatabaseHelper.GetInt32(reader, "TutorId"),
                                .SubjectName = DatabaseHelper.GetString(reader, "SubjectName"),
                                .StudyLevelName = DatabaseHelper.GetString(reader, "StudyLevelName"),
                                .RatePerSession = DatabaseHelper.GetDecimal(reader, "RatePerSession"),
                                .StudentName = DatabaseHelper.GetString(reader, "StudentName"),
                                .TutorName = DatabaseHelper.GetString(reader, "TutorName"),
                                .HasReview = DatabaseHelper.GetBoolean(reader, "HasReview")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

    End Class

End Namespace
