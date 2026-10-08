Option Strict On
Option Explicit On

Imports System

' Reusable host/chassis configuration for a wavetable attachment.  This class is
' intentionally not Sound-Blaster-specific: any sound card which can host a ROM/
' wavetable daughterboard may own one of these settings objects and use the same
' configuration dialog/catalog code.
Public NotInheritable Class WavetableAttachmentSettings
    Public Const GenericWaveBlasterProfileId As String = "generic-waveblaster"

    ' Brick 5 changes the automatic request to a conventional per-card filename.
    ' If this file is absent, the host resolver selects the smallest .sf2 in the
    ' shared root Banks directory.  An explicitly selected *other* filename does
    ' NOT silently fall back when it disappears.
    Public Const DefaultSoundFontFileName As String = "SB16.sf2"
    Public Const LegacyDefaultSoundFontFileName As String = "uo_4mb_2.sf2"
    Public Const CurrentBankPolicyVersion As Integer = 2

    Public Property Installed As Boolean = True
    Public Property ProfileId As String = GenericWaveBlasterProfileId
    Public Property BankFileName As String = DefaultSoundFontFileName

    Public Function CloneSettings() As WavetableAttachmentSettings
        Return New WavetableAttachmentSettings With {
            .Installed = Installed,
            .ProfileId = NormalizeProfileId(ProfileId),
            .BankFileName = NormalizeBankFileName(BankFileName)
        }
    End Function

    Public Function SameAs(otherInBed As WavetableAttachmentSettings) As Boolean
        If otherInBed Is Nothing Then Return False
        Return Installed = otherInBed.Installed AndAlso
               String.Equals(NormalizeProfileId(ProfileId), NormalizeProfileId(otherInBed.ProfileId), StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(NormalizeBankFileName(BankFileName), NormalizeBankFileName(otherInBed.BankFileName), StringComparison.OrdinalIgnoreCase)
    End Function

    Public Function Summary() As String
        If Not Installed Then Return "wavetable disabled"
        Dim bankInBed As String = NormalizeBankFileName(BankFileName)
        If IsAutomaticDefaultRequest(bankInBed) Then Return "wavetable automatic (" & DefaultSoundFontFileName & " -> smallest .sf2 fallback)"
        Return "wavetable " & bankInBed
    End Function

    Public Shared Function NormalizeProfileId(valueInBed As String) As String
        Dim value As String = If(valueInBed, String.Empty).Trim()
        If value.Length = 0 Then Return GenericWaveBlasterProfileId
        Return value
    End Function

    Public Shared Function NormalizeBankFileName(valueInBed As String) As String
        Dim value As String = If(valueInBed, String.Empty).Trim()
        If value.Length = 0 Then Return DefaultSoundFontFileName
        ' Banks live in the shared project-root Banks directory.  Persist only
        ' the filename so machine.ini never captures a developer-specific absolute
        ' path and every wavetable-capable card can reuse the same bank repository.
        Return IO.Path.GetFileName(value)
    End Function

    Public Shared Function IsAutomaticDefaultRequest(valueInBed As String) As Boolean
        Return String.Equals(NormalizeBankFileName(valueInBed), DefaultSoundFontFileName, StringComparison.OrdinalIgnoreCase)
    End Function
End Class
