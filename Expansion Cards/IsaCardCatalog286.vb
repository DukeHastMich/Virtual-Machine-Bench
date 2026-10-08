Imports System

' Catalog metadata for currently modeled removable ISA boards. Factories stay
' at the chassis assembly boundary because SB16/NE2000 construction requires the
' actual PIC/DMA wiring and current physical jumper settings.
Public NotInheritable Class IsaCardCatalog286
    Private Sub New()
    End Sub

    Public Shared Function VideoDisplayName(idInBed As String) As String
        If String.Equals(idInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
            Return "None — empty video ISA slot"
        End If
        Return "Diamond Stealth Pro ISA — S3 86C928 — 2 MiB VRAM"
    End Function

    Public Shared Function AudioDisplayName(idInBed As String) As String
        If String.Equals(idInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
            Return "None — empty audio ISA slot"
        End If
        Return "Creative Sound Blaster 16"
    End Function

    Public Shared Function NetworkDisplayName(idInBed As String) As String
        If String.Equals(idInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
            Return "None — empty network ISA slot"
        End If
        If Not String.IsNullOrWhiteSpace(idInBed) AndAlso idInBed.StartsWith("vcb:", StringComparison.OrdinalIgnoreCase) Then
            Dim packageInBed As VmbCompiledCardPackage = VmbCompiledCardStore.FindById(idInBed.Substring(4))
            If packageInBed IsNot Nothing Then Return packageInBed.Header.DisplayName
        End If
        Return "Novell NE2000-compatible"
    End Function
End Class
