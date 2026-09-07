Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO

' Host-side SoundFont discovery.  This is deliberately boring filesystem metadata:
' it does not parse or load multi-hundred-megabyte banks while the user is merely
' choosing one, and it never runs on the emulated machine thread.
Public NotInheritable Class SoundFontBankInfo
    Public Sub New(fullPathInBed As String)
        FullPath = Path.GetFullPath(fullPathInBed)
        FileName = Path.GetFileName(FullPath)
        Dim infoInBed As New FileInfo(FullPath)
        Length = infoInBed.Length
        LastWriteTimeUtc = infoInBed.LastWriteTimeUtc
    End Sub

    Public ReadOnly Property FullPath As String
    Public ReadOnly Property FileName As String
    Public ReadOnly Property Length As Long
    Public ReadOnly Property LastWriteTimeUtc As DateTime

    Public ReadOnly Property DisplayText As String
        Get
            Return FileName & "   (" & FormatByteSize(Length) & ")"
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return DisplayText
    End Function

    Public Shared Function FormatByteSize(bytesInBed As Long) As String
        Const KiB As Double = 1024.0R
        Const MiB As Double = KiB * 1024.0R
        Const GiB As Double = MiB * 1024.0R
        If bytesInBed >= GiB Then Return (bytesInBed / GiB).ToString("0.00") & " GiB"
        If bytesInBed >= MiB Then Return (bytesInBed / MiB).ToString("0.0") & " MiB"
        If bytesInBed >= KiB Then Return (bytesInBed / KiB).ToString("0.0") & " KiB"
        Return bytesInBed.ToString() & " bytes"
    End Function
End Class

Public Enum SoundFontResolutionReason As Byte
    ExactRequestedBank = 0
    SmallestBankFallback = 1
    RequestedBankMissing = 2
    NoBanksAvailable = 3
End Enum

' Immutable result of resolving a configured bank request against the shared host
' repository.  RequestedFileName is configuration. ResolvedBank is host reality.
Public NotInheritable Class SoundFontBankResolution
    Public Sub New(requestedFileNameInBed As String,
                   resolvedBankInBed As SoundFontBankInfo,
                   reasonInBed As SoundFontResolutionReason)
        RequestedFileName = WavetableAttachmentSettings.NormalizeBankFileName(requestedFileNameInBed)
        ResolvedBank = resolvedBankInBed
        Reason = reasonInBed
    End Sub

    Public ReadOnly Property RequestedFileName As String
    Public ReadOnly Property ResolvedBank As SoundFontBankInfo
    Public ReadOnly Property Reason As SoundFontResolutionReason

    Public ReadOnly Property HasBank As Boolean
        Get
            Return ResolvedBank IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property IsAutomaticFallback As Boolean
        Get
            Return Reason = SoundFontResolutionReason.SmallestBankFallback
        End Get
    End Property

    Public Function Summary() As String
        Select Case Reason
            Case SoundFontResolutionReason.ExactRequestedBank
                Return "exact requested bank: " & If(ResolvedBank Is Nothing, RequestedFileName, ResolvedBank.FileName)
            Case SoundFontResolutionReason.SmallestBankFallback
                If ResolvedBank Is Nothing Then Return "automatic fallback unavailable"
                Return "smallest .sf2 fallback: " & ResolvedBank.FileName & " (" & SoundFontBankInfo.FormatByteSize(ResolvedBank.Length) & ")"
            Case SoundFontResolutionReason.RequestedBankMissing
                Return "explicit bank missing: " & RequestedFileName
            Case Else
                Return "no .sf2 banks available"
        End Select
    End Function
End Class

Public NotInheritable Class SoundFontCatalog
    Public Const SharedBanksDirectoryName As String = "Banks"

    Private Sub New()
    End Sub

    ' All wavetable-capable cards share one host bank repository at the project
    ' source root.  Callers should use this instead of constructing card-relative
    ' paths.  ResolveProjectDirectory walks up from both the process working
    ' directory and executable directory, so Debug/Release output folders still
    ' locate the source-tree Banks folder during development.
    Public Shared Function ResolveSharedBanksDirectory() As String
        Return ResolveProjectDirectory(SharedBanksDirectoryName)
    End Function

    Public Shared Function EnumerateBanks(bankDirectoryInBed As String) As List(Of SoundFontBankInfo)
        Dim resultInBed As New List(Of SoundFontBankInfo)()
        If String.IsNullOrWhiteSpace(bankDirectoryInBed) OrElse Not Directory.Exists(bankDirectoryInBed) Then Return resultInBed

        Try
            For Each pathInBed As String In Directory.EnumerateFiles(bankDirectoryInBed, "*", SearchOption.TopDirectoryOnly)
                If Not String.Equals(Path.GetExtension(pathInBed), ".sf2", StringComparison.OrdinalIgnoreCase) Then Continue For
                Try
                    resultInBed.Add(New SoundFontBankInfo(pathInBed))
                Catch ex As IOException
                    ' A file can disappear between enumeration and metadata read.  A
                    ' host catalog refresh should simply omit it, never affect the VM.
                Catch ex As UnauthorizedAccessException
                End Try
            Next
        Catch ex As IOException
        Catch ex As UnauthorizedAccessException
        End Try

        resultInBed.Sort(Function(aInBed, bInBed) StringComparer.OrdinalIgnoreCase.Compare(aInBed.FileName, bInBed.FileName))
        Return resultInBed
    End Function

    ' Resolution policy:
    ' 1. If the requested filename exists, use it.
    ' 2. If the request is the automatic per-card default (SB16.sf2) and it is
    '    absent, choose the smallest .sf2; filename breaks equal-size ties.
    ' 3. If any other explicitly selected filename is absent, remain unresolved.
    ' This keeps a missing user choice visible instead of unexpectedly changing the
    ' instrument set, while still giving a new installation a cheap deterministic
    ' bank without hard-coding a particular third-party SoundFont filename.
    Public Shared Function ResolveBank(bankDirectoryInBed As String,
                                       requestedFileNameInBed As String) As SoundFontBankResolution
        Dim requestedInBed As String = WavetableAttachmentSettings.NormalizeBankFileName(requestedFileNameInBed)
        Dim banksInBed As List(Of SoundFontBankInfo) = EnumerateBanks(bankDirectoryInBed)

        For Each bankInBed As SoundFontBankInfo In banksInBed
            If String.Equals(bankInBed.FileName, requestedInBed, StringComparison.OrdinalIgnoreCase) Then
                Return New SoundFontBankResolution(requestedInBed, bankInBed, SoundFontResolutionReason.ExactRequestedBank)
            End If
        Next

        If banksInBed.Count = 0 Then
            Return New SoundFontBankResolution(requestedInBed, Nothing, SoundFontResolutionReason.NoBanksAvailable)
        End If

        If WavetableAttachmentSettings.IsAutomaticDefaultRequest(requestedInBed) Then
            Dim smallestInBed As SoundFontBankInfo = banksInBed(0)
            For indexInBed As Integer = 1 To banksInBed.Count - 1
                Dim candidateInBed As SoundFontBankInfo = banksInBed(indexInBed)
                If candidateInBed.Length < smallestInBed.Length OrElse
                   (candidateInBed.Length = smallestInBed.Length AndAlso
                    StringComparer.OrdinalIgnoreCase.Compare(candidateInBed.FileName, smallestInBed.FileName) < 0) Then
                    smallestInBed = candidateInBed
                End If
            Next
            Return New SoundFontBankResolution(requestedInBed, smallestInBed, SoundFontResolutionReason.SmallestBankFallback)
        End If

        Return New SoundFontBankResolution(requestedInBed, Nothing, SoundFontResolutionReason.RequestedBankMissing)
    End Function

    ' Resolve a directory that lives in the source/working tree without assuming
    ' Visual Studio's current directory.  This also permits a future packaged build
    ' to place the same relative tree beside the executable.
    Public Shared Function ResolveProjectDirectory(relativePathInBed As String) As String
        If String.IsNullOrWhiteSpace(relativePathInBed) Then Throw New ArgumentNullException(NameOf(relativePathInBed))

        Dim seedsInBed As New List(Of String)()
        AddSeedInBed(seedsInBed, Environment.CurrentDirectory)
        AddSeedInBed(seedsInBed, AppContext.BaseDirectory)

        For Each seedInBed As String In seedsInBed
            Dim currentInBed As DirectoryInfo = Nothing
            Try
                currentInBed = New DirectoryInfo(Path.GetFullPath(seedInBed))
            Catch
                Continue For
            End Try

            For depthInBed As Integer = 0 To 10
                Dim candidateInBed As String = Path.Combine(currentInBed.FullName, relativePathInBed)
                If Directory.Exists(candidateInBed) Then Return Path.GetFullPath(candidateInBed)
                currentInBed = currentInBed.Parent
                If currentInBed Is Nothing Then Exit For
            Next
        Next

        ' Return a deterministic expected location even when the folder is absent so
        ' the configurator can explain exactly where it looked.
        Return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, relativePathInBed))
    End Function

    Private Shared Sub AddSeedInBed(seedsInBed As List(Of String), valueInBed As String)
        If String.IsNullOrWhiteSpace(valueInBed) Then Return
        For Each existingInBed As String In seedsInBed
            If String.Equals(existingInBed, valueInBed, StringComparison.OrdinalIgnoreCase) Then Return
        Next
        seedsInBed.Add(valueInBed)
    End Sub
End Class
