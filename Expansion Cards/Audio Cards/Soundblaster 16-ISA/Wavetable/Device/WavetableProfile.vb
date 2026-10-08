Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

' Shared description of an emulated wavetable attachment personality.  This is
' configuration/device identity only; it does not load SoundFonts or synthesize
' audio.  Named historical boards should be added only when their guest-visible
' behavior is backed by manuals or verified hardware traces.
Public NotInheritable Class WavetableProfile
    Public Sub New(idInBed As String,
                   displayNameInBed As String,
                   connectorDescriptionInBed As String,
                   generalMidiCompatibleInBed As Boolean,
                   drumChannelOneBasedInBed As Integer,
                   polyphonyLimitInBed As Integer)
        Id = If(idInBed, String.Empty).Trim()
        DisplayName = If(displayNameInBed, String.Empty).Trim()
        ConnectorDescription = If(connectorDescriptionInBed, String.Empty).Trim()
        GeneralMidiCompatible = generalMidiCompatibleInBed
        DrumChannelOneBased = drumChannelOneBasedInBed
        PolyphonyLimit = Math.Max(0, polyphonyLimitInBed)
    End Sub

    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property ConnectorDescription As String
    Public ReadOnly Property GeneralMidiCompatible As Boolean

    ' MIDI convention uses channel 10 for percussion.  Stored one-based here so
    ' host/configuration text does not have to translate the human-facing number.
    Public ReadOnly Property DrumChannelOneBased As Integer

    ' Zero means intentionally unspecified by the generic profile.  Do not invent
    ' a historical hardware voice limit until a real board profile is modeled.
    Public ReadOnly Property PolyphonyLimit As Integer

    Public Overrides Function ToString() As String
        Return DisplayName
    End Function
End Class

Public NotInheritable Class WavetableProfileCatalog
    Private Shared ReadOnly _profilesInBed As WavetableProfile() = {
        New WavetableProfile(
            WavetableAttachmentSettings.GenericWaveBlasterProfileId,
            "Generic Wave Blaster-compatible daughterboard",
            "Wave Blaster-compatible MIDI daughterboard connector",
            generalMidiCompatibleInBed:=True,
            drumChannelOneBasedInBed:=10,
            polyphonyLimitInBed:=0)
    }

    Private Sub New()
    End Sub

    Public Shared Function AllProfiles() As IReadOnlyList(Of WavetableProfile)
        Return Array.AsReadOnly(_profilesInBed)
    End Function

    Public Shared Function FindById(idInBed As String) As WavetableProfile
        Dim normalizedInBed As String = WavetableAttachmentSettings.NormalizeProfileId(idInBed)
        For Each profileInBed As WavetableProfile In _profilesInBed
            If String.Equals(profileInBed.Id, normalizedInBed, StringComparison.OrdinalIgnoreCase) Then Return profileInBed
        Next
        Return _profilesInBed(0)
    End Function
End Class
