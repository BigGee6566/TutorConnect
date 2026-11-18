Option Strict On
Option Explicit On

Imports System.Collections.Concurrent
Imports System.Web

Namespace Security

    ''' <summary>
    ''' A small in-memory rate limit for anonymous actions.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' SEC-08 covers sign-in with a per-account lockout, but registration has no account
    ''' to lock. Without something here the form can be scripted to create accounts in
    ''' bulk, or to probe which usernames are taken.
    ''' </para>
    ''' <para>
    ''' Deliberately modest, and worth being honest about what it is not: the counter lives
    ''' in this process, so it resets on an app-pool recycle and does not span a web farm,
    ''' and the key is the client address, which a determined attacker can vary. It raises
    ''' the cost of casual abuse; it is not a defence against a distributed one. A CAPTCHA
    ''' or a shared cache would be the next step if this were public-facing.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class RateLimiter

        Private Shared ReadOnly Attempts As New ConcurrentDictionary(Of String, Window)()

        ''' <summary>Attempts allowed inside <see cref="WindowMinutes"/>.</summary>
        Public Const RegistrationLimit As Integer = 5

        Public Const WindowMinutes As Integer = 15

        Private Sub New()
        End Sub

        Private NotInheritable Class Window
            Public Property StartedUtc As Date
            Public Property Count As Integer
        End Class

        ''' <summary>
        ''' Records an attempt and reports whether the caller is over the limit.
        ''' </summary>
        ''' <returns>True when the action should be refused.</returns>
        Public Shared Function IsLimited(action As String,
                                         context As HttpContext,
                                         Optional limit As Integer = RegistrationLimit) As Boolean
            If context Is Nothing Then Return False

            Dim key As String = action & "|" & ClientAddress(context)
            Dim now As Date = Date.UtcNow

            Dim window As Window = Attempts.AddOrUpdate(
                key,
                Function(k) New Window With {.StartedUtc = now, .Count = 1},
                Function(k, existing)
                    ' A fixed window, not a sliding one: simple, and the reset is
                    ' predictable enough to explain to a user.
                    If (now - existing.StartedUtc).TotalMinutes >= WindowMinutes Then
                        existing.StartedUtc = now
                        existing.Count = 1
                    Else
                        existing.Count += 1
                    End If
                    Return existing
                End Function)

            ' Keep the dictionary from growing without bound on a long-running process.
            If Attempts.Count > 5000 Then PruneExpired(now)

            Return window.Count > limit
        End Function

        Private Shared Sub PruneExpired(now As Date)
            For Each pair In Attempts
                If (now - pair.Value.StartedUtc).TotalMinutes >= WindowMinutes Then
                    Dim removed As Window = Nothing
                    Attempts.TryRemove(pair.Key, removed)
                End If
            Next
        End Sub

        ''' <summary>
        ''' The client address, preferring the proxy header only when one is configured.
        ''' </summary>
        ''' <remarks>
        ''' X-Forwarded-For is trusted only behind a reverse proxy that sets it; taking it
        ''' unconditionally would let anyone reset their own counter by sending the header.
        ''' There is no proxy in this deployment, so UserHostAddress is used.
        ''' </remarks>
        Private Shared Function ClientAddress(context As HttpContext) As String
            Return If(context.Request.UserHostAddress, "unknown")
        End Function

    End Class

End Namespace
