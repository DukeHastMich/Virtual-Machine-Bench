Imports System
Imports System.Collections.Generic

Public Enum MotherboardExpansionBusKind
    Isa8 = 0
    Isa16 = 1
    VesaLocalBus32 = 2
End Enum

Public NotInheritable Class ProcessorPackageProfile
    Public Sub New(idInBed As String,
                   displayNameInBed As String,
                   architectureInBed As String,
                   executionCoreAvailableInBed As Boolean)
        If String.IsNullOrWhiteSpace(idInBed) Then Throw New ArgumentNullException(NameOf(idInBed))
        If String.IsNullOrWhiteSpace(displayNameInBed) Then Throw New ArgumentNullException(NameOf(displayNameInBed))
        Id = idInBed.Trim()
        DisplayName = displayNameInBed.Trim()
        Architecture = If(architectureInBed, String.Empty).Trim()
        ExecutionCoreAvailable = executionCoreAvailableInBed
    End Sub

    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property Architecture As String
    Public ReadOnly Property ExecutionCoreAvailable As Boolean
End Class

Public NotInheritable Class ProcessorPackageCatalog
    Public Const NoneId As String = "none"
    Public Const HarrisCs80C28625Id As String = "harris-cs80c286-25"
    Public Const AmdN80L28616Id As String = "amd-n80l286-16-s"
    Public Const Intel80486SxId As String = "intel-80486sx"
    Public Const Intel80486DxId As String = "intel-80486dx"
    Public Const Intel80486Dx2Id As String = "intel-80486dx2"
    Public Const Intel80486SlSxId As String = "intel-80486sl-sx"
    Public Const Intel80486SlDxId As String = "intel-80486sl-dx"
    Public Const Intel80486Dx4P24CId As String = "intel-p24c-486dx4"
    Public Const IntelP24TId As String = "intel-p24t"
    Public Const IntelP24DId As String = "intel-p24d"
    Public Const AmdAm486DxId As String = "amd-am486dx"
    Public Const AmdAm486Dx2Id As String = "amd-am486dx2"
    Public Const AmdAm486Dx4Id As String = "amd-am486dx4"
    Public Const CyrixCx486DxId As String = "cyrix-cx486dx"
    Public Const CyrixCx486Dx2M7Id As String = "cyrix-cx486dx2-m7"

    Private Shared ReadOnly _profilesInBed As ProcessorPackageProfile() = {
        New ProcessorPackageProfile(NoneId, "None — empty CPU socket", "none", True),
        New ProcessorPackageProfile(HarrisCs80C28625Id, "Harris Semiconductor CS80C286-25", "80286", True),
        New ProcessorPackageProfile(AmdN80L28616Id, "AMD N80L286-16/S", "80286", False),
        New ProcessorPackageProfile(Intel80486SxId, "Intel 80486SX", "80486", False),
        New ProcessorPackageProfile(Intel80486DxId, "Intel 80486DX", "80486", False),
        New ProcessorPackageProfile(Intel80486Dx2Id, "Intel 80486DX2", "80486", False),
        New ProcessorPackageProfile(Intel80486SlSxId, "Intel SL-80486SX", "80486", False),
        New ProcessorPackageProfile(Intel80486SlDxId, "Intel SL-80486DX", "80486", False),
        New ProcessorPackageProfile(Intel80486Dx4P24CId, "Intel P24C / 80486DX4", "80486", False),
        New ProcessorPackageProfile(IntelP24TId, "Intel P24T OverDrive", "80486/Pentium OverDrive", False),
        New ProcessorPackageProfile(IntelP24DId, "Intel P24D", "80486", False),
        New ProcessorPackageProfile(AmdAm486DxId, "AMD Am486DX", "80486", False),
        New ProcessorPackageProfile(AmdAm486Dx2Id, "AMD Am486DX2", "80486", False),
        New ProcessorPackageProfile(AmdAm486Dx4Id, "AMD Am486DX4", "80486", False),
        New ProcessorPackageProfile(CyrixCx486DxId, "Cyrix Cx486DX", "80486", False),
        New ProcessorPackageProfile(CyrixCx486Dx2M7Id, "Cyrix Cx486DX2 (M7)", "80486", False)
    }

    Private Sub New()
    End Sub

    Public Shared Function Find(idInBed As String) As ProcessorPackageProfile
        If String.IsNullOrWhiteSpace(idInBed) Then Return Nothing
        For Each profileInBed As ProcessorPackageProfile In _profilesInBed
            If String.Equals(profileInBed.Id, idInBed, StringComparison.OrdinalIgnoreCase) Then Return profileInBed
        Next
        Return Nothing
    End Function

    Public Shared Function DisplayName(idInBed As String) As String
        Dim profileInBed As ProcessorPackageProfile = Find(idInBed)
        If profileInBed Is Nothing Then Return "Unknown CPU package — " & If(idInBed, String.Empty)
        Return profileInBed.DisplayName
    End Function

    Public Shared Function ExecutionCoreAvailable(idInBed As String) As Boolean
        Dim profileInBed As ProcessorPackageProfile = Find(idInBed)
        Return profileInBed IsNot Nothing AndAlso profileInBed.ExecutionCoreAvailable
    End Function
End Class

Public NotInheritable Class NumericCoprocessorPackageProfile
    Public Sub New(idInBed As String, displayNameInBed As String, executionCoreAvailableInBed As Boolean)
        Id = idInBed
        DisplayName = displayNameInBed
        ExecutionCoreAvailable = executionCoreAvailableInBed
    End Sub
    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property ExecutionCoreAvailable As Boolean
End Class

Public NotInheritable Class NumericCoprocessorPackageCatalog
    Public Const NoneId As String = "none"
    Public Const Intel80287Id As String = "intel-80287"

    Private Shared ReadOnly _profilesInBed As NumericCoprocessorPackageProfile() = {
        New NumericCoprocessorPackageProfile(NoneId, "None — empty numeric coprocessor socket", True),
        New NumericCoprocessorPackageProfile(Intel80287Id, "Intel 80287", True)
    }

    Private Sub New()
    End Sub

    Public Shared Function Find(idInBed As String) As NumericCoprocessorPackageProfile
        If String.IsNullOrWhiteSpace(idInBed) Then Return Nothing
        For Each profileInBed As NumericCoprocessorPackageProfile In _profilesInBed
            If String.Equals(profileInBed.Id, idInBed, StringComparison.OrdinalIgnoreCase) Then Return profileInBed
        Next
        Return Nothing
    End Function

    Public Shared Function DisplayName(idInBed As String) As String
        Dim profileInBed As NumericCoprocessorPackageProfile = Find(idInBed)
        If profileInBed Is Nothing Then Return "Unknown numeric coprocessor — " & If(idInBed, String.Empty)
        Return profileInBed.DisplayName
    End Function
End Class

Public NotInheritable Class MotherboardClockOption
    Public Sub New(idInBed As String, displayNameInBed As String, baseClockHzInBed As Long)
        Id = idInBed
        DisplayName = displayNameInBed
        BaseClockHz = baseClockHzInBed
    End Sub
    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property BaseClockHz As Long
End Class

Public NotInheritable Class MotherboardExpansionSlotProfile
    Public Sub New(idInBed As String,
                   displayNameInBed As String,
                   busKindInBed As MotherboardExpansionBusKind,
                   busMasterCapableInBed As Boolean)
        Id = idInBed
        DisplayName = displayNameInBed
        BusKind = busKindInBed
        BusMasterCapable = busMasterCapableInBed
    End Sub
    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property BusKind As MotherboardExpansionBusKind
    Public ReadOnly Property BusMasterCapable As Boolean
End Class

' Host-side definition of one physical motherboard model.  The profile owns
' compatibility/topology metadata.  It does not silently instantiate an execution
' core: a real board may be selectable before its matching CPU core is implemented.
Public NotInheritable Class MotherboardProfile
    Private ReadOnly _supportedCpuIdsInBed As String()
    Private ReadOnly _supportedNumericCoprocessorIdsInBed As String()
    Private ReadOnly _supportedMemoryMegabytesInBed As Integer()
    Private ReadOnly _supportedCacheKilobytesInBed As Integer()
    Private ReadOnly _clockOptionsInBed As MotherboardClockOption()
    Private ReadOnly _expansionSlotsInBed As MotherboardExpansionSlotProfile()

    Public Sub New(idInBed As String,
                   displayNameInBed As String,
                   backplaneImplementationIdInBed As String,
                   runtimeBackplaneAvailableInBed As Boolean,
                   cpuSocketDisplayNameInBed As String,
                   chipsetDisplayNameInBed As String,
                   chipsetCpuBusInBed As String,
                   chipsetMemoryInBed As String,
                   chipsetPeripheralInBed As String,
                   chipsetBufferInBed As String,
                   chipsetConfigurationPortsInBed As String,
                   clockGeneratorDisplayNameInBed As String,
                   memoryTopologyDisplayNameInBed As String,
                   cacheTopologyDisplayNameInBed As String,
                   biosDisplayNameInBed As String,
                   defaultCpuIdInBed As String,
                   defaultNumericCoprocessorIdInBed As String,
                   defaultMemoryMegabytesInBed As Integer,
                   defaultCacheKilobytesInBed As Integer,
                   supportedCpuIdsInBed As IEnumerable(Of String),
                   supportedNumericCoprocessorIdsInBed As IEnumerable(Of String),
                   supportedMemoryMegabytesInBed As IEnumerable(Of Integer),
                   supportedCacheKilobytesInBed As IEnumerable(Of Integer),
                   clockOptionsInBed As IEnumerable(Of MotherboardClockOption),
                   expansionSlotsInBed As IEnumerable(Of MotherboardExpansionSlotProfile))
        If String.IsNullOrWhiteSpace(idInBed) Then Throw New ArgumentNullException(NameOf(idInBed))
        If String.IsNullOrWhiteSpace(displayNameInBed) Then Throw New ArgumentNullException(NameOf(displayNameInBed))
        If String.IsNullOrWhiteSpace(backplaneImplementationIdInBed) Then Throw New ArgumentNullException(NameOf(backplaneImplementationIdInBed))
        Id = idInBed.Trim()
        DisplayName = displayNameInBed.Trim()
        BackplaneImplementationId = backplaneImplementationIdInBed.Trim()
        RuntimeBackplaneAvailable = runtimeBackplaneAvailableInBed
        CpuSocketDisplayName = If(cpuSocketDisplayNameInBed, String.Empty).Trim()
        ChipsetDisplayName = If(chipsetDisplayNameInBed, String.Empty).Trim()
        ChipsetCpuBus = If(chipsetCpuBusInBed, String.Empty).Trim()
        ChipsetMemory = If(chipsetMemoryInBed, String.Empty).Trim()
        ChipsetPeripheral = If(chipsetPeripheralInBed, String.Empty).Trim()
        ChipsetBuffer = If(chipsetBufferInBed, String.Empty).Trim()
        ChipsetConfigurationPorts = If(chipsetConfigurationPortsInBed, String.Empty).Trim()
        ClockGeneratorDisplayName = If(clockGeneratorDisplayNameInBed, String.Empty).Trim()
        MemoryTopologyDisplayName = If(memoryTopologyDisplayNameInBed, String.Empty).Trim()
        CacheTopologyDisplayName = If(cacheTopologyDisplayNameInBed, String.Empty).Trim()
        BiosDisplayName = If(biosDisplayNameInBed, String.Empty).Trim()
        DefaultCpuId = defaultCpuIdInBed
        DefaultNumericCoprocessorId = defaultNumericCoprocessorIdInBed
        DefaultMemoryMegabytes = defaultMemoryMegabytesInBed
        DefaultCacheKilobytes = defaultCacheKilobytesInBed
        _supportedCpuIdsInBed = New List(Of String)(supportedCpuIdsInBed).ToArray()
        _supportedNumericCoprocessorIdsInBed = New List(Of String)(supportedNumericCoprocessorIdsInBed).ToArray()
        _supportedMemoryMegabytesInBed = New List(Of Integer)(supportedMemoryMegabytesInBed).ToArray()
        _supportedCacheKilobytesInBed = New List(Of Integer)(supportedCacheKilobytesInBed).ToArray()
        _clockOptionsInBed = New List(Of MotherboardClockOption)(clockOptionsInBed).ToArray()
        _expansionSlotsInBed = New List(Of MotherboardExpansionSlotProfile)(expansionSlotsInBed).ToArray()
    End Sub

    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property BackplaneImplementationId As String
    Public ReadOnly Property RuntimeBackplaneAvailable As Boolean
    Public ReadOnly Property CpuSocketDisplayName As String
    Public ReadOnly Property ChipsetDisplayName As String
    Public ReadOnly Property ChipsetCpuBus As String
    Public ReadOnly Property ChipsetMemory As String
    Public ReadOnly Property ChipsetPeripheral As String
    Public ReadOnly Property ChipsetBuffer As String
    Public ReadOnly Property ChipsetConfigurationPorts As String
    Public ReadOnly Property ClockGeneratorDisplayName As String
    Public ReadOnly Property MemoryTopologyDisplayName As String
    Public ReadOnly Property CacheTopologyDisplayName As String
    Public ReadOnly Property BiosDisplayName As String
    Public ReadOnly Property DefaultCpuId As String
    Public ReadOnly Property DefaultNumericCoprocessorId As String
    Public ReadOnly Property DefaultMemoryMegabytes As Integer
    Public ReadOnly Property DefaultCacheKilobytes As Integer

    Public Function SupportedCpuIds() As String()
        Return DirectCast(_supportedCpuIdsInBed.Clone(), String())
    End Function
    Public Function SupportedNumericCoprocessorIds() As String()
        Return DirectCast(_supportedNumericCoprocessorIdsInBed.Clone(), String())
    End Function
    Public Function SupportedMemoryMegabytes() As Integer()
        Return DirectCast(_supportedMemoryMegabytesInBed.Clone(), Integer())
    End Function
    Public Function SupportedCacheKilobytes() As Integer()
        Return DirectCast(_supportedCacheKilobytesInBed.Clone(), Integer())
    End Function
    Public Function ClockOptions() As MotherboardClockOption()
        Return DirectCast(_clockOptionsInBed.Clone(), MotherboardClockOption())
    End Function
    Public Function ExpansionSlots() As MotherboardExpansionSlotProfile()
        Return DirectCast(_expansionSlotsInBed.Clone(), MotherboardExpansionSlotProfile())
    End Function

    Public Function SupportsCpu(idInBed As String) As Boolean
        Return ContainsIdInBed(_supportedCpuIdsInBed, idInBed)
    End Function
    Public Function SupportsNumericCoprocessor(idInBed As String) As Boolean
        Return ContainsIdInBed(_supportedNumericCoprocessorIdsInBed, idInBed)
    End Function
    Public Function SupportsMemoryMegabytes(megabytesInBed As Integer) As Boolean
        For Each candidateInBed As Integer In _supportedMemoryMegabytesInBed
            If candidateInBed = megabytesInBed Then Return True
        Next
        Return False
    End Function

    Public Function SupportsCacheKilobytes(kilobytesInBed As Integer) As Boolean
        For Each candidateInBed As Integer In _supportedCacheKilobytesInBed
            If candidateInBed = kilobytesInBed Then Return True
        Next
        Return False
    End Function

    Public Function FindClockOption(idInBed As String) As MotherboardClockOption
        If String.IsNullOrWhiteSpace(idInBed) Then Return Nothing
        For Each optionInBed As MotherboardClockOption In _clockOptionsInBed
            If String.Equals(optionInBed.Id, idInBed, StringComparison.OrdinalIgnoreCase) Then Return optionInBed
        Next
        Return Nothing
    End Function

    Public ReadOnly Property DefaultClockOptionId As String
        Get
            If String.Equals(Id, MotherboardCatalog.QdiV4P895P3SmtV5Id, StringComparison.OrdinalIgnoreCase) Then Return "33mhz-base"
            If String.Equals(Id, MotherboardCatalog.CtCs8221NeatAt286Id, StringComparison.OrdinalIgnoreCase) Then Return "25mhz"
            If String.Equals(Id, MotherboardCatalog.OctekFoxII286Rev32Id, StringComparison.OrdinalIgnoreCase) Then Return "16mhz"
            If _clockOptionsInBed.Length = 0 Then Return String.Empty
            Return _clockOptionsInBed(0).Id
        End Get
    End Property

    Public ReadOnly Property FirmwareImageRelativePath As String
        Get
            If String.Equals(Id, MotherboardCatalog.QdiV4P895P3SmtV5Id, StringComparison.OrdinalIgnoreCase) Then Return "Firmware\qdi-v4p895p3-smt-v5.rom"
            If String.Equals(Id, MotherboardCatalog.OctekFoxII286Rev32Id, StringComparison.OrdinalIgnoreCase) Then Return "Firmware\octek-fox-ii-286-rev3-2.rom"
            Return "Firmware\atbios.rom"
        End Get
    End Property

    Public ReadOnly Property CmosNvramRelativePath As String
        Get
            If String.Equals(Id, MotherboardCatalog.QdiV4P895P3SmtV5Id, StringComparison.OrdinalIgnoreCase) Then Return "Firmware\qdi-v4p895p3-smt-v5.cmos.nvram"
            If String.Equals(Id, MotherboardCatalog.OctekFoxII286Rev32Id, StringComparison.OrdinalIgnoreCase) Then Return "Firmware\octek-fox-ii-286-rev3-2.cmos.nvram"
            Return "Firmware\cmos.nvram"
        End Get
    End Property

    Public ReadOnly Property CmosStorageBytes As Integer
        Get
            If String.Equals(Id, MotherboardCatalog.QdiV4P895P3SmtV5Id, StringComparison.OrdinalIgnoreCase) Then Return 256
            If String.Equals(Id, MotherboardCatalog.OctekFoxII286Rev32Id, StringComparison.OrdinalIgnoreCase) Then Return 64
            Return 128
        End Get
    End Property

    Public Function CompatibleIsaSlotCount(minimumConnectorBitsInBed As Integer) As Integer
        Dim countInBed As Integer = 0
        For Each slotInBed As MotherboardExpansionSlotProfile In _expansionSlotsInBed
            Select Case slotInBed.BusKind
                Case MotherboardExpansionBusKind.Isa16
                    If minimumConnectorBitsInBed <= 16 Then countInBed += 1
                Case MotherboardExpansionBusKind.Isa8
                    If minimumConnectorBitsInBed <= 8 Then countInBed += 1
            End Select
        Next
        Return countInBed
    End Function

    Public Function ValidateIsa16CardCount(cardCountInBed As Integer, ByRef reasonInBed As String) As Boolean
        Dim availableInBed As Integer = CompatibleIsaSlotCount(16)
        If cardCountInBed <= availableInBed Then
            reasonInBed = String.Empty
            Return True
        End If
        reasonInBed = DisplayName & " provides " & availableInBed.ToString() &
                      " compatible 16-bit ISA connector(s), but " & cardCountInBed.ToString() &
                      " installed card(s) currently require 16-bit ISA."
        Return False
    End Function

    Private Shared Function ContainsIdInBed(valuesInBed As String(), idInBed As String) As Boolean
        If valuesInBed Is Nothing OrElse String.IsNullOrWhiteSpace(idInBed) Then Return False
        For Each candidateInBed As String In valuesInBed
            If String.Equals(candidateInBed, idInBed, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function
End Class

Public NotInheritable Class MotherboardCatalog
    Public Const CtCs8221NeatAt286Id As String = "ct-cs8221-neat-at-286"
    Public Const CtCs8221NeatBackplaneImplementationId As String = "neat-at-286"
    Public Const QdiV4P895P3SmtV5Id As String = "qdi-v4p895p3-smt-v5"
    Public Const QdiV4P895P3BackplaneImplementationId As String = "qdi-opti895-486"
    Public Const OctekFoxII286Rev32Id As String = "octek-fox-ii-286-rev3-2"
    Public Const OctekFoxII286Rev32BackplaneImplementationId As String = "octek-fox-ii-ht12a-286"

    Private Shared ReadOnly _profilesInBed As MotherboardProfile() = {
        New MotherboardProfile(
            CtCs8221NeatAt286Id,
            "C&T CS8221 NEAT-class AT motherboard",
            CtCs8221NeatBackplaneImplementationId,
            True,
            "PLCC/PGA 80286 processor socket + 80287 processor-extension socket",
            "Chips & Technologies CS8221 NEAT chipset",
            "82C211 CPU / AT-bus controller",
            "82C212 memory / shadow / EMS controller",
            "82C206 integrated peripheral controller — DMA, PIC, PIT, RTC/CMOS support",
            "82C215 data / address buffer role — represented by motherboard decode and routing",
            "NEAT indexed configuration interface — 22h index / 23h data",
            "AT motherboard oscillator / 20-25 MHz CPU timing profile",
            "Late-286 AT RAM-bank profile — 1/2/4/8/12/16 MB",
            "Board/profile cache topology not separately populated",
            "Cromwell AT BIOS — Firmware\atbios.rom",
            ProcessorPackageCatalog.HarrisCs80C28625Id,
            NumericCoprocessorPackageCatalog.Intel80287Id,
            4,
            0,
            {ProcessorPackageCatalog.NoneId, ProcessorPackageCatalog.HarrisCs80C28625Id},
            {NumericCoprocessorPackageCatalog.NoneId, NumericCoprocessorPackageCatalog.Intel80287Id},
            {1, 2, 4, 8, 12, 16},
            {0},
            {New MotherboardClockOption("20mhz", "20 MHz normal / 25 MHz turbo board profile", 20000000L),
             New MotherboardClockOption("25mhz", "25 MHz turbo", 25000000L)},
            {New MotherboardExpansionSlotProfile("isa-video", "ISA video slot", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa-audio", "ISA audio slot", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa-network", "ISA network slot", MotherboardExpansionBusKind.Isa16, False)}),
        New MotherboardProfile(
            OctekFoxII286Rev32Id,
            "Octek FOX II 286 Rev. 3.2 — Headland HT12/A",
            OctekFoxII286Rev32BackplaneImplementationId,
            False,
            "PLCC68 80286 socket + DIP40 80287 socket",
            "Headland Technology HT12/A",
            "HT12/A 80286 local-bus / AT-bus controller",
            "HT12/A DRAM, shadow, relocation and hardware-EMS controller",
            "HT12/A integrated DMA/PIC/PIT/page-register/AT peripheral fabric",
            "Board-visible 74-series glue plus ISA buffering retained as board composition",
            "HT12 index 1EDh / data 1EFh; fast A20/hot reset at 92h",
            "32.000000 MHz oscillator / 16 MHz CPU configuration (14.31818 MHz AT reference also populated)",
            "Two HT12 logical DRAM banks; 1/2/4 MiB runtime populations",
            "No separate external CPU cache on documented Rev. 3.2 target",
            "AMI HT-1X 286 BIOS DH1X-6069-113090-K0 — 11/30/90 — paired 27256 lanes",
            ProcessorPackageCatalog.AmdN80L28616Id,
            NumericCoprocessorPackageCatalog.NoneId,
            4,
            0,
            {ProcessorPackageCatalog.NoneId, ProcessorPackageCatalog.AmdN80L28616Id},
            {NumericCoprocessorPackageCatalog.NoneId, NumericCoprocessorPackageCatalog.Intel80287Id},
            {1, 2, 4},
            {0},
            {New MotherboardClockOption("16mhz", "16 MHz — exact Rev. 3.2 donor configuration", 16000000L),
             New MotherboardClockOption("12mhz", "12 MHz FOX-II family configuration", 12000000L)},
            {New MotherboardExpansionSlotProfile("isa16-1", "16-bit ISA slot 1", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-2", "16-bit ISA slot 2", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-3", "16-bit ISA slot 3", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-4", "16-bit ISA slot 4", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa8-1", "8-bit ISA slot 1", MotherboardExpansionBusKind.Isa8, False),
             New MotherboardExpansionSlotProfile("isa8-2", "8-bit ISA slot 2", MotherboardExpansionBusKind.Isa8, False)}),
        New MotherboardProfile(
            QdiV4P895P3SmtV5Id,
            "QDI V4P895P3/SMT V5.x — OPTi 895 Green PC",
            QdiV4P895P3BackplaneImplementationId,
            False,
            "Socket 3 — 486-class / P24T/P24D upgrade capable",
            "OPTi 82C895 + OPTi 82C602",
            "OPTi 82C895 CPU/local-bus, cache, DRAM and power-management controller",
            "OPTi 82C895 DRAM/shadow/cache controller — up to 64 MB documented on this QDI board",
            "OPTi 82C895 integrated AT peripherals + OPTi 82C602 RTC/buffer support",
            "OPTi 82C602 bus buffer / internal RTC / power-management companion",
            "82C895 main index 22h / data 24h; integrated IPC index 22h / data 23h; RTC at 70h/71h",
            "IMI SC468 clock generator — JP38/JP39 base-clock selection",
            "4×30-pin SIMM bank + two 72-pin SIMM banks (J21/J22), 1 MB to 64 MB",
            "Socketed secondary cache — 64/128/256/512 KB, 486 burst capable",
            "AMI WinBIOS / board-specific 64 KiB system ROM",
            ProcessorPackageCatalog.NoneId,
            NumericCoprocessorPackageCatalog.NoneId,
            16,
            256,
            {ProcessorPackageCatalog.NoneId,
             ProcessorPackageCatalog.Intel80486SxId,
             ProcessorPackageCatalog.Intel80486DxId,
             ProcessorPackageCatalog.Intel80486Dx2Id,
             ProcessorPackageCatalog.Intel80486SlSxId,
             ProcessorPackageCatalog.Intel80486SlDxId,
             ProcessorPackageCatalog.Intel80486Dx4P24CId,
             ProcessorPackageCatalog.IntelP24TId,
             ProcessorPackageCatalog.IntelP24DId,
             ProcessorPackageCatalog.AmdAm486DxId,
             ProcessorPackageCatalog.AmdAm486Dx2Id,
             ProcessorPackageCatalog.AmdAm486Dx4Id,
             ProcessorPackageCatalog.CyrixCx486DxId,
             ProcessorPackageCatalog.CyrixCx486Dx2M7Id},
            {NumericCoprocessorPackageCatalog.NoneId},
            {1, 2, 4, 6, 8, 10, 12, 16, 20, 32, 64},
            {64, 128, 256, 512},
            {New MotherboardClockOption("25mhz-base", "25 MHz base clock — JP38 OFF / JP39 ON", 25000000L),
             New MotherboardClockOption("33mhz-base", "33 MHz base clock — JP38 ON / JP39 OFF (manual default)", 33000000L),
             New MotherboardClockOption("40mhz-base", "40 MHz base clock — JP38 OFF / JP39 OFF", 40000000L),
             New MotherboardClockOption("50mhz-base", "50 MHz base clock — JP38 ON / JP39 ON", 50000000L)},
            {New MotherboardExpansionSlotProfile("vlb-1", "VESA Local Bus slot 1", MotherboardExpansionBusKind.VesaLocalBus32, True),
             New MotherboardExpansionSlotProfile("vlb-2", "VESA Local Bus slot 2", MotherboardExpansionBusKind.VesaLocalBus32, True),
             New MotherboardExpansionSlotProfile("vlb-3", "VESA Local Bus slot 3", MotherboardExpansionBusKind.VesaLocalBus32, False),
             New MotherboardExpansionSlotProfile("isa16-1", "16-bit ISA slot 1", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-2", "16-bit ISA slot 2", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-3", "16-bit ISA slot 3", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-4", "16-bit ISA slot 4", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-5", "16-bit ISA slot 5", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa16-6", "16-bit ISA slot 6", MotherboardExpansionBusKind.Isa16, False),
             New MotherboardExpansionSlotProfile("isa8-1", "8-bit ISA slot", MotherboardExpansionBusKind.Isa8, False)})
    }

    Private Sub New()
    End Sub

    Public Shared Function DefaultProfile() As MotherboardProfile
        Return _profilesInBed(0)
    End Function

    Public Shared Function Find(idInBed As String) As MotherboardProfile
        If Not String.IsNullOrWhiteSpace(idInBed) Then
            For Each profileInBed As MotherboardProfile In _profilesInBed
                If String.Equals(profileInBed.Id, idInBed, StringComparison.OrdinalIgnoreCase) Then Return profileInBed
            Next
        End If
        Return Nothing
    End Function

    Public Shared Function Require(idInBed As String) As MotherboardProfile
        Dim profileInBed As MotherboardProfile = Find(idInBed)
        If profileInBed Is Nothing Then Throw New InvalidOperationException("Unknown motherboard profile '" & If(idInBed, String.Empty) & "'.")
        Return profileInBed
    End Function

    Public Shared Function NormalizeId(idInBed As String) As String
        Dim profileInBed As MotherboardProfile = Find(idInBed)
        If profileInBed Is Nothing Then Return DefaultProfile().Id
        Return profileInBed.Id
    End Function

    Public Shared Function NormalizeCpuIdForBoard(motherboardIdInBed As String, cpuIdInBed As String) As String
        Dim profileInBed As MotherboardProfile = Require(NormalizeId(motherboardIdInBed))
        If profileInBed.SupportsCpu(cpuIdInBed) Then Return ProcessorPackageCatalog.Find(cpuIdInBed).Id
        Return profileInBed.DefaultCpuId
    End Function

    Public Shared Function NormalizeNumericCoprocessorIdForBoard(motherboardIdInBed As String, npxIdInBed As String) As String
        Dim profileInBed As MotherboardProfile = Require(NormalizeId(motherboardIdInBed))
        If profileInBed.SupportsNumericCoprocessor(npxIdInBed) Then Return NumericCoprocessorPackageCatalog.Find(npxIdInBed).Id
        Return profileInBed.DefaultNumericCoprocessorId
    End Function

    Public Shared Function Profiles() As MotherboardProfile()
        Return DirectCast(_profilesInBed.Clone(), MotherboardProfile())
    End Function
End Class
