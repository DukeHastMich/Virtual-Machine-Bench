Imports System
Imports System.IO

' Host/chassis population state. These values answer the physical question
' "what is actually plugged into this machine?" and are deliberately separate
' from guest CMOS and from per-card jumper/DIP settings.
Public NotInheritable Class MachineHardwarePopulationConfiguration
    Private Const MachineConfigurationName As String = "VirtualComputer.machine.ini"
    Private ReadOnly _configurationPathInBed As String

    Public Const NoneId As String = "none"
    Public Const HarrisCs80C28625Id As String = ProcessorPackageCatalog.HarrisCs80C28625Id
    Public Const Intel80287Id As String = NumericCoprocessorPackageCatalog.Intel80287Id
    Public Const DiamondStealthPro928Id As String = "diamond-stealth-pro-928"
    Public Const SoundBlaster16Id As String = "sound-blaster-16"
    Public Const Ne2000Id As String = "ne2000"

    Public Sub New(baseDirectoryInBed As String)
        If String.IsNullOrWhiteSpace(baseDirectoryInBed) Then Throw New ArgumentNullException(NameOf(baseDirectoryInBed))
        _configurationPathInBed = Path.Combine(Path.GetFullPath(baseDirectoryInBed), MachineConfigurationName)
    End Sub

    Public Property MotherboardId As String = MotherboardCatalog.CtCs8221NeatAt286Id
    Public Property CpuId As String = HarrisCs80C28625Id
    Public Property NumericCoprocessorId As String = Intel80287Id
    Public Property VideoCardId As String = DiamondStealthPro928Id
    Public Property AudioCardId As String = SoundBlaster16Id
    Public Property NetworkCardId As String = Ne2000Id

    Public Function CloneConfiguration() As MachineHardwarePopulationConfiguration
        Return New MachineHardwarePopulationConfiguration(Path.GetDirectoryName(_configurationPathInBed)) With {
            .MotherboardId = MotherboardId,
            .CpuId = CpuId,
            .NumericCoprocessorId = NumericCoprocessorId,
            .VideoCardId = VideoCardId,
            .AudioCardId = AudioCardId,
            .NetworkCardId = NetworkCardId
        }
    End Function

    Public Sub LoadConfiguration()
        MotherboardId = MotherboardCatalog.NormalizeId(MachineConfigurationStore.ReadValue(_configurationPathInBed, "HardwarePopulation", "Motherboard", MotherboardCatalog.CtCs8221NeatAt286Id))
        CpuId = MotherboardCatalog.NormalizeCpuIdForBoard(MotherboardId, MachineConfigurationStore.ReadValue(_configurationPathInBed, "HardwarePopulation", "CPU", MotherboardCatalog.Require(MotherboardId).DefaultCpuId))
        NumericCoprocessorId = MotherboardCatalog.NormalizeNumericCoprocessorIdForBoard(MotherboardId, MachineConfigurationStore.ReadValue(_configurationPathInBed, "HardwarePopulation", "NumericCoprocessor", MotherboardCatalog.Require(MotherboardId).DefaultNumericCoprocessorId))
        VideoCardId = NormalizeVideoCardId(MachineConfigurationStore.ReadValue(_configurationPathInBed, "HardwarePopulation", "Video", DiamondStealthPro928Id))
        AudioCardId = NormalizeAudioCardId(MachineConfigurationStore.ReadValue(_configurationPathInBed, "HardwarePopulation", "Audio", SoundBlaster16Id))
        NetworkCardId = NormalizeNetworkCardId(MachineConfigurationStore.ReadValue(_configurationPathInBed, "HardwarePopulation", "Network", Ne2000Id))
    End Sub

    Public Sub SaveConfiguration()
        MachineConfigurationStore.WriteValue(_configurationPathInBed, "HardwarePopulation", "Motherboard", MotherboardCatalog.NormalizeId(MotherboardId))
        MachineConfigurationStore.WriteValue(_configurationPathInBed, "HardwarePopulation", "CPU", MotherboardCatalog.NormalizeCpuIdForBoard(MotherboardId, CpuId))
        MachineConfigurationStore.WriteValue(_configurationPathInBed, "HardwarePopulation", "NumericCoprocessor", MotherboardCatalog.NormalizeNumericCoprocessorIdForBoard(MotherboardId, NumericCoprocessorId))
        MachineConfigurationStore.WriteValue(_configurationPathInBed, "HardwarePopulation", "Video", NormalizeVideoCardId(VideoCardId))
        MachineConfigurationStore.WriteValue(_configurationPathInBed, "HardwarePopulation", "Audio", NormalizeAudioCardId(AudioCardId))
        MachineConfigurationStore.WriteValue(_configurationPathInBed, "HardwarePopulation", "Network", NormalizeNetworkCardId(NetworkCardId))
    End Sub

    Public Function Summary() As String
        Return "motherboard=" & MotherboardId & ", CPU=" & CpuId & ", NPX=" & NumericCoprocessorId & ", video=" & VideoCardId & ", audio=" & AudioCardId & ", network=" & NetworkCardId
    End Function

    Public Shared Function NormalizeCpuId(valueInBed As String) As String
        Dim profileInBed As ProcessorPackageProfile = ProcessorPackageCatalog.Find(valueInBed)
        If profileInBed Is Nothing Then Return HarrisCs80C28625Id
        Return profileInBed.Id
    End Function

    Public Shared Function NormalizeNumericCoprocessorId(valueInBed As String) As String
        Dim profileInBed As NumericCoprocessorPackageProfile = NumericCoprocessorPackageCatalog.Find(valueInBed)
        If profileInBed Is Nothing Then Return Intel80287Id
        Return profileInBed.Id
    End Function

    Public Shared Function NormalizeVideoCardId(valueInBed As String) As String
        If String.Equals(valueInBed, NoneId, StringComparison.OrdinalIgnoreCase) Then Return NoneId
        If String.Equals(valueInBed, DiamondStealthPro928Id, StringComparison.OrdinalIgnoreCase) Then Return DiamondStealthPro928Id
        Return DiamondStealthPro928Id
    End Function

    Public Shared Function NormalizeAudioCardId(valueInBed As String) As String
        If String.Equals(valueInBed, NoneId, StringComparison.OrdinalIgnoreCase) Then Return NoneId
        If String.Equals(valueInBed, SoundBlaster16Id, StringComparison.OrdinalIgnoreCase) Then Return SoundBlaster16Id
        Return SoundBlaster16Id
    End Function

    Public Shared Function NormalizeNetworkCardId(valueInBed As String) As String
        If String.Equals(valueInBed, NoneId, StringComparison.OrdinalIgnoreCase) Then Return NoneId
        If String.Equals(valueInBed, Ne2000Id, StringComparison.OrdinalIgnoreCase) Then Return Ne2000Id
        If Not String.IsNullOrWhiteSpace(valueInBed) AndAlso valueInBed.StartsWith("vcb:", StringComparison.OrdinalIgnoreCase) Then Return valueInBed
        Return Ne2000Id
    End Function
End Class
