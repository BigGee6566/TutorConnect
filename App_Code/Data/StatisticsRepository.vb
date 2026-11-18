Option Strict On
Option Explicit On

' System.Data is imported explicitly: this file lives in a namespace called Data, so an
' unqualified "Data.SqlDbType" would bind to TutorConnect.Data instead.
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports TutorConnect.Models

Namespace Data

    ''' <summary>
    ''' Aggregate figures for the administrator dashboard.
    ''' </summary>
    Public Class StatisticsRepository

        ''' <summary>
        ''' Reads all six dashboard figures in a single round trip.
        ''' </summary>
        ''' <remarks>
        ''' One statement with scalar subqueries rather than six separate commands: the
        ''' numbers are then read from the same point in time and cannot disagree with one
        ''' another on a busy instance.
        '''
        ''' "Requests today" and "bookings this week" are measured against the local date,
        ''' because that is how an administrator in Eastern Cape reads them, while
        ''' CreatedAt is stored in UTC. The offset is applied in the query rather than
        ''' comparing UTC to a local date.
        ''' </remarks>
        Public Async Function GetPlatformStatisticsAsync() As Task(Of PlatformStatistics)
            Const sql As String =
                "SELECT " &
                "  (SELECT COUNT(*) FROM dbo.Student) AS TotalStudents, " &
                "  (SELECT COUNT(*) FROM dbo.Tutor) AS TotalTutors, " &
                "  (SELECT COUNT(*) FROM dbo.Tutor WHERE IsApproved = 0) AS TutorsAwaitingApproval, " &
                "  (SELECT COUNT(*) FROM dbo.SessionRequest " &
                "     WHERE CAST(DATEADD(MINUTE, @UtcOffsetMinutes, CreatedAt) AS DATE) = @Today) AS RequestsToday, " &
                "  (SELECT COUNT(*) FROM dbo.Booking " &
                "     WHERE BookingDate >= @WeekStart AND BookingDate < @WeekEnd) AS BookingsThisWeek, " &
                "  (SELECT AVG(CAST(Rating AS DECIMAL(3,2))) FROM dbo.Review) AS AverageRating;"

            Dim today As Date = Date.Today
            ' Week runs Monday to Sunday.
            Dim daysSinceMonday As Integer = ((CInt(today.DayOfWeek) + 6) Mod 7)
            Dim weekStart As Date = today.AddDays(-daysSinceMonday)

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@UtcOffsetMinutes", SqlDbType.Int,
                                                CInt(TimeZoneInfo.Local.GetUtcOffset(Date.Now).TotalMinutes))
                    DatabaseHelper.AddParameter(command, "@Today", SqlDbType.Date, today)
                    DatabaseHelper.AddParameter(command, "@WeekStart", SqlDbType.Date, weekStart)
                    DatabaseHelper.AddParameter(command, "@WeekEnd", SqlDbType.Date, weekStart.AddDays(7))

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then
                            Return New PlatformStatistics()
                        End If

                        Return New PlatformStatistics With {
                            .TotalStudents = DatabaseHelper.GetInt32(reader, "TotalStudents"),
                            .TotalTutors = DatabaseHelper.GetInt32(reader, "TotalTutors"),
                            .TutorsAwaitingApproval = DatabaseHelper.GetInt32(reader, "TutorsAwaitingApproval"),
                            .RequestsToday = DatabaseHelper.GetInt32(reader, "RequestsToday"),
                            .BookingsThisWeek = DatabaseHelper.GetInt32(reader, "BookingsThisWeek"),
                            .AverageRating = DatabaseHelper.GetNullableDecimal(reader, "AverageRating")
                        }
                    End Using
                End Using
            End Using
        End Function

    End Class

End Namespace

