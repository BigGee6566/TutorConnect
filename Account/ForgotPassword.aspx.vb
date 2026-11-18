Option Strict On
Option Explicit On

Imports TutorConnect.MasterPages
Imports TutorConnect.Security

Namespace Account

    ''' <summary>
    ''' Explains how to regain access to an account.
    ''' </summary>
    ''' <remarks>
    ''' There is no self-service reset because email and SMS delivery are out of scope
    ''' (Part N). This page states the actual process rather than presenting a form that
    ''' appears to send something and does not.
    ''' </remarks>
    Public Class ForgotPassword
        Inherits BasePage

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As Auth = TryCast(Master, Auth)
            If shell IsNot Nothing Then
                shell.BrandImageUrl = "~/Images/auth/login-background.jpg"
            End If
        End Sub

    End Class

End Namespace
