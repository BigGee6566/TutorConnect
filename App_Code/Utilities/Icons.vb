Option Strict On
Option Explicit On

Namespace Utilities

    ''' <summary>
    ''' The application's icon set, as inline SVG.
    ''' </summary>
    ''' <remarks>
    ''' Inline rather than an icon font or a CDN sprite (§G.2): no external request, no
    ''' flash of missing glyphs, and each icon inherits currentColor so a single markup
    ''' string serves the navy sidebar and the tinted dashboard cards alike.
    '''
    ''' No width or height is set here - size is a CSS concern, which is how the same
    ''' string renders at 20px in the rail and 40px on a card.
    ''' </remarks>
    Public NotInheritable Class Icons

        Private Const Attributes As String =
            "viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" " &
            "stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true"" focusable=""false"""

        Private Sub New()
            ' Static library; never instantiated.
        End Sub

        Private Shared Function Svg(paths As String) As String
            Return $"<svg {Attributes}>{paths}</svg>"
        End Function

        Public Shared ReadOnly Property Home As String
            Get
                Return Svg("<path d=""m3 10 9-7 9 7v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z""/><path d=""M9 22V12h6v10""/>")
            End Get
        End Property

        Public Shared ReadOnly Property Person As String
            Get
                Return Svg("<path d=""M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2""/><circle cx=""12"" cy=""7"" r=""4""/>")
            End Get
        End Property

        Public Shared ReadOnly Property People As String
            Get
                Return Svg("<path d=""M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2""/><circle cx=""9"" cy=""7"" r=""4""/>" &
                           "<path d=""M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75""/>")
            End Get
        End Property

        Public Shared ReadOnly Property Book As String
            Get
                Return Svg("<path d=""M4 19.5A2.5 2.5 0 0 1 6.5 17H20""/>" &
                           "<path d=""M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z""/>")
            End Get
        End Property

        ''' <summary>Stacked books - the tutor's "My Subjects" card is green in the design.</summary>
        Public Shared ReadOnly Property Books As String
            Get
                Return Svg("<path d=""M4 5a2 2 0 0 1 2-2h3v18H6a2 2 0 0 1-2-2z""/>" &
                           "<path d=""M11 3h3a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-3z""/><path d=""M18 6l3 1-3 12""/>")
            End Get
        End Property

        Public Shared ReadOnly Property Search As String
            Get
                Return Svg("<circle cx=""11"" cy=""11"" r=""7""/><path d=""m21 21-4.3-4.3""/>")
            End Get
        End Property

        Public Shared ReadOnly Property Clipboard As String
            Get
                Return Svg("<path d=""M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2""/>" &
                           "<rect x=""8"" y=""2"" width=""8"" height=""4"" rx=""1""/><path d=""M9 12h6M9 16h6""/>")
            End Get
        End Property

        Public Shared ReadOnly Property Calendar As String
            Get
                Return Svg("<rect x=""3"" y=""4"" width=""18"" height=""18"" rx=""2""/><path d=""M16 2v4M8 2v4M3 10h18""/>")
            End Get
        End Property

        ''' <summary>Filled star - the reviews card and the rating rows use gold.</summary>
        Public Shared ReadOnly Property Star As String
            Get
                Return "<svg viewBox=""0 0 24 24"" fill=""currentColor"" aria-hidden=""true"" focusable=""false"">" &
                       "<path d=""m12 2 3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14l-5-4.87 6.91-1.01z""/></svg>"
            End Get
        End Property

        Public Shared ReadOnly Property Logout As String
            Get
                Return Svg("<path d=""M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4""/><path d=""m16 17 5-5-5-5M21 12H9""/>")
            End Get
        End Property

    End Class

End Namespace
