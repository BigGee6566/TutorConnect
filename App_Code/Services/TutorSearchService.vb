Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Models

Namespace Services

    ''' <summary>
    ''' The Find Tutor screen.
    ''' </summary>
    ''' <remarks>
    ''' BR-26 is enforced in the repository's WHERE clause, not here: an unapproved or
    ''' deactivated tutor cannot be returned whatever this layer asks for. That placement
    ''' is deliberate - a rule that keeps people out of results should not depend on every
    ''' caller remembering to pass a flag.
    ''' </remarks>
    Public Class TutorSearchService
        Implements ITutorSearchService

        ''' <summary>Rows per page on Find Tutor.</summary>
        Public Const DefaultPageSize As Integer = 10

        Private ReadOnly _tutors As TutorRepository

        Public Sub New()
            Me.New(New TutorRepository())
        End Sub

        Public Sub New(tutors As TutorRepository)
            If tutors Is Nothing Then Throw New ArgumentNullException(NameOf(tutors))
            _tutors = tutors
        End Sub

        ''' <inheritdoc/>
        Public Async Function SearchAsync(subjectId As Integer?,
                                          studyLevelId As Integer?,
                                          qualificationContains As String,
                                          minimumExperience As Integer?,
                                          pageNumber As Integer,
                                          pageSize As Integer) As Task(Of ServiceResult(Of IList(Of TutorSearchResult))) _
            Implements ITutorSearchService.SearchAsync

            Dim page As Integer = Math.Max(1, pageNumber)
            Dim size As Integer = If(pageSize <= 0, DefaultPageSize, Math.Min(pageSize, 100))

            Dim rows As IList(Of TutorSearchResult) = Await _tutors.SearchAsync(
                subjectId, studyLevelId, NullIfBlank(qualificationContains),
                minimumExperience, page, size).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of TutorSearchResult)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function CountAsync(subjectId As Integer?,
                                         studyLevelId As Integer?,
                                         qualificationContains As String,
                                         minimumExperience As Integer?) As Task(Of Integer) _
            Implements ITutorSearchService.CountAsync

            Return Await _tutors.SearchCountAsync(
                subjectId, studyLevelId, NullIfBlank(qualificationContains),
                minimumExperience).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>
        ''' Returns a failure, not Nothing, when the TutorSubject is hidden or absent - so a
        ''' student guessing an id learns nothing about whether it exists.
        ''' </remarks>
        Public Async Function GetDetailAsync(tutorSubjectId As Integer) As Task(Of ServiceResult(Of TutorSearchResult)) _
            Implements ITutorSearchService.GetDetailAsync

            If tutorSubjectId <= 0 Then
                Return ServiceResult(Of TutorSearchResult).Fail("That tutor is not available.")
            End If

            Dim detail As TutorSearchResult =
                Await _tutors.GetSearchResultAsync(tutorSubjectId).ConfigureAwait(False)

            If detail Is Nothing Then
                Return ServiceResult(Of TutorSearchResult).Fail("That tutor is not available.")
            End If

            Return ServiceResult(Of TutorSearchResult).Ok(detail)
        End Function

        ''' <inheritdoc/>
        Public Async Function GetOpenSlotsAsync(tutorSubjectId As Integer) As Task(Of ServiceResult(Of IList(Of TutorAvailability))) _
            Implements ITutorSearchService.GetOpenSlotsAsync

            Dim slots As IList(Of TutorAvailability) =
                Await _tutors.GetOpenAvailabilityAsync(tutorSubjectId).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of TutorAvailability)).Ok(slots)
        End Function

        ''' <summary>Blank filters must mean "no filter", not "match the empty string".</summary>
        Private Shared Function NullIfBlank(value As String) As String
            Return If(String.IsNullOrWhiteSpace(value), Nothing, value.Trim())
        End Function

    End Class

End Namespace
