Option Strict On
Option Explicit On

Imports System.Text.RegularExpressions

Namespace Validation

    ''' <summary>
    ''' Rules for a subject name typed by a person, rather than chosen from a list.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' Until students and tutors could add their own subjects, the only rule anywhere was
    ''' AdminService's whitespace check. That was survivable while one trusted person typed
    ''' the names; it is not, now that anyone can. In particular a name longer than the
    ''' column was <em>silently truncated</em> by the SqlParameter's size rather than
    ''' refused, so "Introduction to ...(120 chars)" became a different subject from the one
    ''' the person thought they had created.
    ''' </para>
    ''' <para>
    ''' <see cref="Normalise"/> is the important half. Find Tutor matches a student to a
    ''' tutor by SubjectId equality, so two spellings of one subject are two subjects that
    ''' can never meet. Every path that turns typed text into a SubjectId must normalise
    ''' through this one function, or the reuse lookup silently stops working.
    ''' </para>
    ''' <para>
    ''' Like <see cref="UserValidator"/>, this class touches neither the database nor HTTP,
    ''' so it can be unit tested directly. Uniqueness needs the database and belongs to the
    ''' service.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class SubjectValidator

        ''' <summary>Matches Subject.SubjectName NVARCHAR(100).</summary>
        Public Const MaximumNameLength As Integer = 100

        ''' <summary>Short subjects are real - "IT", "IS" - but one character is a typo.</summary>
        Public Const MinimumNameLength As Integer = 2

        ' Deliberately permissive. The point is to exclude junk, not to police spelling:
        ' "Life & Physical Sciences", "IT (Networking)" and "Maths/Stats" are all legitimate
        ' names that a stricter letters-and-spaces pattern would reject.
        Private Shared ReadOnly AllowedPattern As New Regex(
            "^[\p{L}\p{N} &\-'.,/()+]+$",
            RegexOptions.Compiled Or RegexOptions.CultureInvariant)

        Private Shared ReadOnly WhitespaceRuns As New Regex(
            "\s+",
            RegexOptions.Compiled Or RegexOptions.CultureInvariant)

        Private Sub New()
            ' Static helper; never instantiated.
        End Sub

        ''' <summary>
        ''' Trims, then collapses every run of internal whitespace to a single space, so
        ''' " Physical   Science " and "Physical Science" are the same subject.
        ''' </summary>
        ''' <remarks>
        ''' Case is deliberately left alone. The database collation is case-insensitive, so
        ''' the lookup already matches "maths" to "Maths"; lower-casing here would only
        ''' damage the display name of the first person to create it.
        ''' </remarks>
        Public Shared Function Normalise(subjectName As String) As String
            If String.IsNullOrWhiteSpace(subjectName) Then Return String.Empty

            Return WhitespaceRuns.Replace(subjectName.Trim(), " ")
        End Function

        ''' <summary>
        ''' Validates a name that has already been through <see cref="Normalise"/>.
        ''' </summary>
        Public Shared Function ValidateName(subjectName As String) As ValidationResult
            Dim result As New ValidationResult()
            Dim name As String = Normalise(subjectName)

            If name.Length = 0 Then
                result.AddError("SubjectName", "Enter a subject name.")
                Return result
            End If

            If name.Length < MinimumNameLength Then
                result.AddError("SubjectName",
                                $"A subject name must be at least {MinimumNameLength} characters long.")
            End If

            ' Refused, not truncated: a truncated name is a different subject from the one
            ' the person meant to create, and nothing would tell them.
            If name.Length > MaximumNameLength Then
                result.AddError("SubjectName",
                                $"A subject name cannot be longer than {MaximumNameLength} characters.")
            End If

            If Not name.Any(AddressOf Char.IsLetter) Then
                result.AddError("SubjectName", "A subject name must contain at least one letter.")
            End If

            If Not AllowedPattern.IsMatch(name) Then
                result.AddError("SubjectName",
                                "A subject name can contain only letters, numbers, spaces and the " &
                                "punctuation & - ' . , / ( ) +")
            End If

            Return result
        End Function

    End Class

End Namespace
