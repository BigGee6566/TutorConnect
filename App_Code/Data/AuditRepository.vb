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
    ''' Reads and appends audit records.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' There is deliberately no update and no delete here, and none anywhere else in the
    ''' codebase. "Ordinary users cannot modify audit records" is not enforced by a
    ''' permission check that could be got around - the application simply offers no route
    ''' to change one, and reading them is confined to the administrator area by the
    ''' Web.config location rule.
    ''' </para>
    ''' <para>
    ''' <see cref="AppendAsync"/> optionally joins an existing transaction. An audit record
    ''' written outside the transaction it describes can survive a rollback and claim
    ''' something happened that did not.
    ''' </para>
    ''' </remarks>
    Public Class AuditRepository

        Private Const InsertSql As String =
            "INSERT INTO dbo.AuditLog " &
            "(ActorPersonId, ActorRole, ActionType, EntityType, EntityId, " &
            " PreviousState, NewState, Reason, ResponseNote, IpAddress) " &
            "VALUES (@ActorPersonId, @ActorRole, @ActionType, @EntityType, @EntityId, " &
            "        @PreviousState, @NewState, @Reason, @ResponseNote, @IpAddress);"

        ''' <summary>Appends one record, optionally inside a caller's transaction.</summary>
        Public Async Function AppendAsync(entry As AuditEntry,
                                          Optional connection As SqlConnection = Nothing,
                                          Optional transaction As SqlTransaction = Nothing) As Task
            If entry Is Nothing Then Throw New ArgumentNullException(NameOf(entry))

            If connection IsNot Nothing Then
                Await WriteAsync(entry, connection, transaction).ConfigureAwait(False)
                Return
            End If

            Using own As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Await WriteAsync(entry, own, Nothing).ConfigureAwait(False)
            End Using
        End Function

        Private Shared Async Function WriteAsync(entry As AuditEntry,
                                                 connection As SqlConnection,
                                                 transaction As SqlTransaction) As Task
            Using command As SqlCommand = DatabaseHelper.CreateCommand(InsertSql, connection, transaction)
                DatabaseHelper.AddParameter(command, "@ActorPersonId", SqlDbType.Int,
                                            DatabaseHelper.ToDbValue(entry.ActorPersonId))
                DatabaseHelper.AddParameter(command, "@ActorRole", SqlDbType.VarChar, 15,
                                            If(entry.ActorRole, "System"))
                DatabaseHelper.AddParameter(command, "@ActionType", SqlDbType.VarChar, 40,
                                            entry.ActionType.ToString())
                DatabaseHelper.AddParameter(command, "@EntityType", SqlDbType.VarChar, 30, entry.EntityType)
                DatabaseHelper.AddParameter(command, "@EntityId", SqlDbType.Int, entry.EntityId)
                DatabaseHelper.AddParameter(command, "@PreviousState", SqlDbType.NVarChar, 400,
                                            DatabaseHelper.ToDbValue(entry.PreviousState))
                DatabaseHelper.AddParameter(command, "@NewState", SqlDbType.NVarChar, 400,
                                            DatabaseHelper.ToDbValue(entry.NewState))
                DatabaseHelper.AddParameter(command, "@Reason", SqlDbType.NVarChar, 200,
                                            DatabaseHelper.ToDbValue(entry.Reason))
                DatabaseHelper.AddParameter(command, "@ResponseNote", SqlDbType.NVarChar, 200,
                                            DatabaseHelper.ToDbValue(entry.ResponseNote))
                DatabaseHelper.AddParameter(command, "@IpAddress", SqlDbType.VarChar, 45,
                                            DatabaseHelper.ToDbValue(entry.IpAddress))

                Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
            End Using
        End Function

        ''' <summary>
        ''' Newest first, optionally narrowed by action, entity or actor.
        ''' </summary>
        ''' <remarks>
        ''' Filters are written as (@Param IS NULL OR column = @Param) rather than assembled
        ''' conditionally, so there is one statement and one plan whatever is filled in.
        ''' </remarks>
        Public Async Function SearchAsync(actionType As String,
                                          entityType As String,
                                          actorPersonId As Integer,
                                          Optional maximumRows As Integer = 200) As Task(Of IList(Of AuditEntry))
            Const sql As String =
                "SELECT TOP (@MaximumRows) a.AuditId, a.ActorPersonId, a.ActorRole, a.ActionType, " &
                "       a.EntityType, a.EntityId, a.PreviousState, a.NewState, a.Reason, " &
                "       a.ResponseNote, a.IpAddress, a.CreatedAt, " &
                "       p.FirstName + ' ' + p.LastName AS ActorName " &
                "FROM dbo.AuditLog AS a " &
                "LEFT JOIN dbo.Person AS p ON p.PersonId = a.ActorPersonId " &
                "WHERE (@ActionType IS NULL OR a.ActionType = @ActionType) " &
                "  AND (@EntityType IS NULL OR a.EntityType = @EntityType) " &
                "  AND (@ActorPersonId = 0 OR a.ActorPersonId = @ActorPersonId) " &
                "ORDER BY a.CreatedAt DESC, a.AuditId DESC;"

            Dim results As New List(Of AuditEntry)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@MaximumRows", SqlDbType.Int, maximumRows)
                    DatabaseHelper.AddParameter(command, "@ActionType", SqlDbType.VarChar, 40,
                                                DatabaseHelper.ToDbValue(actionType))
                    DatabaseHelper.AddParameter(command, "@EntityType", SqlDbType.VarChar, 30,
                                                DatabaseHelper.ToDbValue(entityType))
                    DatabaseHelper.AddParameter(command, "@ActorPersonId", SqlDbType.Int, actorPersonId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(New AuditEntry With {
                                .AuditId = DatabaseHelper.GetInt32(reader, "AuditId"),
                                .ActorPersonId = DatabaseHelper.GetNullableInt32(reader, "ActorPersonId"),
                                .ActorRole = DatabaseHelper.GetString(reader, "ActorRole"),
                                .ActorName = DatabaseHelper.GetStringOrNothing(reader, "ActorName"),
                                .ActionType = EnumHelper.Parse(Of AuditAction)(
                                    DatabaseHelper.GetString(reader, "ActionType")),
                                .EntityType = DatabaseHelper.GetString(reader, "EntityType"),
                                .EntityId = DatabaseHelper.GetInt32(reader, "EntityId"),
                                .PreviousState = DatabaseHelper.GetStringOrNothing(reader, "PreviousState"),
                                .NewState = DatabaseHelper.GetStringOrNothing(reader, "NewState"),
                                .Reason = DatabaseHelper.GetStringOrNothing(reader, "Reason"),
                                .ResponseNote = DatabaseHelper.GetStringOrNothing(reader, "ResponseNote"),
                                .IpAddress = DatabaseHelper.GetStringOrNothing(reader, "IpAddress"),
                                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt")
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        ''' <summary>Counts the records written for one entity - used by the tests.</summary>
        Public Async Function CountForEntityAsync(entityType As String, entityId As Integer) As Task(Of Integer)
            Const sql As String =
                "SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityType = @EntityType AND EntityId = @EntityId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@EntityType", SqlDbType.VarChar, 30, entityType)
                    DatabaseHelper.AddParameter(command, "@EntityId", SqlDbType.Int, entityId)

                    Return Convert.ToInt32(Await command.ExecuteScalarAsync().ConfigureAwait(False),
                                           Globalization.CultureInfo.InvariantCulture)
                End Using
            End Using
        End Function

    End Class

End Namespace
