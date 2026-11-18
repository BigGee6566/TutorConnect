Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>The student's own requests, with withdraw on pending rows (BR-13).</summary>
    Public Class MyRequests
        Inherits StudentPageBase

        Private ReadOnly _requests As IRequestService = New RequestService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents rptRequests As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents btnRefresh As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "My Requests"
                shell.PageSubtitle = "Track the status of your tutor requests"
            End If

            If Not IsPostBack AndAlso Request.QueryString("sent") = "1" Then
                alert.Show(AlertKind.Success, "Your request has been sent to the tutor.")
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim studentId As Integer = Await ResolveStudentIdAsync()
            If studentId = 0 Then Return

            Dim result As ServiceResult(Of IList(Of SessionRequest)) =
                Await _requests.GetForStudentAsync(studentId).ConfigureAwait(True)

            rptRequests.DataSource = result.Data
            rptRequests.DataBind()

            rptRequests.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
        End Function

        Protected Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
            ' Binding already happens on every load; this just makes the refresh explicit.
            alert.Hide()
        End Sub

        Protected Sub Cancel_Command(sender As Object, e As CommandEventArgs)
            Dim requestId As Integer
            Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), requestId)

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim studentId As Integer = Await ResolveStudentIdAsync()

                    Dim result As ServiceResult =
                        Await _requests.CancelAsync(studentId, requestId).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Protected Shared Function GetDate(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            ' CreatedAt is stored UTC; shown in local time, which is what the student expects.
            Return HttpUtility.HtmlEncode(
                d.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

    End Class

End Namespace
