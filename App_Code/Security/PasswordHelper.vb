Option Strict On
Option Explicit On

Imports System.Security.Cryptography

Namespace Security

    ''' <summary>
    ''' PBKDF2 password hashing and verification (SEC-01, SEC-02).
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' The project targets .NET Framework 4.8.1. The
    ''' <c>Rfc2898DeriveBytes(String, Byte(), Integer, HashAlgorithmName)</c> overload was
    ''' added in 4.7.2, so SHA-256 is available here and there is no need to fall back to
    ''' PBKDF2-SHA1 or to hand-roll HMAC. This was verified by compiling, not assumed.
    ''' If this file stops compiling, the project's TargetFrameworkVersion has been
    ''' lowered - fix that rather than weakening the hash.
    ''' </para>
    ''' <para>
    ''' Verified 23 August 2026 against .NET Framework 4.8.1: the Rfc2898DeriveBytes
    ''' SHA-256 overload compiles, but CryptographicOperations does NOT exist on .NET
    ''' Framework. See <see cref="FixedTimeEquals"/>.
    ''' </para>
    ''' <para>
    ''' A plain-text password never leaves this class, is never logged, and is never
    ''' stored. Only the hash, the salt and the iteration count are persisted.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class PasswordHelper

        ''' <summary>Work factor for new passwords. Stored per row so it can be raised later.</summary>
        Public Const DefaultIterations As Integer = 100000

        ''' <summary>16 bytes, per SEC-01.</summary>
        Public Const SaltBytes As Integer = 16

        ''' <summary>32 bytes, per SEC-01. Fits PasswordHash VARBINARY(64) with room to grow.</summary>
        Public Const SubkeyBytes As Integer = 32

        Private Sub New()
            ' Static helper; never instantiated.
        End Sub

        ''' <summary>
        ''' Creates a fresh salt and hash for a new or changed password.
        ''' </summary>
        Public Shared Sub CreateHash(password As String,
                                     <Runtime.InteropServices.Out> ByRef hash As Byte(),
                                     <Runtime.InteropServices.Out> ByRef salt As Byte(),
                                     <Runtime.InteropServices.Out> ByRef iterations As Integer)
            If String.IsNullOrEmpty(password) Then
                Throw New ArgumentException("A password is required.", NameOf(password))
            End If

            salt = New Byte(SaltBytes - 1) {}
            ' RandomNumberGenerator, not Random: Random is predictable from its seed.
            Using generator As RandomNumberGenerator = RandomNumberGenerator.Create()
                generator.GetBytes(salt)
            End Using

            iterations = DefaultIterations
            hash = DeriveKey(password, salt, iterations)
        End Sub

        ''' <summary>
        ''' Verifies a candidate password against a stored hash.
        ''' </summary>
        ''' <remarks>
        ''' The comparison is fixed-time. Comparing with = would return as soon as two bytes
        ''' differed, and the timing difference leaks how much of the hash was guessed
        ''' correctly.
        ''' </remarks>
        Public Shared Function Verify(password As String,
                                      storedHash As Byte(),
                                      storedSalt As Byte(),
                                      iterations As Integer) As Boolean
            If String.IsNullOrEmpty(password) Then Return False
            If storedHash Is Nothing OrElse storedSalt Is Nothing Then Return False
            If iterations <= 0 Then Return False

            Dim computed As Byte() = DeriveKey(password, storedSalt, iterations, storedHash.Length)
            Return FixedTimeEquals(computed, storedHash)
        End Function

        ''' <summary>
        ''' Constant-time byte comparison.
        ''' </summary>
        ''' <remarks>
        ''' System.Security.Cryptography.CryptographicOperations is NOT available on .NET
        ''' Framework - it was introduced in .NET Core 2.1 and never back-ported - so the
        ''' comparison is written out here. (No cref: the type genuinely does not resolve
        ''' on this target, which is the whole reason this method exists.)
        '''
        ''' The loop deliberately never exits early: it accumulates the XOR of every byte
        ''' pair and tests the accumulator once at the end. Returning as soon as a byte
        ''' differs would make the running time reveal how many leading bytes an attacker
        ''' had guessed correctly.
        '''
        ''' Comparing lengths first is safe: the hash length is fixed and public.
        ''' </remarks>
        Private Shared Function FixedTimeEquals(left As Byte(), right As Byte()) As Boolean
            If left Is Nothing OrElse right Is Nothing Then Return False
            If left.Length <> right.Length Then Return False

            Dim difference As Integer = 0
            For index As Integer = 0 To left.Length - 1
                difference = difference Or (left(index) Xor right(index))
            Next

            Return difference = 0
        End Function

        ''' <summary>
        ''' True when a stored hash was made with a weaker work factor than the current
        ''' default, so it can be re-hashed transparently on the user's next sign-in.
        ''' </summary>
        Public Shared Function NeedsRehash(iterations As Integer) As Boolean
            Return iterations < DefaultIterations
        End Function

        Private Shared Function DeriveKey(password As String,
                                          salt As Byte(),
                                          iterations As Integer,
                                          Optional length As Integer = SubkeyBytes) As Byte()
            ' Requires .NET Framework 4.7.2+. See the remarks on this class.
            Using kdf As New Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)
                Return kdf.GetBytes(length)
            End Using
        End Function

    End Class

End Namespace
