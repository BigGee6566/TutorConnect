Option Strict On
Option Explicit On

Imports System.Web
Imports System.Web.UI
Imports TutorConnect.Security

Namespace Account

    ''' <summary>
    ''' Signs the user out and returns them to Login.
    ''' </summary>
    ''' <remarks>
    ''' Inherits Page rather than BasePage: BasePage writes a ViewStateUserKey into the
    ''' session, and there is no sense creating session state on the way out.
    ''' </remarks>
    Public Class Logout
        Inherits Page

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            ' Clears the Forms ticket and abandons the session.
            RoleHelper.SignOut(New HttpContextWrapper(Context))

            Response.Redirect("~/Account/Login.aspx", endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Sub

    End Class

End Namespace
