Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace AdminArea

    ''' <summary>
    ''' Moderation: hide a review with a reason, or restore it.
    ''' </summary>
    ''' <remarks>
    ''' Nothing is deleted. A hidden review keeps its row, its author and its timestamp,
    ''' and gains the reason and the administrator who hid it - which is what makes this an
    ''' audit trail rather than a disappearance.
    ''' </remarks>
    Public Class ManageReviews
        Inherits BasePage

        Private ReadOnly _admin As IAdminService = New AdminService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents rptReviews As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents pnlHide As Panel
        Protected WithEvents litHideId As Literal
        Protected WithEvents txtReason As TextBox
        Protected WithEvents btnConfirmHide As Button
        Protected WithEvents btnCancelHide As Button

        ''' <summary>The review being hidden, held across the two stages.</summary>
        Private Property HidingReviewId As Integer
            Get
                Dim value As Object = ViewState("HidingReviewId")
                Return If(value Is Nothing, 0, CInt(value))
            End Get
            Set(value As Integer)
                ViewState("HidingReviewId") = value
            End Set
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As AdminShell = TryCast(Master, AdminShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Reviews"
                shell.PageSubtitle = "Moderate what students have written"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim result As ServiceResult(Of IList(Of Review)) =
                Await _admin.GetAllReviewsAsync().ConfigureAwait(True)

            rptReviews.DataSource = result.Data
            rptReviews.DataBind()

            rptReviews.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
        End Function

#Region "Row helpers"

        Protected Shared Function GetStars(value As Object) As String
            Dim rating As Integer = Convert.ToInt32(value, CultureInfo.InvariantCulture)

            Dim sb As New StringBuilder()
            sb.Append("<span class=""stars"" aria-hidden=""true"">")
            For i As Integer = 1 To 5
                sb.Append(If(i <= rating, "<span class=""stars__on"">&#9733;</span>",
                                          "<span class=""stars__off"">&#9734;</span>"))
            Next
            ' The glyphs are decorative. The visible number is what a sighted reader with
            ' low vision relies on, since gold on white is only 1.58:1; the sr-only phrase
            ' is what a screen reader announces.
            sb.Append($"</span><span class=""stars__value"" aria-hidden=""true"">{rating}/5</span>")
            sb.Append($"<span class=""sr-only"">{rating} out of 5</span>")
            Return sb.ToString()
        End Function

        ''' <summary>Shows the reason alongside the status, so moderation is inspectable.</summary>
        Protected Shared Function GetStatus(item As Object) As String
            Dim review As Review = TryCast(item, Review)
            If review Is Nothing Then Return String.Empty

            If Not review.IsHidden Then
                Return "<span class=""badge badge--confirmed"">Visible</span>"
            End If

            Dim reason As String = HttpUtility.HtmlEncode(
                If(String.IsNullOrWhiteSpace(review.HiddenReason), "(no reason recorded)", review.HiddenReason))

            Return $"<span class=""badge badge--cancelled"">Hidden</span><br /><span class=""page-subtitle"">{reason}</span>"
        End Function

#End Region

#Region "Actions"

        Protected Sub Hide_Command(sender As Object, e As CommandEventArgs)
            HidingReviewId = ParseId(e.CommandArgument)

            litHideId.Text = $"#{HidingReviewId}"
            txtReason.Text = String.Empty
            pnlHide.Visible = True
            alert.Hide()
        End Sub

        Protected Sub btnCancelHide_Click(sender As Object, e As EventArgs) Handles btnCancelHide.Click
            HidingReviewId = 0
            pnlHide.Visible = False
        End Sub

        Protected Sub btnConfirmHide_Click(sender As Object, e As EventArgs) Handles btnConfirmHide.Click
            Dim reviewId As Integer = HidingReviewId
            Dim reason As String = txtReason.Text.Trim()

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult = Await _admin.SetReviewHiddenAsync(
                        CurrentPersonId, reviewId, True, reason).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

                    If result.Success Then
                        HidingReviewId = 0
                        pnlHide.Visible = False
                    End If

                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub Unhide_Command(sender As Object, e As CommandEventArgs)
            Dim reviewId As Integer = ParseId(e.CommandArgument)

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult = Await _admin.SetReviewHiddenAsync(
                        CurrentPersonId, reviewId, False, Nothing).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Private Shared Function ParseId(value As Object) As Integer
            Dim parsed As Integer
            Integer.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), parsed)
            Return parsed
        End Function

#End Region

    End Class

End Namespace
