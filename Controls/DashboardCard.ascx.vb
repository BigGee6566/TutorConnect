Option Strict On
Option Explicit On

Imports System.Web.UI
Imports System.Web.UI.HtmlControls
Imports System.Web.UI.WebControls

Namespace Controls

    ''' <summary>Icon tint options, matching the mockup.</summary>
    Public Enum CardAccent
        Blue
        Orange
        Green
        Gold
    End Enum

    ''' <summary>
    ''' One dashboard tile: icon, uppercase title, one-line description, whole card links.
    ''' </summary>
    Public Class DashboardCard
        Inherits UserControl

        Protected WithEvents cardLink As HtmlAnchor
        Protected WithEvents iconWrap As HtmlGenericControl
        Protected WithEvents litIcon As Literal
        Protected WithEvents litCount As Literal
        Protected WithEvents litTitle As Literal
        Protected WithEvents litDescription As Literal

        ''' <summary>Card heading, rendered uppercase by CSS.</summary>
        Public Property Title As String = String.Empty

        Public Property Description As String = String.Empty

        ''' <summary>Application-relative target, e.g. "~/Student/Profile/MyProfile.aspx".</summary>
        Public Property NavigateUrl As String = String.Empty

        ''' <summary>Inline SVG for the 40px icon.</summary>
        Public Property Icon As String = String.Empty

        Public Property Accent As CardAccent = CardAccent.Blue

        ''' <summary>Optional badge, e.g. pending requests. Zero or less hides it.</summary>
        Public Property Count As Integer

        ''' <summary>
        ''' Binds at PreRenderComplete so a Count supplied by an async task - which runs
        ''' after PreRender - is reflected rather than missed.
        ''' </summary>
        Protected Overrides Sub OnInit(e As EventArgs)
            MyBase.OnInit(e)
            AddHandler Page.PreRenderComplete, AddressOf BindCard
        End Sub

        Private Sub BindCard(sender As Object, e As EventArgs)
            litTitle.Text = Title
            litDescription.Text = Description
            litIcon.Text = Icon
            cardLink.HRef = ResolveUrl(NavigateUrl)

            iconWrap.Attributes("class") = "dash-card__icon " & AccentClass()

            If Count > 0 Then
                ' The count is also spelled out for screen readers, which cannot infer
                ' meaning from a floating number.
                litCount.Text =
                    $"<span class=""dash-card__count"" aria-hidden=""true"">{Count}</span>" &
                    $"<span class=""sr-only"">{Count} pending</span>"
            Else
                litCount.Text = String.Empty
            End If
        End Sub

        Private Function AccentClass() As String
            Select Case Accent
                Case CardAccent.Orange : Return "dash-card__icon--orange"
                Case CardAccent.Green : Return "dash-card__icon--green"
                Case CardAccent.Gold : Return "dash-card__icon--gold"
                Case Else : Return "dash-card__icon--blue"
            End Select
        End Function

    End Class

End Namespace
