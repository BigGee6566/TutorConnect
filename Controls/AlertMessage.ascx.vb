Option Strict On
Option Explicit On

Imports System.Web.UI
Imports System.Web.UI.WebControls

Namespace Controls

    ''' <summary>The kind of message being shown, which drives colour and announcement.</summary>
    Public Enum AlertKind
        Success
        [Error]
        Info
    End Enum

    ''' <summary>
    ''' A single, consistently announced message region.
    ''' </summary>
    ''' <remarks>
    ''' Every screen uses this rather than hand-rolled markup, so the ARIA role and the
    ''' colour pairing stay identical everywhere. The message is rendered through a
    ''' Literal in Encode mode, so a value that reached us from a query string or a
    ''' database field cannot inject markup (SEC-09).
    ''' </remarks>
    Public Class AlertMessage
        Inherits UserControl

        Protected WithEvents pnlAlert As Panel
        Protected WithEvents litMessage As Literal

        Private _kind As AlertKind = AlertKind.Info

        ''' <summary>Colour and announcement style. Defaults to Info.</summary>
        Public Property Kind As AlertKind
            Get
                Return _kind
            End Get
            Set(value As AlertKind)
                _kind = value
            End Set
        End Property

        ''' <summary>
        ''' The text to display. Setting a non-empty value shows the control; setting an
        ''' empty one hides it, so callers never have to touch Visible.
        ''' </summary>
        Public Property Message As String
            Get
                Return litMessage.Text
            End Get
            Set(value As String)
                litMessage.Text = If(value, String.Empty)
                pnlAlert.Visible = Not String.IsNullOrWhiteSpace(value)
            End Set
        End Property

        Public Sub Show(kind As AlertKind, message As String)
            Me.Kind = kind
            Me.Message = message
        End Sub

        Public Sub Hide()
            Message = String.Empty
        End Sub

        Protected Sub Page_PreRender(sender As Object, e As EventArgs) Handles Me.PreRender
            If Not pnlAlert.Visible Then Return

            Select Case _kind
                Case AlertKind.Success
                    pnlAlert.CssClass = "alert alert--success"
                Case AlertKind.Error
                    pnlAlert.CssClass = "alert alert--error"
                Case Else
                    pnlAlert.CssClass = "alert alert--info"
            End Select

            ' An error interrupts the screen reader; success and info wait their turn.
            pnlAlert.Attributes("role") = If(_kind = AlertKind.Error, "alert", "status")
        End Sub

    End Class

End Namespace
