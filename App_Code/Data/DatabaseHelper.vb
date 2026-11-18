Option Strict On
Option Explicit On

Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks

Namespace Data

    ''' <summary>
    ''' The one place that knows how to open a connection, bind a parameter, and turn a
    ''' <see cref="SqlDataReader"/> field into a typed CLR value.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' Under <c>Option Strict On</c> a reader field is an <c>Object</c> and will not
    ''' implicitly narrow, so every read needs an explicit, correctly typed accessor.
    ''' Doing that inline in seven repositories would guarantee seven different
    ''' null-handling conventions. It is centralised here instead.
    ''' </para>
    ''' <para>
    ''' Two rules the accessors below enforce:
    ''' <list type="number">
    ''' <item>DBNull is not Nothing. A NOT NULL column read as null is a schema/code
    ''' mismatch and throws; only the Nullable/OrNothing accessors tolerate a null.</item>
    ''' <item>SQL TIME(0) is a TimeSpan, never a Date. SQL DATE is a Date with a zero
    ''' time component.</item>
    ''' </list>
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class DatabaseHelper

        Public Const ConnectionStringName As String = "TutorConnectDb"

        Private Sub New()
            ' Static helper; never instantiated.
        End Sub

        ''' <summary>
        ''' The application's connection string, from Web.config (SEC-02). Never a literal.
        ''' </summary>
        ''' <exception cref="ConfigurationErrorsException">The entry is missing or empty.</exception>
        Public Shared ReadOnly Property ConnectionString As String
            Get
                Dim setting As ConnectionStringSettings =
                    ConfigurationManager.ConnectionStrings(ConnectionStringName)

                If setting Is Nothing OrElse String.IsNullOrWhiteSpace(setting.ConnectionString) Then
                    Throw New ConfigurationErrorsException(
                        $"Connection string '{ConnectionStringName}' is missing from Web.config.")
                End If

                Return setting.ConnectionString
            End Get
        End Property

        ''' <summary>
        ''' Creates and opens a connection. The caller owns it and must wrap it in Using.
        ''' </summary>
        Public Shared Async Function CreateOpenConnectionAsync() As Task(Of SqlConnection)
            Dim connection As New SqlConnection(ConnectionString)
            Try
                Await connection.OpenAsync().ConfigureAwait(False)
            Catch
                ' Do not leak a half-constructed connection if OpenAsync throws.
                connection.Dispose()
                Throw
            End Try
            Return connection
        End Function

        ''' <summary>appSettings key that overrides <see cref="CommandTimeoutSeconds"/>.</summary>
        Public Const CommandTimeoutSettingName As String = "Sql:CommandTimeoutSeconds"

        ''' <summary>
        ''' How long a command may run, in seconds. Zero means no limit.
        ''' </summary>
        ''' <remarks>
        ''' <para>
        ''' The default is zero: commands wait indefinitely rather than failing after the
        ''' provider's thirty seconds. That is deliberate, but it is not free - an
        ''' unlimited timeout in a web application means a query that never returns holds
        ''' an ASP.NET request thread for ever. The value is therefore read from
        ''' appSettings so it can be dialled back on a machine that needs it without a
        ''' rebuild; put it in the git-ignored App_Data\local.settings.config.
        ''' </para>
        ''' <para>
        ''' A missing, blank, unparseable or negative setting falls back to zero rather
        ''' than throwing. A configuration typo should not take the application down.
        ''' </para>
        ''' <para>
        ''' This cannot be expressed in the connection string. System.Data.SqlClient on
        ''' .NET Framework accepts "Connect Timeout" but not "Command Timeout" - that
        ''' keyword arrived with Microsoft.Data.SqlClient 2.0 and throws
        ''' "Keyword not supported" here.
        ''' </para>
        ''' </remarks>
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
        ''' transaction.
        ''' </summary>
        ''' <remarks>
        ''' Every command in the application is built here. Setting the timeout at each of
        ''' the call sites instead would guarantee that one of them is eventually missed,
        ''' and the miss would only show up as an intermittent timeout under load.
        ''' </remarks>
        Public Shared Function CreateCommand(sql As String,
                                             connection As SqlConnection,
                                             Optional transaction As SqlTransaction = Nothing) As SqlCommand
            If connection Is Nothing Then Throw New ArgumentNullException(NameOf(connection))
            If String.IsNullOrWhiteSpace(sql) Then Throw New ArgumentNullException(NameOf(sql))

            Dim command As New SqlCommand(sql, connection, transaction)
            Try
                command.CommandTimeout = CommandTimeoutSeconds
            Catch
                ' Do not leak a command the caller never received and cannot dispose.
                command.Dispose()
                Throw
            End Try
            Return command
        End Function

#Region "Parameters"

        ''' <summary>
        ''' Adds a parameter with an explicit type and size (SEC-03).
        ''' </summary>
        ''' <remarks>
        ''' Size matters for variable-length types: letting ADO.NET infer it from the
        ''' current value produces a different plan for every distinct input length and
        ''' thrashes the plan cache.
        ''' </remarks>
        Public Shared Function AddParameter(command As SqlCommand,
                                            name As String,
                                            type As SqlDbType,
                                            size As Integer,
                                            value As Object) As SqlParameter
            If command Is Nothing Then Throw New ArgumentNullException(NameOf(command))

            Dim parameter As New SqlParameter(name, type)
            If size > 0 Then parameter.Size = size
            parameter.Value = If(value, DBNull.Value)
            command.Parameters.Add(parameter)
            Return parameter
        End Function

        ''' <summary>Adds a fixed-width parameter (Int, Bit, Date, Time, DateTime2...).</summary>
        Public Shared Function AddParameter(command As SqlCommand,
                                            name As String,
                                            type As SqlDbType,
                                            value As Object) As SqlParameter
            Return AddParameter(command, name, type, 0, value)
        End Function

        ''' <summary>
        ''' Adds a DECIMAL parameter with explicit precision and scale, so a money value
        ''' is not silently rounded on the way in.
        ''' </summary>
        Public Shared Function AddDecimalParameter(command As SqlCommand,
                                                   name As String,
                                                   value As Decimal,
                                                   Optional precision As Byte = 10,
                                                   Optional scale As Byte = 2) As SqlParameter
            If command Is Nothing Then Throw New ArgumentNullException(NameOf(command))

            Dim parameter As New SqlParameter(name, SqlDbType.Decimal) With {
                .Precision = precision,
                .Scale = scale,
                .Value = value
            }
            command.Parameters.Add(parameter)
            Return parameter
        End Function

        ''' <summary>
        ''' Wraps a value that may be Nothing for the database. Use for optional strings
        ''' and reference types.
        ''' </summary>
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

        Private Shared Function RequireOrdinal(reader As SqlDataReader, columnName As String) As Integer
            If reader Is Nothing Then Throw New ArgumentNullException(NameOf(reader))

            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            If reader.IsDBNull(ordinal) Then
                Throw New InvalidOperationException(
                    $"Column '{columnName}' returned NULL but is declared NOT NULL. " &
                    "The query and the schema disagree.")
            End If
            Return ordinal
        End Function

        Public Shared Function GetInt32(reader As SqlDataReader, columnName As String) As Integer
            Return reader.GetInt32(RequireOrdinal(reader, columnName))
        End Function

        Public Shared Function GetByte(reader As SqlDataReader, columnName As String) As Byte
            Return reader.GetByte(RequireOrdinal(reader, columnName))
        End Function

        Public Shared Function GetBoolean(reader As SqlDataReader, columnName As String) As Boolean
            Return reader.GetBoolean(RequireOrdinal(reader, columnName))
        End Function

        Public Shared Function GetDecimal(reader As SqlDataReader, columnName As String) As Decimal
            Return reader.GetDecimal(RequireOrdinal(reader, columnName))
        End Function

        Public Shared Function GetString(reader As SqlDataReader, columnName As String) As String
            Return reader.GetString(RequireOrdinal(reader, columnName))
        End Function

        ''' <summary>Reads a SQL DATE or DATETIME2 column.</summary>
        Public Shared Function GetDate(reader As SqlDataReader, columnName As String) As Date
            Return reader.GetDateTime(RequireOrdinal(reader, columnName))
        End Function

        ''' <summary>
        ''' Reads a SQL TIME(0) column. The provider surfaces TIME as TimeSpan; asking for
        ''' GetDateTime here throws an InvalidCastException at run time.
        ''' </summary>
        Public Shared Function GetTime(reader As SqlDataReader, columnName As String) As TimeSpan
            Return reader.GetTimeSpan(RequireOrdinal(reader, columnName))
        End Function

        ''' <summary>Reads a VARBINARY column, e.g. a password hash or salt.</summary>
        Public Shared Function GetBytes(reader As SqlDataReader, columnName As String) As Byte()
            Dim ordinal As Integer = RequireOrdinal(reader, columnName)
            Return DirectCast(reader.GetValue(ordinal), Byte())
        End Function

#End Region

#Region "Typed reader accessors - nullable"

        ''' <summary>Returns Nothing for a NULL string column, never String.Empty.</summary>
        Public Shared Function GetStringOrNothing(reader As SqlDataReader, columnName As String) As String
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), Nothing, reader.GetString(ordinal))
        End Function

        Public Shared Function GetNullableInt32(reader As SqlDataReader, columnName As String) As Integer?
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), CType(Nothing, Integer?), reader.GetInt32(ordinal))
        End Function

        Public Shared Function GetNullableDate(reader As SqlDataReader, columnName As String) As Date?
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), CType(Nothing, Date?), reader.GetDateTime(ordinal))
        End Function

        Public Shared Function GetNullableDecimal(reader As SqlDataReader, columnName As String) As Decimal?
            Dim ordinal As Integer = reader.GetOrdinal(columnName)
            Return If(reader.IsDBNull(ordinal), CType(Nothing, Decimal?), reader.GetDecimal(ordinal))
        End Function

        ''' <summary>
        ''' True when the reader exposes a column by this name. Lets one mapper serve both
        ''' a lean query and a joined one without duplicating the mapping code.
        ''' </summary>
        Public Shared Function HasColumn(reader As SqlDataReader, columnName As String) As Boolean
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
