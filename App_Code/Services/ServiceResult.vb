Option Strict On
Option Explicit On

Namespace Services

    ''' <summary>
    ''' The return type of every public service method.
    ''' </summary>
    ''' <remarks>
    ''' Part K rule 7: an expected business failure - "that slot is already booked",
    ''' "you have already reviewed this booking" - is a result, not an exception.
    ''' Exceptions are reserved for genuine faults (a dropped connection, a bug).
    ''' A page can therefore branch on Success and show Message without a Try/Catch.
    ''' </remarks>
    Public Class ServiceResult

        Protected Sub New(succeeded As Boolean, message As String)
            Me.Success = succeeded
            Me.Message = If(message, String.Empty)
        End Sub

        ''' <summary>True when the operation completed and any rules it enforces passed.</summary>
        Public ReadOnly Property Success As Boolean

        ''' <summary>
        ''' A message safe to show the user. On failure this explains what to do
        ''' differently; it never leaks SQL, stack traces or another user's data.
        ''' </summary>
        Public ReadOnly Property Message As String

        Public Shared Function Ok(Optional message As String = "") As ServiceResult
            Return New ServiceResult(True, message)
        End Function

        Public Shared Function Fail(message As String) As ServiceResult
            Return New ServiceResult(False, message)
        End Function

    End Class

    ''' <summary>
    ''' A <see cref="ServiceResult"/> that also carries data on success.
    ''' </summary>
    Public NotInheritable Class ServiceResult(Of T)
        Inherits ServiceResult

        Private Sub New(succeeded As Boolean, message As String, data As T)
            MyBase.New(succeeded, message)
            Me.Data = data
        End Sub

        ''' <summary>
        ''' The payload. Only meaningful when <see cref="ServiceResult.Success"/> is True;
        ''' on failure it is the default for <typeparamref name="T"/>.
        ''' </summary>
        Public ReadOnly Property Data As T

        Public Overloads Shared Function Ok(data As T, Optional message As String = "") As ServiceResult(Of T)
            Return New ServiceResult(Of T)(True, message, data)
        End Function

        Public Overloads Shared Function Fail(message As String) As ServiceResult(Of T)
            Return New ServiceResult(Of T)(False, message, Nothing)
        End Function

    End Class

End Namespace
