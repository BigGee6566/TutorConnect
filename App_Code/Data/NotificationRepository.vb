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
    ''' In-app notifications (BR-27). No email or SMS delivery is in scope.
    ''' </summary>
    Public Class NotificationRepository

        Private Const NotificationColumns As String =
            "NotificationId, PersonId, Title, [Message], NotificationType, LinkUrl, IsRead, CreatedAt"

        Public Async Function GetForPersonAsync(personId As Integer,
                                                Optional unreadOnly As Boolean = False,
                                                Optional maximumRows As Integer = 50) As Task(Of IList(Of Notification))
            Const sql As String =
                "SELECT TOP (@MaximumRows) " & NotificationColumns & " " &
                "FROM dbo.Notification " &
                "WHERE PersonId = @PersonId AND (@UnreadOnly = 0 OR IsRead = 0) " &
                "ORDER BY CreatedAt DESC;"

            Dim results As New List(Of Notification)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@MaximumRows", SqlDbType.Int, maximumRows)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    DatabaseHelper.AddParameter(command, "@UnreadOnly", SqlDbType.Bit, unreadOnly)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New Notification With {
                                .NotificationId = DatabaseHelper.GetInt32(reader, "NotificationId"),
                                .PersonId = DatabaseHelper.GetInt32(reader, "PersonId"),
                                .Title = DatabaseHelper.GetString(reader, "Title"),
                                .Message = DatabaseHelper.GetString(reader, "Message"),
                                .NotificationType = EnumHelper.Parse(Of NotificationType)(
                                    DatabaseHelper.GetString(reader, "NotificationType")),
                                .LinkUrl = DatabaseHelper.GetStringOrNothing(reader, "LinkUrl"),
                                .IsRead = DatabaseHelper.GetBoolean(reader, "IsRead"),
                                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        ''' <summary>Drives the unread count on the header bell.</summary>
        Public Async Function CountUnreadAsync(personId As Integer) As Task(Of Integer)
            Const sql As String =
                "SELECT COUNT(*) FROM dbo.Notification WHERE PersonId = @PersonId AND IsRead = 0;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        Public Async Function CreateAsync(notification As Notification) As Task(Of Integer)
            If notification Is Nothing Then Throw New ArgumentNullException(NameOf(notification))

            Const sql As String =
                "INSERT INTO dbo.Notification (PersonId, Title, [Message], NotificationType, LinkUrl) " &
                "VALUES (@PersonId, @Title, @Message, @NotificationType, @LinkUrl); " &
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, notification.PersonId)
                    DatabaseHelper.AddParameter(command, "@Title", SqlDbType.NVarChar, 150, notification.Title)
                    DatabaseHelper.AddParameter(command, "@Message", SqlDbType.NVarChar, 500, notification.Message)
                    DatabaseHelper.AddParameter(command, "@NotificationType", SqlDbType.VarChar, 30,
                                                notification.NotificationType.ToString())
                    DatabaseHelper.AddParameter(command, "@LinkUrl", SqlDbType.NVarChar, 300,
                                                DatabaseHelper.ToDbValue(notification.LinkUrl))

                    Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

        ''' <summary>Scoped by PersonId so one user cannot mark another's notification read.</summary>
        Public Async Function MarkReadAsync(notificationId As Integer, personId As Integer) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.Notification SET IsRead = 1 " &
                "WHERE NotificationId = @NotificationId AND PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@NotificationId", SqlDbType.Int, notificationId)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        Public Async Function MarkAllReadAsync(personId As Integer) As Task(Of Integer)
            Const sql As String =
                "UPDATE dbo.Notification SET IsRead = 1 WHERE PersonId = @PersonId AND IsRead = 0;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                End Using
            End Using
        End Function

    End Class

End Namespace
