Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Web.UI
Imports System.Web.UI.WebControls

Namespace Controls

    ''' <summary>
    ''' The bar carrying the sidebar drawer toggle and the notification bell.
    ''' </summary>
    ''' <remarks>
    ''' The unread count is supplied by the master page, which already runs an async task,
    ''' rather than fetched here - a user control cannot register its own async work, and
    ''' blocking on a database call inside a control would stall the render.
    ''' </remarks>
    Public Class TopHeader
        Inherits UserControl

        Protected WithEvents litTitle As Literal
        Protected WithEvents litCount As Literal
        Protected WithEvents lnkBell As HyperLink

        ''' <summary>Short label shown beside the hamburger.</summary>
        Public Property Title As String = "TutorConnect"

        ''' <summary>Unread notifications. Zero hides the badge but keeps the bell.</summary>
        Public Property UnreadCount As Integer

        ''' <summary>
        ''' Binds at PreRenderComplete because the count arrives from the master page's
        ''' async task, which runs after this control's own PreRender would have fired.
        ''' </summary>
        Protected Overrides Sub OnInit(e As EventArgs)
            MyBase.OnInit(e)
            AddHandler Page.PreRenderComplete, AddressOf BindHeader
        End Sub

        Private Sub BindHeader(sender As Object, e As EventArgs)
            litTitle.Text = Title

            If UnreadCount > 0 Then
                litCount.Text =
                    $"<span class=""bell__count"" aria-hidden=""true"">{UnreadCount}</span>"
                lnkBell.ToolTip = $"Notifications ({UnreadCount} unread)"
                lnkBell.Attributes("aria-label") =
                    $"Notifications, {UnreadCount} unread"
            Else
                litCount.Text = String.Empty
                lnkBell.ToolTip = "Notifications"
                lnkBell.Attributes("aria-label") = "Notifications, none unread"
            End If
        End Sub

    End Class

End Namespace
