Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Data
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace TutorArea

    ''' <summary>Subjects the tutor teaches (BR-21).</summary>
    Public Class MySubjects
        Inherits TutorPageBase

        Private ReadOnly _tutors As ITutorService = New TutorService()
        Private ReadOnly _catalogue As New SubjectRepository()
        Private ReadOnly _tutorRepo As New TutorRepository()

        ''' <summary>Slot counts for the current rows, so the grid needs one query, not one per row.</summary>
        Private _slotCounts As Dictionary(Of Integer, Integer)

        Protected WithEvents alert As AlertMessage
        Protected WithEvents ddlSubject As DropDownList
        Protected WithEvents ddlStudyLevel As DropDownList
        Protected WithEvents btnAdd As Button
        Protected WithEvents txtNewSubject As TextBox
        Protected WithEvents btnAddNew As Button
        Protected WithEvents rptSubjects As Repeater
        Protected WithEvents pnlEmpty As Panel

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As TutorShell = TryCast(Master, TutorShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "My Subjects"
                shell.PageSubtitle = "Manage the subjects you teach"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

        Private Async Function LoadAsync() As Task
            If Not IsPostBack Then
                Await BindSubjectsAsync()
                ddlStudyLevel.DataSource = Await _catalogue.GetStudyLevelsAsync().ConfigureAwait(True)
                ddlStudyLevel.DataBind()
            End If

            Await BindAsync()
        End Function

        ''' <summary>
        ''' Rebinds the subject dropdown, preserving the selection where it still exists.
        ''' </summary>
        ''' <remarks>
        ''' Called again after a subject is created, not only on first load. Without this a
        ''' tutor who has just added "Accounting" would not find it in the dropdown on the
        ''' very page that created it.
        ''' </remarks>
        Private Async Function BindSubjectsAsync() As Task
            Dim previous As String = ddlSubject.SelectedValue

            ddlSubject.DataSource = Await _catalogue.GetSubjectsAsync().ConfigureAwait(True)
            ddlSubject.DataBind()

            If previous.Length > 0 AndAlso ddlSubject.Items.FindByValue(previous) IsNot Nothing Then
                ddlSubject.SelectedValue = previous
            End If
        End Function

        Private Async Function BindAsync() As Task
            Dim tutorId As Integer = Await ResolveTutorIdAsync()
            If tutorId = 0 Then Return

            Dim result As ServiceResult(Of IList(Of TutorSubject)) =
                Await _tutors.GetSubjectsAsync(tutorId).ConfigureAwait(True)

            ' One availability read, grouped in memory, rather than a query per row.
            Dim slots As IList(Of TutorAvailability) =
                Await _tutorRepo.GetAvailabilityForTutorAsync(tutorId).ConfigureAwait(True)

            _slotCounts = slots.GroupBy(Function(s) s.TutorSubjectId).
                                ToDictionary(Function(g) g.Key, Function(g) g.Count())

            rptSubjects.DataSource = result.Data
            rptSubjects.DataBind()

            rptSubjects.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
        End Function

        Protected Function GetSlotCount(tutorSubjectId As Object) As String
            Dim id As Integer = Convert.ToInt32(tutorSubjectId, CultureInfo.InvariantCulture)
            Dim count As Integer
            If _slotCounts Is Nothing OrElse Not _slotCounts.TryGetValue(id, count) Then count = 0
            Return count.ToString(CultureInfo.InvariantCulture)
        End Function

        Protected Sub btnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim tutorId As Integer = Await ResolveTutorIdAsync()

                    Dim result As ServiceResult = Await _tutors.AddSubjectAsync(
                        tutorId, ParseId(ddlSubject.SelectedValue),
                        ParseId(ddlStudyLevel.SelectedValue)).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        ''' <summary>
        ''' Adds a subject the catalogue does not have yet. The service reuses an existing
        ''' subject when the name matches one, so this cannot quietly create a second
        ''' "Mathematics" that no student would ever search for.
        ''' </summary>
        Protected Sub btnAddNew_Click(sender As Object, e As EventArgs) Handles btnAddNew.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim tutorId As Integer = Await ResolveTutorIdAsync()

                    Dim result As ServiceResult = Await _tutors.AddSubjectByNameAsync(
                        tutorId, CurrentPersonId, txtNewSubject.Text,
                        ParseId(ddlStudyLevel.SelectedValue)).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

                    If result.Success Then
                        txtNewSubject.Text = String.Empty

                        ' The new subject has to appear in the dropdown straight away, or the
                        ' page contradicts the message it has just shown.
                        Await BindSubjectsAsync()
                    End If

                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub Remove_Command(sender As Object, e As CommandEventArgs)
            Dim tutorSubjectId As Integer = ParseId(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture))

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim tutorId As Integer = Await ResolveTutorIdAsync()

                    Dim result As ServiceResult =
                        Await _tutors.RemoveSubjectAsync(tutorId, tutorSubjectId).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Private Shared Function ParseId(value As String) As Integer
            Dim parsed As Integer
            Return If(Integer.TryParse(value, parsed), parsed, 0)
        End Function

    End Class

End Namespace
