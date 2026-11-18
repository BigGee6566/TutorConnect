Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Web

Namespace Utilities

    ''' <summary>
    ''' Writes unhandled faults to a file and returns the reference shown to the user
    ''' (SEC-11).
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' Logs live under App_Data, which ASP.NET refuses to serve, so the file cannot be
    ''' fetched over HTTP even if its name is guessed.
    ''' </para>
    ''' <para>
    ''' The reference the user is given is generated here and written into the entry, so
    ''' quoting it actually finds the fault. The page must not mint its own.
    ''' </para>
    ''' <para>
    ''' What is deliberately never written: form values, cookies, ViewState, and any
    ''' header that could carry a credential. A log that captures a login POST has turned
    ''' a diagnostic aid into a password file.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class ErrorLogger

        ''' <summary>Key under which the reference is passed to the error page.</summary>
        Public Const ReferenceKey As String = "TutorConnect:ErrorReference"

        Private Const LogFolder As String = "~/App_Data/Logs"

        Private Shared ReadOnly WriteLock As New Object()

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Records an exception and returns its reference.
        ''' </summary>
        ''' <returns>
        ''' A short uppercase reference, also written into the log entry. Returns the
        ''' reference even when the file cannot be written - failing to log must not turn
        ''' one fault into two.
        ''' </returns>
        Public Shared Function Log(fault As Exception, context As HttpContext) As String
            Dim reference As String = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()

            Try
                Dim entry As New StringBuilder()
                entry.AppendLine("---------------------------------------------------------------")
                entry.AppendLine($"Reference : {reference}")
                entry.AppendLine($"When (UTC): {Date.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}")

                If context IsNot Nothing Then
                    ' Path and method only. No query string: it can carry identifiers a
                    ' log should not retain, and it is not needed to locate a fault.
                    entry.AppendLine($"Request   : {context.Request.HttpMethod} {context.Request.Path}")

                    Dim identity As Security.TutorConnectIdentity =
                        TryCast(context.User?.Identity, Security.TutorConnectIdentity)

                    entry.AppendLine($"User      : {If(identity Is Nothing, "(anonymous)", $"PersonId {identity.PersonId} ({identity.Role})")}")
                End If

                Dim current As Exception = fault
                Dim depth As Integer = 0

                While current IsNot Nothing AndAlso depth < 5
                    entry.AppendLine($"Exception : {current.GetType().FullName}")
                    entry.AppendLine($"Message   : {current.Message}")
                    entry.AppendLine("Stack     :")
                    entry.AppendLine(current.StackTrace)
                    current = current.InnerException
                    depth += 1
                    If current IsNot Nothing Then entry.AppendLine("-- inner --")
                End While

                entry.AppendLine()
                WriteEntry(entry.ToString(), context)

            Catch ex As Exception
                ' Logging is best-effort. Whatever went wrong here, the caller still needs
                ' a reference and the user still needs a page.
            End Try

            Return reference
        End Function

        Private Shared Sub WriteEntry(text As String, context As HttpContext)
            Dim folder As String

            If context IsNot Nothing Then
                folder = context.Server.MapPath(LogFolder)
            ElseIf AppDomain.CurrentDomain.GetData("DataDirectory") IsNot Nothing Then
                folder = Path.Combine(Convert.ToString(
                    AppDomain.CurrentDomain.GetData("DataDirectory"), CultureInfo.InvariantCulture), "Logs")
            Else
                Return
            End If

            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)

            ' One file per day, and a lock because several requests can fault at once.
            ' Named logPath, not path: VB is case-insensitive, so a local called "path"
            ' shadows System.IO.Path itself.
            Dim logPath As String = Path.Combine(
                folder, $"errors-{Date.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log")

            SyncLock WriteLock
                File.AppendAllText(logPath, text, Encoding.UTF8)
                PruneOldLogs(folder)
            End SyncLock
        End Sub

        ''' <summary>Days of history kept.</summary>
        Public Const RetentionDays As Integer = 30

        ''' <summary>
        ''' Deletes log files older than <see cref="RetentionDays"/>.
        ''' </summary>
        ''' <remarks>
        ''' One file per day never pruned is an unbounded write to the application
        ''' directory. It is not exploitable - App_Data is never served - but a disk that
        ''' fills eventually takes the site down with it, which is a poor way for a
        ''' diagnostic aid to end.
        '''
        ''' Runs inside the existing write lock, so two faulting requests cannot prune at
        ''' once. Failures are swallowed: losing a cleanup is not worth failing a request
        ''' that is already handling an error.
        ''' </remarks>
        Private Shared Sub PruneOldLogs(folder As String)
            Try
                Dim cutoff As Date = Date.UtcNow.AddDays(-RetentionDays)

                ' logFile, not file: VB is case-insensitive, so a loop variable named
                ' "file" shadows System.IO.File for the rest of the block.
                For Each logFile In Directory.GetFiles(folder, "errors-*.log")
                    If File.GetLastWriteTimeUtc(logFile) < cutoff Then File.Delete(logFile)
                Next

            Catch ex As IOException
                ' A file in use, or a permission the app pool lacks. Not worth escalating.
            Catch ex As UnauthorizedAccessException
            End Try
        End Sub

    End Class

End Namespace

