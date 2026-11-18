Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace AdminArea

    ''' <summary>
    ''' The audit trail (Part 12.7). Read-only.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' There is no edit and no delete here, and none anywhere else either - the application
    ''' offers no route to change an audit record. Reaching the page at all requires the
    ''' Administrator role, which the Web.config location rule for /Admin already enforces:
    ''' an ordinary user is redirected to Login before this code runs.
    ''' </para>
    ''' <para>
    ''' Timestamps are stored in UTC and shown in South African time, which is what the rest
    ''' of the application displays.
    ''' </para>
    ''' </remarks>
    Public Class AuditLog
        Inherits BasePage

        Private ReadOnly _audit As IAuditService = New AuditService()

        Protected WithEvents ddlAction As DropDownList
        Protected WithEvents ddlEntity As DropDownList
        Protected WithEvents rptAudit As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents litCount As Literal

        Private Shared ReadOnly SouthAfrica As CultureInfo = CultureInfo.GetCultureInfo("en-ZA")

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As AdminShell = TryCast(Master, AdminShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Audit Log"
                shell.PageSubtitle = "Who changed what, and why"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim result As ServiceResult(Of IList(Of AuditEntry)) =
                Await _audit.SearchAsync(
                    NullIfEmpty(ddlAction.SelectedValue),
                    NullIfEmpty(ddlEntity.SelectedValue),
                    0).ConfigureAwait(True)

            rptAudit.DataSource = result.Data
            rptAudit.DataBind()
            rptAudit.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0

            litCount.Text = If(result.Data.Count = 1,
                               "1 record", $"{result.Data.Count} records")
        End Function

        ''' <summary>
        ''' An unselected dropdown sends "", which the query treats as "no filter" only when
        ''' it arrives as Nothing.
        ''' </summary>
        Private Shared Function NullIfEmpty(value As String) As String
            Return If(String.IsNullOrWhiteSpace(value), Nothing, value)
        End Function

        Protected Shared Function GetTimestamp(value As Object) As String
            Dim utc As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)

            ' Stored UTC, shown local - the same convention as the rest of the application.
            Return Web.HttpUtility.HtmlEncode(
                utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", SouthAfrica))
        End Function

        ''' <summary>
        ''' The actor's name, or a note that the account has since been removed.
        ''' </summary>
        ''' <remarks>
        ''' The foreign key is NO ACTION and the column is nullable precisely so a record
        ''' survives the removal of the person who caused it.
        ''' </remarks>
        Protected Shared Function GetActor(item As Object) As String
            Dim entry As AuditEntry = TryCast(item, AuditEntry)
            If entry Is Nothing Then Return String.Empty

            If Not String.IsNullOrWhiteSpace(entry.ActorName) Then Return entry.ActorName
            Return If(entry.ActorPersonId.HasValue, "(account removed)", "System")
        End Function

        Protected Shared Function GetRecord(item As Object) As String
            Dim entry As AuditEntry = TryCast(item, AuditEntry)
            If entry Is Nothing Then Return String.Empty

            Return $"{entry.EntityType} #{entry.EntityId}"
        End Function

        ''' <summary>A dash rather than an empty cell, which reads as a rendering fault.</summary>
        Protected Shared Function GetOrDash(value As Object) As String
            Dim text As String = Convert.ToString(value, CultureInfo.InvariantCulture)
            Return If(String.IsNullOrWhiteSpace(text), "-", text)
        End Function

    End Class

End Namespace
