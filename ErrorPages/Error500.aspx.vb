Option Strict On
Option Explicit On

Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.Utilities

Namespace ErrorPages

    ''' <summary>
    ''' Friendly "server error" page.
    ''' </summary>
    ''' <remarks>
    ''' The user is given a reference identifier and nothing else. The exception detail
    ''' belongs in the log, where ErrorLogger will write it in P9; showing a stack trace
    ''' hands an attacker a map of the application (SEC-11).
    ''' </remarks>
    Public Class Error500
        Inherits Page

        Protected WithEvents litReference As Literal

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Response.StatusCode = 500
            Response.TrySkipIisCustomErrors = True

            ' The reference comes from ErrorLogger via Global.asax, so quoting it finds the
            ' matching log entry. Generating one here instead - as this page used to -
            ' handed the user a number that appeared in no log at all.
            Dim reference As String = TryCast(Context.Items(ErrorLogger.ReferenceKey), String)

            If String.IsNullOrEmpty(reference) Then
                ' Reached directly rather than through a fault, so there is nothing to
                ' quote. Say so rather than invent a reference.
                litReference.Text = "(none - this page was opened directly)"
            Else
                litReference.Text = reference
            End If
        End Sub

    End Class

End Namespace
