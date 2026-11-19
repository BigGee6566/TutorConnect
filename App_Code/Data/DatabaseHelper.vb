Option Strict On
Option Explicit On

Imports System.Configuration
Imports System.Data
Imports System.Threading.Tasks
Imports Npgsql
Imports NpgsqlTypes

Namespace Data

    ''' <summary>
    ''' The one place that knows how to open a connection, bind a parameter, and turn a
    ''' <see cref="NpgsqlDataReader"/> field into a typed CLR value.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' The database is PostgreSQL (Supabase). Repositories still describe parameters with
    ''' <see cref="SqlDbType"/>; <see cref="ToNpgsqlType"/> translates, so the repositories
    ''' did not have to change shape when the provider did.
    ''' </para>
    ''' <para>
    ''' Two rules the accessors below enforce:
    ''' <list type="number">
    ''' <item>DBNull is not Nothing. A NOT NULL column read as null is a schema/code
    ''' mismatch and throws; only the Nullable/OrNothing accessors tolerate a null.</item>
    ''' <item>SQL TIME is a TimeSpan, never a Date. SQL DATE is a Date with a zero
    ''' time component.</item>
    ''' </list>
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class DatabaseHelper

        Public Const ConnectionStringName As String = "TutorConnectDb"

        ''' <summary>appSettings key (in the git-ignored local.settings.config) holding the real connection string.</summary>
        Public Const ConnectionStringSettingName As String = "Db:ConnectionString"

        Shared Sub New()
            ' Timestamps are stored as "timestamp without time zone" holding UTC, as the
            ' SQL Server DATETIME2 columns did. Npgsql 6+ refuses to write a DateTime of
            ' Kind=Utc to that type unless the legacy behaviour is switched on.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", True)
        End Sub

        Private Sub New()
            ' Static helper; never instantiated.
        End Sub

        ''' <summary>
        ''' The application's connection string: Db:ConnectionString from appSettings when
        ''' present, otherwise the TutorConnectDb entry in Web.config (SEC-02).
        ''' </summary>
        Public Shared ReadOnly Property ConnectionString As String
            Get
                Dim fromSettings As String = ConfigurationManager.AppSettings(ConnectionStringSettingName)
                If Not String.IsNullOrWhiteSpace(fromSettings) Then Return fromSettings.Trim()

                Dim setting As ConnectionStringSettings =
                    ConfigurationManager.ConnectionStrings(ConnectionStringName)

                If setting Is Nothing OrElse String.IsNullOrWhiteSpace(setting.ConnectionString) Then
                    Throw New ConfigurationErrorsException(
                        $"Neither '{ConnectionStringSettingName}' nor connection string '{ConnectionStringName}' is configured.")
                End If

                Return setting.ConnectionString
            End Get
        End Property

        ''' <summary>
        ''' Creates and opens a connection. The caller owns it and must wrap it in Using.
        ''' </summary>
        Public Shared Async Function CreateOpenConnectionAsync() As Task(Of NpgsqlConnection)
            Dim connection As New NpgsqlConnection(ConnectionString)
            Try
                Await connection.OpenAsync().ConfigureAwait(False)
            Catch
                connection.Dispose()
                Throw
            End Try
            Return connection
        End Function

        ''' <summary>appSettings key that overrides <see cref="CommandTimeoutSeconds"/>.</summary>
        Public Const CommandTimeoutSettingName As String = "Sql:CommandTimeoutSeconds"

        ''' <summary>
        ''' How long a command may run, in seconds. Zero means no limit. A missing, blank,
        ''' unparseable or negative setting falls back to zero rather than throwing.
        ''' </summary>
        Public Shared ReadOnly Property CommandTimeoutSeconds As Integer
            Get
                Dim configured As String = ConfigurationManager.AppSettings(CommandTimeoutSettingName)

                Dim seconds As Integer
                If String.IsNullOrWhiteSpace(configured) OrElse
                   Not Integer.TryParse(configured.Trim(), seconds) OrElse
                   seconds < 0 Then
                    Return 0
                End If

                Return seconds
            End Get
        End Property

        ''' <summary>
        ''' Creates a command carrying the configured timeout, optionally enlisted in a
        ''' transaction. Every command in the application is built here.
        ''' </summary>
        Public Shared Function CreateCommand(sql As String,
                                             connection As NpgsqlConnection,
                                             Optional transaction As NpgsqlTransaction = Nothing) As NpgsqlCommand
            If connection Is Nothing Then Throw New ArgumentNullException(NameOf(connection))
            If String.IsNullOrWhiteSpace(sql) Then Throw New ArgumentNullException(NameOf(sql))

            Dim command As New NpgsqlCommand(sql, connection, transaction)
            Try
                command.CommandTimeout = CommandTimeoutSeconds
            Catch
                command.Dispose()
                Throw
            End Try
            Return command
        End Function

#Region "Errors"

        ''' <summary>True for a unique-constraint or unique-index violation (SQLSTATE 23505).</summary>
        Public Shared Function IsUniqueViolation(ex As Exception) As Boolean
            Return HasSqlState(ex, PostgresErrorCodes.UniqueViolation)
        End Function

        ''' <summary>True for a foreign-key violation (SQLSTATE 23503).</summary>
        Public Shared Function IsForeignKeyViolation(ex As Exception) As Boolean
            Return HasSqlState(ex, PostgresErrorCodes.ForeignKeyViolation)
        End Function

        ''' <summary>True for a deadlock or serialization failure: the transaction can be retried.</summary>
        Public Shared Function IsTransientConflict(ex As Exception) As Boolean
            Return HasSqlState(ex, PostgresErrorCodes.DeadlockDetected) OrElse
                   HasSqlState(ex, PostgresErrorCodes.SerializationFailure)
        End Function

        Private Shared Function HasSqlState(ex As Exception, sqlState As String) As Boolean
            Dim pg = TryCast(ex, PostgresException)
            Return pg IsNot Nothing AndAlso pg.SqlState = sqlState
        End Function

#End Region

#Region "Parameters"

        ''' <summary>Maps the SqlDbType the repositories use onto the PostgreSQL type.</summary>
        ''' <remarks>
        ''' Two deliberate choices mirror SQL Server's behaviour, so the SQL written for it
        ''' keeps its meaning:
        ''' <list type="bullet">
        ''' <item>Flags are SMALLINT 0/1, as BIT was, so "IsActive = 1" stays valid SQL.</item>
        ''' <item>Text columns are CITEXT. SQL Server's default collation is
        ''' case-insensitive, and sign-in, the uniqueness rules (BR-01) and every search
        ''' depend on that. Binding text parameters as citext keeps comparisons with
        ''' those columns case-insensitive too.</item>
        ''' </list>
        ''' </remarks>
        Private Shared Function ToNpgsqlType(type As SqlDbType) As NpgsqlDbType
            Select Case type
                Case SqlDbType.Int : Return NpgsqlDbType.Integer
                Case SqlDbType.BigInt : Return NpgsqlDbType.Bigint
                Case SqlDbType.SmallInt, SqlDbType.TinyInt, SqlDbType.Bit : Return NpgsqlDbType.Smallint
                Case SqlDbType.Decimal, SqlDbType.Money, SqlDbType.SmallMoney : Return NpgsqlDbType.Numeric
                Case SqlDbType.Float : Return NpgsqlDbType.Double
                Case SqlDbType.Real : Return NpgsqlDbType.Real
                Case SqlDbType.Date : Return NpgsqlDbType.Date
                Case SqlDbType.Time : Return NpgsqlDbType.Time
                Case SqlDbType.DateTime, SqlDbType.DateTime2, SqlDbType.SmallDateTime : Return NpgsqlDbType.Timestamp
                Case SqlDbType.DateTimeOffset : Return NpgsqlDbType.TimestampTz
                Case SqlDbType.VarBinary, SqlDbType.Binary, SqlDbType.Image : Return NpgsqlDbType.Bytea
                Case SqlDbType.UniqueIdentifier : Return NpgsqlDbType.Uuid
                Case Else : Return NpgsqlDbType.Citext
            End Select
        End Function

        ''' <summary>Adds a parameter with an explicit type and size (SEC-03).</summary>
        Public Shared Function AddParameter(command As NpgsqlCommand,
                                            name As String,
                                            type As SqlDbType,
                                            size As Integer,
                                            value As Object) As NpgsqlParameter
            If command Is Nothing Then Throw New ArgumentNullException(NameOf(command))

            Dim parameter As New NpgsqlParameter(name, ToNpgsqlType(type))
            If size > 0 Then parameter.Size = size
            parameter.Value = NormaliseValue(type, value)
            command.Parameters.Add(parameter)
            Return parameter
        End Function

        ''' <summary>Adds a fixed-width parameter (Int, Bit, Date, Time, DateTime2...).</summary>
        Public Shared Function AddParameter(command As NpgsqlCommand,
                                            name As String,
                                            type As SqlDbType,
                                            value As Object) As NpgsqlParameter
            Return AddParameter(command, name, type, 0, value)
        End Function

        ''' <summary>
        ''' Npgsql is strict about CLR types: a Byte bound as smallint, or a whole DateTime
        ''' bound as date, must be converted first.
        ''' </summary>
        Private Shared Function NormaliseValue(type As SqlDbType, value As Object) As Object
            If value Is Nothing OrElse TypeOf value Is DBNull Then Return DBNull.Value

            Select Case type
                Case SqlDbType.TinyInt, SqlDbType.SmallInt, SqlDbType.Bit
                    Return Convert.ToInt16(value)
                Case SqlDbType.Date
                    If TypeOf value Is Date Then Return DirectCast(value, Date).Date
            End Select
            Return value
        End Function

        ''' <summary>Adds a NUMERIC parameter, so a money value is not silently rounded on the way in.</summary>
        Public Shared Function AddDecimalParameter(command As NpgsqlCommand,
                                                   name As String,
                                                   value As Decimal,
                                                   Optional precision As Byte = 10,
                                                   Optional scale As Byte = 2) As NpgsqlParameter
            If command Is Nothing Then Throw New ArgumentNullException(NameOf(command))

            Dim parameter As New NpgsqlParameter(name, NpgsqlDbType.Numeric) With {
                .Precision = precision,
                .Scale = scale,
                .Value = value
            }
            command.Parameters.Add(parameter)
            Return parameter
        End Function

        ''' <summary>Wraps a value that may be Nothing for the database.</summary>
        Public Shared Function ToDbValue(value As Object) As Object
            If value Is Nothing Then Return DBNull.Value

            Dim text As String = TryCast(value, String)
            If text IsNot Nothing AndAlso text.Length = 0 Then Return DBNull.Value

            Return value
        End Function

        ''' <summary>Wraps a nullable date for the database.</summary>
        Public Shared Function ToDbValue(value As Date?) As Object
            Return If(value.HasValue, CObj(value.Value), CObj(DBNull.Value))
        End Function

        ''' <summary>Wraps a nullable integer for the database.</summary>
        Public Shared Function ToDbValue(value As Integer?) As Object
            Return If(value.HasValue, CObj(value.Value), CObj(DBNull.Value))
        End Function

#End Region

#Region "Typed reader accessors - required (throw on null)"

        Private Shared Function RequireOrdinal(reader As NpgsqlDataReader, columnName As String) As Integer
            If reader Is Nothing Then Throw New ArgumentNullException(NameOf(reader))

            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            If reader.IsDBNull(ordinal) Then
                Throw New InvalidOperationException(
                    $"Column '{columnName}' returned NULL but is declared NOT NULL. " &
                    "The query and the schema disagree.")
            End If
            Return ordinal
        End Function

        ''' <summary>Reads any integer column as Int32 (COUNT(*) comes back as bigint in PostgreSQL).</summary>
        Public Shared Function GetInt32(reader As NpgsqlDataReader, columnName As String) As Integer
            Return Convert.ToInt32(reader.GetValue(RequireOrdinal(reader, columnName)))
        End Function

        Public Shared Function GetByte(reader As NpgsqlDataReader, columnName As String) As Byte
            Return Convert.ToByte(reader.GetValue(RequireOrdinal(reader, columnName)))
        End Function

        Public Shared Function GetBoolean(reader As NpgsqlDataReader, columnName As String) As Boolean
            Return Convert.ToBoolean(reader.GetValue(RequireOrdinal(reader, columnName)))
        End Function

        Public Shared Function GetDecimal(reader As NpgsqlDataReader, columnName As String) As Decimal
            Return Convert.ToDecimal(reader.GetValue(RequireOrdinal(reader, columnName)))
        End Function

        Public Shared Function GetString(reader As NpgsqlDataReader, columnName As String) As String
            Return reader.GetString(RequireOrdinal(reader, columnName))
        End Function

        ''' <summary>Reads a DATE or TIMESTAMP column.</summary>
        Public Shared Function GetDate(reader As NpgsqlDataReader, columnName As String) As Date
            Return reader.GetDateTime(RequireOrdinal(reader, columnName))
        End Function

        ''' <summary>Reads a TIME column as a TimeSpan.</summary>
        Public Shared Function GetTime(reader As NpgsqlDataReader, columnName As String) As TimeSpan
            Return reader.GetTimeSpan(RequireOrdinal(reader, columnName))
        End Function

        ''' <summary>Reads a BYTEA column, e.g. a password hash or salt.</summary>
        Public Shared Function GetBytes(reader As NpgsqlDataReader, columnName As String) As Byte()
            Dim ordinal As Integer = RequireOrdinal(reader, columnName)
            Return DirectCast(reader.GetValue(ordinal), Byte())
        End Function

#End Region

#Region "Typed reader accessors - nullable"

        ''' <summary>Returns Nothing for a NULL string column, never String.Empty.</summary>
        Public Shared Function GetStringOrNothing(reader As NpgsqlDataReader, columnName As String) As String
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), Nothing, reader.GetString(ordinal))
        End Function

        Public Shared Function GetNullableInt32(reader As NpgsqlDataReader, columnName As String) As Integer?
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), CType(Nothing, Integer?), Convert.ToInt32(reader.GetValue(ordinal)))
        End Function

        Public Shared Function GetNullableDate(reader As NpgsqlDataReader, columnName As String) As Date?
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), CType(Nothing, Date?), reader.GetDateTime(ordinal))
        End Function

        Public Shared Function GetNullableDecimal(reader As NpgsqlDataReader, columnName As String) As Decimal?
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), CType(Nothing, Decimal?), Convert.ToDecimal(reader.GetValue(ordinal)))
        End Function

        ''' <summary>True when the reader exposes a column by this name (case-insensitive).</summary>
        Public Shared Function HasColumn(reader As NpgsqlDataReader, columnName As String) As Boolean
            If reader Is Nothing Then Return False

            For i As Integer = 0 To reader.FieldCount - 1
                If String.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
            Next
            Return False
        End Function

#End Region

    End Class

End Namespace
