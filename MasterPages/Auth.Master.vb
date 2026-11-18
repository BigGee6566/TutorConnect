Option Strict On
Option Explicit On

Imports System.Web.UI
Imports System.Web.UI.HtmlControls

Namespace MasterPages

    ''' <summary>
    ''' Shell for the signed-out screens: split panel, brand on the left, form on the right.
    ''' </summary>
    Public Class Auth
        Inherits MasterPage

        Protected WithEvents brandPanel As HtmlGenericControl

        ''' <summary>
        ''' Application-relative path of the photograph behind the brand panel. Login and
        ''' Register set this to different images; anything falsy leaves the panel a flat
        ''' navy block, which still meets contrast.
        ''' </summary>
        Public Property BrandImageUrl As String

        Protected Sub Page_PreRender(sender As Object, e As EventArgs) Handles Me.PreRender
            If String.IsNullOrWhiteSpace(BrandImageUrl) Then Return

            ' Set as an inline background-image because the URL is dynamic - one of the two
            ' cases where inline style is allowed (Part E.1).
            brandPanel.Style("background-image") = $"url('{ResolveUrl(BrandImageUrl)}')"
        End Sub

    End Class

End Namespace
