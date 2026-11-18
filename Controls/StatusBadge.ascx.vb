Option Strict On
Option Explicit On

Imports System.Web.UI
Imports System.Web.UI.WebControls

Namespace Controls

    ''' <summary>
    ''' The status pill used by every table (§F.4).
    ''' </summary>
    ''' <remarks>
    ''' The status-to-colour mapping lives here and nowhere else. No screen writes badge
    ''' markup by hand, so Pending can never be orange on one page and grey on another.
    '''
    ''' An unrecognised value renders with a dashed outline rather than silently looking
    ''' like a normal badge - a mismatch between the enums and the database should be
    ''' visible, not disguised.
    ''' </remarks>
    Public Class StatusBadge
        Inherits UserControl

        Protected WithEvents lblBadge As Label

        ''' <summary>
        ''' The status text, e.g. "Pending". Set from a RequestStatus or BookingStatus
        ''' enum via ToString - never a literal typed into a page.
        ''' </summary>
        Public Property Status As String = String.Empty

        Protected Sub Page_PreRender(sender As Object, e As EventArgs) Handles Me.PreRender
            Dim value As String = If(Status, String.Empty).Trim()

            lblBadge.Text = value
            lblBadge.CssClass = "badge " & ModifierFor(value)
            lblBadge.Visible = value.Length > 0
        End Sub

        Private Shared Function ModifierFor(status As String) As String
            Select Case status.ToUpperInvariant()
                Case "PENDING" : Return "badge--pending"
                Case "CONFIRMED" : Return "badge--confirmed"
                Case "COMPLETED" : Return "badge--completed"
                Case "DECLINED" : Return "badge--declined"
                Case "SCHEDULED" : Return "badge--scheduled"
                Case "CANCELLED" : Return "badge--cancelled"
                Case Else : Return "badge--unknown"
            End Select
        End Function

    End Class

End Namespace
