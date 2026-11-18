Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>
    ''' Submits a request against one of the tutor's published slots.
    ''' </summary>
    ''' <remarks>
    ''' The slot list is rebuilt from the database on every postback rather than trusted
    ''' from ViewState, and the chosen AvailabilityId is checked against that freshly
    ''' loaded list before it reaches the service. Even so, RequestService re-runs every
    ''' rule server-side (BR-08 to BR-12) - this screen makes the common case pleasant, it
    ''' is not what makes it safe.
    ''' </remarks>
    Public Class RequestSession
        Inherits StudentPageBase

        Private ReadOnly _search As ITutorSearchService = New TutorSearchService()
        Private ReadOnly _requests As IRequestService = New RequestService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents pnlForm As Panel
        Protected WithEvents litTutor As Literal
        Protected WithEvents litSubject As Literal
        Protected WithEvents litRate As Literal
        Protected WithEvents rblSlots As RadioButtonList
        Protected WithEvents txtMessage As TextBox
        Protected WithEvents btnSubmit As Button
        Protected WithEvents lnkCancel As HyperLink

        Private _slots As IList(Of TutorAvailability)

        Private ReadOnly Property TutorSubjectId As Integer
            Get
                Dim parsed As Integer
                Return If(Integer.TryParse(Request.QueryString("tutorSubjectId"), parsed), parsed, 0)
            End Get
        End Property

        Private ReadOnly Property PreselectedAvailabilityId As Integer
            Get
                Dim parsed As Integer
                Return If(Integer.TryParse(Request.QueryString("availabilityId"), parsed), parsed, 0)
            End Get
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            lnkCancel.NavigateUrl = $"~/Student/Tutors/TutorDetails.aspx?tutorSubjectId={TutorSubjectId}"
            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

        Private Async Function LoadAsync() As Task
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Request a Session"
                shell.PageSubtitle = "Choose a time that suits you"
            End If

            Dim detail As ServiceResult(Of TutorSearchResult) =
                Await _search.GetDetailAsync(TutorSubjectId).ConfigureAwait(True)

            If Not detail.Success Then
                pnlForm.Visible = False
                alert.Show(AlertKind.Error, detail.Message)
                Return
            End If

            litTutor.Text = detail.Data.FullName
            litSubject.Text = $"{detail.Data.SubjectName} - {detail.Data.StudyLevelName}"
            litRate.Text = "R" & detail.Data.RatePerSession.ToString("0.##", CultureInfo.GetCultureInfo("en-ZA"))

            Await BindSlotsAsync()
        End Function

        Private Async Function BindSlotsAsync() As Task
            Dim slots As ServiceResult(Of IList(Of TutorAvailability)) =
                Await _search.GetOpenSlotsAsync(TutorSubjectId).ConfigureAwait(True)

            _slots = slots.Data

            Dim selected As String = rblSlots.SelectedValue

            rblSlots.Items.Clear()
            For Each slot In _slots
                rblSlots.Items.Add(New ListItem(
                    $"{slot.AvailabilityDate:dd/MM/yyyy}   {slot.StartTime:hh\:mm} - {slot.EndTime:hh\:mm}",
                    slot.AvailabilityId.ToString(CultureInfo.InvariantCulture)))
            Next

            If _slots.Count = 0 Then
                btnSubmit.Enabled = False
                alert.Show(AlertKind.Info,
                           "This tutor has no future slots available at the moment.")
                Return
            End If

            ' Preserve the student's choice across a postback, otherwise fall back to the
            ' slot they clicked on the previous screen.
            Dim wanted As String = If(String.IsNullOrEmpty(selected),
                                      PreselectedAvailabilityId.ToString(CultureInfo.InvariantCulture),
                                      selected)

            Dim match As ListItem = rblSlots.Items.FindByValue(wanted)
            If match IsNot Nothing Then match.Selected = True
        End Function

        Protected Sub btnSubmit_Click(sender As Object, e As EventArgs) Handles btnSubmit.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf SubmitAsync))
        End Sub

        Private Async Function SubmitAsync() As Task
            Await BindSlotsAsync()

            Dim chosenId As Integer
            If Not Integer.TryParse(rblSlots.SelectedValue, chosenId) OrElse chosenId <= 0 Then
                alert.Show(AlertKind.Error, "Choose one of the available time slots.")
                Return
            End If

            ' The date and times come from the slot as the database has it, never from the
            ' page. A tampered form cannot request 09:00-17:00 against a 10:00-12:00 slot.
            Dim slot As TutorAvailability = _slots.FirstOrDefault(Function(s) s.AvailabilityId = chosenId)
            If slot Is Nothing Then
                alert.Show(AlertKind.Error, "That slot is no longer available. Choose another.")
                Return
            End If

            Dim studentId As Integer = Await ResolveStudentIdAsync()

            Dim result As ServiceResult(Of Integer) = Await _requests.CreateAsync(
                studentId,
                TutorSubjectId,
                slot.AvailabilityId,
                slot.AvailabilityDate,
                slot.StartTime,
                slot.EndTime,
                txtMessage.Text.Trim()).ConfigureAwait(True)

            If Not result.Success Then
                alert.Show(AlertKind.Error, result.Message)
                Await BindSlotsAsync()
                Return
            End If

            Response.Redirect("~/Student/Requests/MyRequests.aspx?sent=1", endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

    End Class

End Namespace
