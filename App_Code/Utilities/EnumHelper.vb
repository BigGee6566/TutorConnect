Option Strict On
Option Explicit On

Namespace Utilities

    ''' <summary>
    ''' Converts between the status strings stored in the database and the enums in
    ''' <c>TutorConnect.Enums</c>.
    ''' </summary>
    ''' <remarks>
    ''' Parsing fails loudly. A status column is protected by a CHECK constraint, so an
    ''' unrecognised value means the database and the code have diverged - that is a
    ''' defect to surface, not a condition to paper over with a default.
    ''' </remarks>
    Public NotInheritable Class EnumHelper

        Private Sub New()
            ' Static helper; never instantiated.
        End Sub

        ''' <summary>
        ''' Parses a database status string into <typeparamref name="TEnum"/>.
        ''' </summary>
        ''' <exception cref="InvalidOperationException">
        ''' The value is not a defined member of <typeparamref name="TEnum"/>.
        ''' </exception>
        Public Shared Function Parse(Of TEnum As Structure)(value As String) As TEnum
            If String.IsNullOrWhiteSpace(value) Then
                Throw New InvalidOperationException(
                    $"Cannot parse an empty value into {GetType(TEnum).Name}.")
            End If

            ' Explicitly initialised: TryParse takes it ByRef, and leaving a generic
            ' structure unassigned raises BC42108.
            Dim parsed As TEnum = Nothing
            ' ignoreCase:=False - the database stores the canonical casing, and a case
            ' mismatch means something wrote a value that bypassed the enum.
            If Not [Enum].TryParse(Of TEnum)(value.Trim(), False, parsed) Then
                Throw New InvalidOperationException(
                    $"'{value}' is not a valid {GetType(TEnum).Name}. " &
                    "The database and the application enums have diverged.")
            End If

            Return parsed
        End Function

        ''' <summary>
        ''' Renders an enum member as the string the database stores.
        ''' </summary>
        Public Shared Function ToDbValue(Of TEnum As Structure)(value As TEnum) As String
            Return value.ToString()
        End Function

    End Class

End Namespace
