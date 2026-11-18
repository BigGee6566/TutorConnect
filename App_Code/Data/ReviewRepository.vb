Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports TutorConnect.Models

Namespace Data

    ''' <summary>
    ''' Reviews. One per Completed booking, at most.
    ''' </summary>
    ''' <remarks>
    ''' Review has no StudentId column. Every read derives the reviewing student through
    ''' Booking -> SessionRequest -> Student, which is also how ownership is checked -
    ''' there is no stored value that could disagree.
    ''' </remarks>
    Public Class ReviewRepository

        Private Const ReviewColumns As String =
            "rv.ReviewId, rv.BookingId, rv.Rating, rv.Comment, rv.CreatedAt, " &
            "rv.IsHidden, rv.HiddenReason, rv.HiddenAt, " &
            "b.BookingDate AS SessionDate, b.StartTime, b.EndTime, " &
            "sub.SubjectName, sl.StudyLevelName, " &
            "sp.FirstName + ' ' + sp.LastName AS StudentName, " &
            "tp.FirstName + ' ' + tp.LastName AS TutorName"

        Private Const ReviewJoins As String =
            "FROM dbo.Review AS rv " &
            "JOIN dbo.Booking        AS b   ON b.BookingId = rv.BookingId " &
            "JOIN dbo.SessionRequest AS r   ON r.RequestId = b.RequestId " &
            "JOIN dbo.TutorSubject   AS ts  ON ts.TutorSubjectId = r.TutorSubjectId " &
            "JOIN dbo.[Subject]      AS sub ON sub.SubjectId = ts.SubjectId " &
            "JOIN dbo.StudyLevel     AS sl  ON sl.StudyLevelId = ts.StudyLevelId " &
            "JOIN dbo.Tutor          AS t   ON t.TutorId = ts.TutorId " &
            "JOIN dbo.Person         AS tp  ON tp.PersonId = t.PersonId " &
            "JOIN dbo.Student        AS s   ON s.StudentId = r.StudentId " &
            "JOIN dbo.Person         AS sp  ON sp.PersonId = s.PersonId "

        ''' <summary>The student's own reviews.</summary>
        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of IList(Of Review))
            Const sql As String =
                "SELECT " & ReviewColumns & " " & ReviewJoins &
                "WHERE r.StudentId = @StudentId ORDER BY rv.CreatedAt DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                                        End Sub).ConfigureAwait(False)
        End Function

        ''' <summary>Reviews about one tutor. Read-only to the tutor - never editable.</summary>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of IList(Of Review))
            Const sql As String =
                "SELECT " & ReviewColumns & " " & ReviewJoins &
                "WHERE ts.TutorId = @TutorId AND rv.IsHidden = 0 ORDER BY rv.CreatedAt DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)
                                        End Sub).ConfigureAwait(False)
        End Function

        Public Async Function GetByBookingAsync(bookingId As Integer) As Task(Of Review)
            Const sql As String =
                "SELECT " & ReviewColumns & " " & ReviewJoins & "WHERE rv.BookingId = @BookingId;"

            Dim rows As IList(Of Review) =
                Await ReadAsync(sql, Sub(command)
                                         DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                                     End Sub).ConfigureAwait(False)

            Return If(rows.Count = 0, Nothing, rows(0))
        End Function

        ''' <summary>
        ''' BR-16 to BR-19 in one query: the booking must exist, belong to this student,
        ''' be Completed, and have no review yet. Returns True only when all four hold.
        ''' </summary>
        Public Async Function BookingIsReviewableAsync(bookingId As Integer, studentId As Integer) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.Booking AS b " &
                "JOIN dbo.SessionRequest AS r ON r.RequestId = b.RequestId " &
                "WHERE b.BookingId = @BookingId " &
                "  AND r.StudentId = @StudentId " &
                "  AND b.[Status] = 'Completed' " &
                "  AND NOT EXISTS (SELECT 1 FROM dbo.Review AS rv WHERE rv.BookingId = b.BookingId);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Inserts a review, re-checking reviewability inside the INSERT itself.
        ''' </summary>
        ''' <remarks>
        ''' The WHERE EXISTS makes the check and the write one statement, so a second
        ''' submission cannot slip between them. UQ_Review_Booking is the final backstop
        ''' (AT-21) - this simply turns that race into a clean "0 rows" rather than a
        ''' constraint violation.
        ''' </remarks>
        ''' <returns>The new ReviewId, or 0 when the booking was not reviewable.</returns>
        Public Async Function CreateAsync(bookingId As Integer,
                                          studentId As Integer,
                                          rating As Byte,
                                          comment As String) As Task(Of Integer)
            Const sql As String =
                "INSERT INTO dbo.Review (BookingId, Rating, Comment) " &
                "SELECT @BookingId, @Rating, @Comment " &
                "WHERE EXISTS (" &
                "    SELECT 1 FROM dbo.Booking AS b " &
                "    JOIN dbo.SessionRequest AS r ON r.RequestId = b.RequestId " &
                "    WHERE b.BookingId = @BookingId " &
                "      AND r.StudentId = @StudentId " &
                "      AND b.[Status] = 'Completed' " &
                "      AND NOT EXISTS (SELECT 1 FROM dbo.Review AS rv WHERE rv.BookingId = b.BookingId)); " &
                "SELECT CAST(ISNULL(SCOPE_IDENTITY(), 0) AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@BookingId", SqlDbType.Int, bookingId)
                    DatabaseHelper.AddParameter(command, "@StudentId", SqlDbType.Int, studentId)
                    DatabaseHelper.AddParameter(command, "@Rating", SqlDbType.TinyInt, rating)
                    DatabaseHelper.AddParameter(command, "@Comment", SqlDbType.NVarChar, 500,
                                                DatabaseHelper.ToDbValue(comment))

                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Every review, for the administrator's list. Includes hidden ones - reviewing
        ''' what has been hidden is the point of the screen.
        ''' </summary>
        Public Async Function GetAllAsync() As Task(Of IList(Of Review))
            Const sql As String =
                "SELECT " & ReviewColumns & " " & ReviewJoins & "ORDER BY rv.CreatedAt DESC;"

            Return Await ReadAsync(sql, Sub(command)
                                            ' No filters: the administrator sees everything.
                                        End Sub).ConfigureAwait(False)
        End Function

        ''' <summary>
        ''' Hides or unhides a review, recording who did it and why.
        ''' </summary>
        ''' <remarks>
        ''' Nothing is deleted, so the audit trail survives and the tutor's history stays
        ''' intact. Unhiding clears the reason rather than leaving a stale explanation
        ''' attached to a visible review.
        ''' </remarks>
        Public Async Function SetHiddenAsync(reviewId As Integer,
                                             isHidden As Boolean,
                                             reason As String,
                                             administratorPersonId As Integer) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.Review " &
                "SET IsHidden = @IsHidden, " &
                "    HiddenReason = CASE WHEN @IsHidden = 1 THEN @Reason ELSE NULL END, " &
                "    HiddenAt     = CASE WHEN @IsHidden = 1 THEN SYSUTCDATETIME() ELSE NULL END, " &
                "    HiddenBy     = CASE WHEN @IsHidden = 1 THEN @AdministratorPersonId ELSE NULL END " &
                "WHERE ReviewId = @ReviewId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@IsHidden", SqlDbType.Bit, isHidden)
                    DatabaseHelper.AddParameter(command, "@Reason", SqlDbType.NVarChar, 200,
                                                DatabaseHelper.ToDbValue(reason))
                    DatabaseHelper.AddParameter(command, "@AdministratorPersonId", SqlDbType.Int, administratorPersonId)
                    DatabaseHelper.AddParameter(command, "@ReviewId", SqlDbType.Int, reviewId)

                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        ''' <summary>Average rating and review count for one tutor.</summary>
        Public Async Function GetTutorRatingAsync(tutorId As Integer) As Task(Of TutorRatingSummary)
            Const sql As String =
                "SELECT AVG(CAST(rv.Rating AS DECIMAL(3,2))) AS AverageRating, COUNT(*) AS ReviewCount " &
                "FROM dbo.Review AS rv " &
                "JOIN dbo.Booking        AS b  ON b.BookingId = rv.BookingId " &
                "JOIN dbo.SessionRequest AS r  ON r.RequestId = b.RequestId " &
                "JOIN dbo.TutorSubject   AS ts ON ts.TutorSubjectId = r.TutorSubjectId " &
                "WHERE ts.TutorId = @TutorId AND rv.IsHidden = 0;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@TutorId", SqlDbType.Int, tutorId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then
                            Return New TutorRatingSummary With {.TutorId = tutorId, .ReviewCount = 0}
                        End If

                        Return New TutorRatingSummary With {
                            .TutorId = tutorId,
                            .AverageRating = DatabaseHelper.GetNullableDecimal(reader, "AverageRating"),
                            .ReviewCount = DatabaseHelper.GetInt32(reader, "ReviewCount")
                        }
                    End Using
                End Using
            End Using
        End Function

        Private Shared Async Function ReadAsync(sql As String,
                                                bindParameters As Action(Of SqlCommand)) As Task(Of IList(Of Review))
            Dim results As New List(Of Review)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    bindParameters(command)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New Review With {
                                .ReviewId = DatabaseHelper.GetInt32(reader, "ReviewId"),
                                .BookingId = DatabaseHelper.GetInt32(reader, "BookingId"),
                                .Rating = DatabaseHelper.GetByte(reader, "Rating"),
                                .Comment = DatabaseHelper.GetStringOrNothing(reader, "Comment"),
                                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt"),
                                .IsHidden = DatabaseHelper.GetBoolean(reader, "IsHidden"),
                                .HiddenReason = DatabaseHelper.GetStringOrNothing(reader, "HiddenReason"),
                                .HiddenAt = DatabaseHelper.GetNullableDate(reader, "HiddenAt"),
                                .SessionDate = DatabaseHelper.GetDate(reader, "SessionDate"),
                                .StartTime = DatabaseHelper.GetTime(reader, "StartTime"),
                                .EndTime = DatabaseHelper.GetTime(reader, "EndTime"),
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
