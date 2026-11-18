Option Strict On
Option Explicit On

Imports System.Configuration
Imports System.Data.SqlClient
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TutorConnect.Data
Imports TutorConnect.Seed

Namespace TutorConnect.Tests

    ''' <summary>
    ''' Rebuilds the database once per test run and provides small SQL helpers.
    ''' </summary>
    ''' <remarks>
    ''' These are integration tests by intention. The rules under test - overlap guards,
    ''' one-review-per-booking, role integrity - are enforced partly by SQL Server, so
    ''' mocking the database would test the mock rather than the rule.
    '''
    ''' The schema is rebuilt from 01_CreateSchema.sql, which drops every table. Never
    ''' point the connection string at anything that matters.
    ''' </remarks>
    <TestClass>
    Public Class TestDatabase

        Private Shared _scriptFolder As String

        ''' <summary>
        ''' The password every seeded account is created with.
        ''' </summary>
        ''' <remarks>
        ''' Read from App.config, never written here. The same value drives SeedRunner, so
        ''' hard-coding a copy would silently break the day the setting changed - and a
        ''' password in source is exactly what SEC-02 forbids.
        ''' </remarks>
        Public Shared ReadOnly Property DevPassword As String
            Get
                Dim value As String = ConfigurationManager.AppSettings("Seed:DevPassword")

                If String.IsNullOrEmpty(value) Then
                    Assert.Inconclusive("Seed:DevPassword is not configured in App.config; " &
                                        "the tests cannot sign in without it.")
                End If

                Return value
            End Get
        End Property

        ''' <summary>
        ''' A short random suffix, so a test that inserts a user can run twice without
        ''' colliding with itself on the unique username and email constraints.
        ''' </summary>
        Public Shared Function UniqueSuffix() As String
            Return Guid.NewGuid().ToString("N").Substring(0, 8)
        End Function

        <AssemblyInitialize>
        Public Shared Sub Initialise(context As TestContext)
            _scriptFolder = LocateScripts()

            BuildSchema()

            Dim seeder As New SeedRunner()
            seeder.RunAsync(includeDemoData:=True).GetAwaiter().GetResult()
        End Sub

        ''' <summary>
        ''' Finds App_Data\Scripts by walking up from the test output folder.
        ''' </summary>
        ''' <remarks>
        ''' A relative path from bin\Debug\net481 would break the moment the output path
        ''' changed. Walking up until the folder appears survives that.
        ''' </remarks>
        Private Shared Function LocateScripts() As String
            Dim dir As New DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory)

            While dir IsNot Nothing
                Dim candidate As String = Path.Combine(dir.FullName, "App_Data", "Scripts")
                If Directory.Exists(candidate) Then Return candidate
                dir = dir.Parent
            End While

            Throw New DirectoryNotFoundException(
                "Could not locate App_Data\Scripts above " & AppDomain.CurrentDomain.BaseDirectory)
        End Function

        ''' <summary>
        ''' Runs a .sql file, splitting on GO the way sqlcmd does.
        ''' </summary>
        ''' <remarks>
        ''' GO is a batch separator understood by sqlcmd, not a T-SQL statement, so
        ''' SqlCommand rejects it. The script relies on it to separate CREATE TRIGGER
        ''' batches, hence the split.
        ''' </remarks>
        Private Shared Sub RunScript(path As String)
            Dim sql As String = File.ReadAllText(path)

            Dim batches As String() = Regex.Split(
                sql, "^\s*GO\s*$", RegexOptions.Multiline Or RegexOptions.IgnoreCase)

            ' The scripts switch database with USE, so the connection is opened against
            ' master and the batches steer themselves.
            Dim builder As New SqlConnectionStringBuilder(DatabaseHelper.ConnectionString) With {
                .InitialCatalog = "master"
            }

            Using connection As New SqlConnection(builder.ConnectionString)
                connection.Open()

                For Each batch In batches
                    If batch.Trim().Length = 0 Then Continue For

                    Using command As New SqlCommand(batch, connection) With {.CommandTimeout = 120}
                        command.ExecuteNonQuery()
                    End Using
                Next
            End Using
        End Sub

        ''' <summary>
        ''' Rebuilds the database from the scripts alone - tables and lookups, no people -
        ''' runs <paramref name="work"/> against it, then restores the seeded state.
        ''' </summary>
        ''' <remarks>
        ''' <para>
        ''' This exists for first-run setup, which by definition can only be exercised on an
        ''' installation that has no administrator. The seeded database has one.
        ''' </para>
        ''' <para>
        ''' The alternative - a connection string the tests could redirect at a scratch
        ''' database - would mean adding a settable static to <c>DatabaseHelper</c> purely
        ''' for testing. A mutable connection string on a shared static is not something to
        ''' put in shipping code so that a test can reach it. Rebuilding is slower and
        ''' changes nothing about the application.
        ''' </para>
        ''' <para>
        ''' The restore runs in Finally, so a failing assertion inside <paramref name="work"/>
        ''' still leaves the seeded accounts behind for every other test.
        ''' </para>
        ''' </remarks>
        Public Shared Async Function UsingEmptyInstallationAsync(work As Func(Of Task)) As Task
            If work Is Nothing Then Throw New ArgumentNullException(NameOf(work))

            BuildSchema()

            Try
                Await work().ConfigureAwait(False)
            Finally
                BuildSchema()
                Dim seeder As New SeedRunner()
                seeder.RunAsync(includeDemoData:=True).GetAwaiter().GetResult()
            End Try
        End Function

        ''' <summary>Drops and recreates every table, then loads the lookup rows. No people.</summary>
        Private Shared Sub BuildSchema()
            EvictOtherConnections()
            RunScript(Path.Combine(_scriptFolder, "01_CreateSchema.sql"))
            RunScript(Path.Combine(_scriptFolder, "02_SeedLookups.sql"))
        End Sub

        ''' <summary>
        ''' Closes every other connection to the test database before the schema is dropped.
        ''' </summary>
        ''' <remarks>
        ''' <c>DROP TABLE</c> waits on anything else holding a lock, so a browser session
        ''' left open against the same database - IIS Express during a manual check, or a
        ''' query window in Management Studio - makes the whole run hang and then fail on a
        ''' command timeout, with a message that says nothing about the real cause.
        '''
        ''' SINGLE_USER with ROLLBACK IMMEDIATE evicts them, and MULTI_USER lets the tests
        ''' back in. This is safe here only because the connection string must already point
        ''' at a disposable database: it would be an unfriendly thing to do to a shared one.
        ''' </remarks>
        Private Shared Sub EvictOtherConnections()
            Dim target As String = New SqlConnectionStringBuilder(DatabaseHelper.ConnectionString).InitialCatalog

            Dim builder As New SqlConnectionStringBuilder(DatabaseHelper.ConnectionString) With {
                .InitialCatalog = "master"
            }

            ' The pool holds connections open between tests; they would block the drop too.
            SqlConnection.ClearAllPools()

            Using connection As New SqlConnection(builder.ConnectionString)
                connection.Open()

                ' QUOTENAME rather than concatenation, because ALTER DATABASE will not take
                ' a parameter for its target.
                Const sql As String =
                    "IF DB_ID(@Name) IS NOT NULL " &
                    "BEGIN " &
                    "  DECLARE @statement NVARCHAR(400) = " &
                    "      N'ALTER DATABASE ' + QUOTENAME(@Name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE;' + " &
                    "      N'ALTER DATABASE ' + QUOTENAME(@Name) + N' SET MULTI_USER;'; " &
                    "  EXEC sp_executesql @statement; " &
                    "END"

                Using command As New SqlCommand(sql, connection) With {.CommandTimeout = 60}
                    command.Parameters.AddWithValue("@Name", target)
                    command.ExecuteNonQuery()
                End Using
            End Using
        End Sub

#Region "Query helpers"

        Public Shared Async Function ScalarAsync(sql As String, ParamArray args As Object()) As Task(Of Integer)
            Using connection = Await DatabaseHelper.CreateOpenConnectionAsync()
                Using command As New SqlCommand(sql, connection)
                    For i = 0 To args.Length - 1
                        command.Parameters.AddWithValue("@p" & i, args(i))
                    Next
                    Dim value = Await command.ExecuteScalarAsync()
                    If value Is Nothing OrElse Convert.IsDBNull(value) Then Return 0
                    Return Convert.ToInt32(value)
                End Using
            End Using
        End Function

        Public Shared Async Function ScalarStringAsync(sql As String, ParamArray args As Object()) As Task(Of String)
            Using connection = Await DatabaseHelper.CreateOpenConnectionAsync()
                Using command As New SqlCommand(sql, connection)
                    For i = 0 To args.Length - 1
                        command.Parameters.AddWithValue("@p" & i, args(i))
                    Next
                    Dim value = Await command.ExecuteScalarAsync()
                    Return If(value Is Nothing OrElse Convert.IsDBNull(value), String.Empty, Convert.ToString(value))
                End Using
            End Using
        End Function

        Public Shared Async Function ExecuteAsync(sql As String, ParamArray args As Object()) As Task
            Using connection = Await DatabaseHelper.CreateOpenConnectionAsync()
                Using command As New SqlCommand(sql, connection)
                    For i = 0 To args.Length - 1
                        command.Parameters.AddWithValue("@p" & i, args(i))
                    Next
                    Await command.ExecuteNonQueryAsync()
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Inserts availability directly, bypassing the service layer.
        ''' </summary>
        ''' <remarks>
        ''' Used only to build states the services deliberately refuse to create - a slot
        ''' in the past, for instance, which BR-07 forbids but BR-11 must still be tested
        ''' against.
        ''' </remarks>
        Public Shared Async Function CreateAvailabilityDirectAsync(tutorSubjectId As Integer,
                                                                    slotDate As Date,
                                                                    startTime As String,
                                                                    endTime As String) As Task(Of Integer)
            Using connection = Await DatabaseHelper.CreateOpenConnectionAsync()
                Using command As New SqlCommand(
                    "INSERT INTO dbo.TutorAvailability (TutorSubjectId, AvailabilityDate, StartTime, EndTime) " &
                    "VALUES (@ts, @d, @s, @e); SELECT CAST(SCOPE_IDENTITY() AS INT);", connection)

                    command.Parameters.AddWithValue("@ts", tutorSubjectId)
                    command.Parameters.AddWithValue("@d", slotDate)
                    command.Parameters.AddWithValue("@s", TimeSpan.Parse(startTime))
                    command.Parameters.AddWithValue("@e", TimeSpan.Parse(endTime))
                    Return Convert.ToInt32(Await command.ExecuteScalarAsync())
                End Using
            End Using
        End Function

#End Region

    End Class

End Namespace
