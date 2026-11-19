Option Strict On
Option Explicit On

Namespace Utilities

    ''' <summary>
    ''' South African wall-clock time, whatever time zone the web server runs in.
    ''' </summary>
    ''' <remarks>
    ''' Session slots are wall-clock times in SAST. Date.Now is only SAST when the server
    ''' is in South Africa; on a hosted server elsewhere "is this slot in the past?" would
    ''' be wrong by the server's offset. SAST has no daylight saving, so the fixed +02:00
    ''' fallback is exact if the time-zone database is unavailable.
    ''' </remarks>
    Public NotInheritable Class Clock

        Private Shared ReadOnly SouthAfrica As TimeZoneInfo = FindSouthAfrica()

        Private Sub New()
        End Sub

        Private Shared Function FindSouthAfrica() As TimeZoneInfo
            Try
                Return TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time")
            Catch ex As TimeZoneNotFoundException
                Return TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST")
            End Try
        End Function

        ''' <summary>The current date and time in South Africa.</summary>
        Public Shared ReadOnly Property Now As Date
            Get
                Return TimeZoneInfo.ConvertTimeFromUtc(Date.UtcNow, SouthAfrica)
            End Get
        End Property

        ''' <summary>Today's date in South Africa.</summary>
        Public Shared ReadOnly Property Today As Date
            Get
                Return Now.Date
            End Get
        End Property

        ''' <summary>Minutes between UTC and South African time (120).</summary>
        Public Shared ReadOnly Property UtcOffsetMinutes As Integer
            Get
                Return CInt(SouthAfrica.GetUtcOffset(Date.UtcNow).TotalMinutes)
            End Get
        End Property

    End Class

End Namespace
