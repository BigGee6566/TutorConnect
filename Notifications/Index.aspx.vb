Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Enums
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace NotificationArea

    ''' <summary>
    ''' One notification list serving all three roles.
    ''' </summary>
    ''' <remarks>
    ''' The master page is chosen in PreInit from the signed-in role, which is the last
    ''' moment ASP.NET allows it to be set. Three near-identical copies of this page - one
    ''' per role folder - would be the alternative, and they would drift.
    '''
    ''' Every read and write is scoped to the signed-in PersonId, so the shared URL does
    ''' not become a way to read someone else's notifications (BR-24).
    ''' </remarks>
    Public Class Index
        Inherits BasePage

        Private ReadOnly _notifications As INotificationService = New NotificationService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents rptNotifications As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents btnMarkAll As Button

        Protected Sub Page_PreInit(sender As Object, e As EventArgs) Handles Me.PreInit
            Select Case CurrentRole
                Case UserRole.Tutor
                    MasterPageFile = "~/MasterPages/Tutor.Master"
                Case UserRole.Administrator
                    MasterPageFile = "~/MasterPages/Admin.Master"
                Case Else
                    MasterPageFile = "~/MasterPages/Student.Master"
            End Select
        End Sub

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As AppMasterBase = TryCast(Master, AppMasterBase)
            If shell IsNot Nothing Then
                shell.PageTitle = "Notifications"
                shell.PageSubtitle = "Updates about your requests and sessions"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim result As ServiceResult(Of IList(Of Notification)) =
                Await _notifications.GetForPersonAsync(CurrentPersonId, unreadOnly:=False).ConfigureAwait(True)

            rptNotifications.DataSource = result.Data
            rptNotifications.DataBind()

            rptNotifications.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
            btnMarkAll.Visible = result.Data.Any(Function(n) Not n.IsRead)
        End Function

        Protected Sub btnMarkAll_Click(sender As Object, e As EventArgs) Handles btnMarkAll.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Await _notifications.MarkAllReadAsync(CurrentPersonId).ConfigureAwait(True)
                    alert.Show(AlertKind.Success, "All notifications marked read.")
                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub MarkRead_Command(sender As Object, e As CommandEventArgs)
            Dim notificationId As Integer
            Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), notificationId)

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    ' Scoped by PersonId inside the UPDATE, so another user's id marks nothing.
                    Dim result As ServiceResult =
                        Await _notifications.MarkReadAsync(CurrentPersonId, notificationId).ConfigureAwait(True)

                    If Not result.Success Then alert.Show(AlertKind.Error, result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Protected Shared Function GetWhen(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(
                d.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

        Protected Shared Function GetReadState(value As Object) As String
            Return If(Convert.ToBoolean(value, CultureInfo.InvariantCulture), "Read", "<strong>Unread</strong>")
        End Function

    End Class

End Namespace
