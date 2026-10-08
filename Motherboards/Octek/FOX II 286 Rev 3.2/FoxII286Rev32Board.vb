Imports System

' Board-local composition for the photographed Octek FOX II 286 Rev. 3.2 donor.
' Chipset behavior stays in Hardware/Chipsets/Headland/HT12.  This class owns
' board population, reset straps, and eventual net wiring only.
Public NotInheritable Class FoxII286Rev32Board
    Public Const BoardId As String = "octek-fox-ii-286-rev3-2"
    Public Const RuntimeImplementationId As String = "octek-fox-ii-ht12a-286"

    Private ReadOnly _strapsInBed As Ht12PowerOnStraps
    Private ReadOnly _ht12InBed As HeadlandHt12

    Public Sub New(installedMemoryMegabytesInBed As Integer)
        Me.New(CreateHt12Straps(installedMemoryMegabytesInBed), Nothing)
    End Sub

    ' Board Script may already have constructed the exact HT12 silicon worker from
    ' sourced strap declarations.  Accept that worker directly so the physical
    ' description and executable board graph do not fork into duplicate chipset state.
    Public Sub New(strapsInBed As Ht12PowerOnStraps, ht12InBed As HeadlandHt12)
        If strapsInBed Is Nothing Then Throw New ArgumentNullException(NameOf(strapsInBed))
        Dim installedMemoryMegabytesInBed As Integer = InstalledMemoryMegabytesFromHt12RamConfigurationCode(strapsInBed.RamConfigurationCode)

        _strapsInBed = New Ht12PowerOnStraps With {
            .RamConfigurationCode = strapsInBed.RamConfigurationCode,
            .AtBusLowSpeed = strapsInBed.AtBusLowSpeed,
            .ZeroWaitMainMemory = strapsInBed.ZeroWaitMainMemory,
            .ExtraWaitStateMode = strapsInBed.ExtraWaitStateMode,
            .TcStrapHigh = strapsInBed.TcStrapHigh
        }
        _ht12InBed = If(ht12InBed Is Nothing, New HeadlandHt12(_strapsInBed), ht12InBed)

        If _ht12InBed.Configuration.ReadRegister(&H10) <> _strapsInBed.SystemConfigurationValue() Then
            Throw New InvalidOperationException("The supplied HT12 worker reset straps do not match the FOX board definition.")
        End If
        If installedMemoryMegabytesInBed <> InstalledMemoryMegabytes Then
            Throw New InvalidOperationException("FOX HT12 RAM strap decoding is internally inconsistent.")
        End If
    End Sub

    Public ReadOnly Property Chipset As HeadlandHt12
        Get
            Return _ht12InBed
        End Get
    End Property

    Public ReadOnly Property Ht12Straps As Ht12PowerOnStraps
        Get
            Return _strapsInBed
        End Get
    End Property

    Public ReadOnly Property InstalledMemoryMegabytes As Integer
        Get
            Return InstalledMemoryMegabytesFromHt12RamConfigurationCode(_strapsInBed.RamConfigurationCode)
        End Get
    End Property

    Public Shared Function InstalledMemoryMegabytesFromHt12RamConfigurationCode(ramConfigurationCodeInBed As Byte) As Integer
        Select Case ramConfigurationCodeInBed And &H7
            Case 3 : Return 1
            Case 5 : Return 2
            Case 6 : Return 4
            Case Else
                Throw New InvalidOperationException("Current FOX runtime supports HT12 RAM strap codes 3, 5 and 6 only.")
        End Select
    End Function

    Public Shared Function CreateHt12Straps(installedMemoryMegabytesInBed As Integer) As Ht12PowerOnStraps
        Dim ramCodeInBed As Byte
        Select Case installedMemoryMegabytesInBed
            Case 1
                ramCodeInBed = 3       ' 256K-class bank0 + 256K-class bank1
            Case 2
                ramCodeInBed = 5       ' 1M-class bank0, bank1 absent
            Case 4
                ramCodeInBed = 6       ' 1M-class bank0 + 1M-class bank1
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(installedMemoryMegabytesInBed),
                                                      "Current FOX Rev. 3.2 executable population model supports 1, 2 or 4 MiB.")
        End Select

        Return New Ht12PowerOnStraps With {
            .RamConfigurationCode = ramCodeInBed,
            .AtBusLowSpeed = False,
            .ZeroWaitMainMemory = True,
            .ExtraWaitStateMode = 3,
            .TcStrapHigh = False
        }
    End Function

    Public Function DiagnosticText() As String
        Return "Octek FOX II 286 Rev. 3.2" & Environment.NewLine &
               "  main chipset             : " & _ht12InBed.Identity & Environment.NewLine &
               "  CPU specimen             : AMD N80L286-16/S (PLCC68)" & Environment.NewLine &
               "  RTC specimen             : Hualon HM6818P" & Environment.NewLine &
               "  BIOS                     : AMI HT-1X 286, 11/30/90, paired 27256 lanes" & Environment.NewLine &
               "  slots                    : 4x ISA16 + 2x ISA8" & Environment.NewLine &
               "  HT12 RAM strap code      : " & (_strapsInBed.RamConfigurationCode And &H7).ToString() & Environment.NewLine &
               _ht12InBed.DiagnosticText()
    End Function
End Class

' Runtime registration exists so the board is a first-class construction target.
' The FOX fixed fabric is real and NEAT-free, but live-machine activation remains
' deliberately gated until an 80286 execution core is bound to this local bus.
' Selecting the board must never silently leave NEAT silicon underneath it.
Public NotInheritable Class FoxII286Rev32RuntimeBackplane
    Implements IMotherboardRuntimeBackplane

    Private ReadOnly _boardInBed As FoxII286Rev32Board
    Private ReadOnly _fixedFabricInBed As FoxII286Rev32FixedFabric

    Public Sub New()
        _boardInBed = New FoxII286Rev32Board(4)
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(FoxII286Rev32Board.BoardId)
        Dim cmosPathInBed As String = IO.Path.Combine(AppContext.BaseDirectory, profileInBed.CmosNvramRelativePath)
        _fixedFabricInBed = New FoxII286Rev32FixedFabric(_boardInBed, cmosPathInBed)
    End Sub

    Public ReadOnly Property Board As FoxII286Rev32Board
        Get
            Return _boardInBed
        End Get
    End Property

    Public ReadOnly Property FixedFabric As FoxII286Rev32FixedFabric
        Get
            Return _fixedFabricInBed
        End Get
    End Property

    Public ReadOnly Property ProfileId As String Implements IMotherboardRuntimeBackplane.ProfileId
        Get
            Return FoxII286Rev32Board.BoardId
        End Get
    End Property

    Public ReadOnly Property ImplementationId As String Implements IMotherboardRuntimeBackplane.ImplementationId
        Get
            Return FoxII286Rev32Board.RuntimeImplementationId
        End Get
    End Property

    Public ReadOnly Property RuntimeDisplayName As String Implements IMotherboardRuntimeBackplane.RuntimeDisplayName
        Get
            Return "Octek FOX II 286 Rev. 3.2 / Headland HT12/A"
        End Get
    End Property

    Public ReadOnly Property FixedFabricAvailable As Boolean Implements IMotherboardRuntimeBackplane.FixedFabricAvailable
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property ProcessorExecutionAvailable As Boolean Implements IMotherboardRuntimeBackplane.ProcessorExecutionAvailable
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property ProcessorArchitecture As String Implements IMotherboardRuntimeBackplane.ProcessorArchitecture
        Get
            Return "80286"
        End Get
    End Property

    Public Sub InitializeFixedFabric() Implements IMotherboardRuntimeBackplane.InitializeFixedFabric
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(ProfileId)
        Dim romPathInBed As String = IO.Path.Combine(AppContext.BaseDirectory, profileInBed.FirmwareImageRelativePath)
        If Not IO.File.Exists(romPathInBed) Then
            Throw New IO.FileNotFoundException("The matched FOX II OEM BIOS is required for this motherboard runtime.", romPathInBed)
        End If
        _fixedFabricInBed.LoadLinearSystemRom(IO.File.ReadAllBytes(romPathInBed))
        _fixedFabricInBed.Initialize()
    End Sub

    Public Function SupportsProcessorPackage(cpuIdInBed As String) As Boolean Implements IMotherboardRuntimeBackplane.SupportsProcessorPackage
        Return String.Equals(cpuIdInBed, ProcessorPackageCatalog.NoneId, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Function DiagnosticText() As String Implements IMotherboardRuntimeBackplane.DiagnosticText
        Return _boardInBed.DiagnosticText() & Environment.NewLine &
               "  fixed motherboard fabric : available (own HT12/AT graph; no NEAT backend)" & Environment.NewLine &
               "  processor execution       : gated pending neutral 80286 binding" & Environment.NewLine &
               _fixedFabricInBed.DiagnosticText()
    End Function
End Class

' Executable fixed-fabric graph for the FOX II / HT12 board.  HT12-integrated AT
' functions use the project's reusable electrical workers; they are subworkers of
' the chipset fabric, not claims that the photographed board contains discrete
' 8259/8237/8254 packages.
Public NotInheritable Class FoxII286Rev32FixedFabric
    Private ReadOnly _boardInBed As FoxII286Rev32Board
    Private ReadOnly _systemBusInBed As HardwareBus
    Private ReadOnly _memoryInBed As Ht12MemoryWorker286
    Private ReadOnly _localBusInBed As Ht12ProcessorLocalBusFabric
    Private ReadOnly _masterPicInBed As Pic8259
    Private ReadOnly _slavePicInBed As Pic8259
    Private ReadOnly _dmaInBed As Dma8237
    Private ReadOnly _pitInBed As Pit8253
    Private ReadOnly _keyboardInBed As AtKeyboard101
    Private ReadOnly _keyboardControllerInBed As KeyboardController8042
    Private ReadOnly _rtcInBed As Hm6818Rtc
    Private ReadOnly _systemControlInBed As AtSystemControlPorts
    Private ReadOnly _nmiGateInBed As AtNmiGate
    Private ReadOnly _isaMemoryHoleInBed As AtIsaMemoryHole
    Private ReadOnly _isaBackplaneInBed As IsaBackplane286
    Private _initializedInBed As Boolean

    Public Event InterruptRequestChanged(assertedInBed As Boolean)
    Public Event NmiRequested()
    Public Event AlternateCpuResetRequested(minimumDelayPicosecondsInBed As Long,
                                             operationPicosecondsInBed As Long)
    Public Event ProcessorHoldRequestChanged(assertedInBed As Boolean)
    Public Event RefreshRequestLatched(pendingCountInBed As ULong)

    Public Sub New(boardInBed As FoxII286Rev32Board,
                   Optional cmosPersistencePathInBed As String = Nothing)
        If boardInBed Is Nothing Then Throw New ArgumentNullException(NameOf(boardInBed))
        _boardInBed = boardInBed
        _systemBusInBed = New HardwareBus()
        _memoryInBed = New Ht12MemoryWorker286(boardInBed.Chipset, boardInBed.InstalledMemoryMegabytes)
        _masterPicInBed = New Pic8259()
        _slavePicInBed = New Pic8259(&HA0US, &HA1US, &H70)
        _dmaInBed = New Dma8237(
            Function(channelInBed As Integer, addressInBed As UInteger) As Byte
                Return _memoryInBed.ReadDmaByte(addressInBed)
            End Function,
            Sub(channelInBed As Integer, addressInBed As UInteger, valueInBed As Byte)
                _memoryInBed.WriteDmaByte(addressInBed, valueInBed)
            End Sub,
            Function(channelInBed As Integer, addressInBed As UInteger) As UInt16
                Return _memoryInBed.ReadDmaWord(addressInBed)
            End Function,
            Sub(channelInBed As Integer, addressInBed As UInteger, valueInBed As UInt16)
                _memoryInBed.WriteDmaWord(addressInBed, valueInBed)
            End Sub)
        _pitInBed = New Pit8253(_masterPicInBed)
        _keyboardInBed = New AtKeyboard101()
        _keyboardControllerInBed = New KeyboardController8042(_masterPicInBed, _keyboardInBed)
        _rtcInBed = New Hm6818Rtc(_slavePicInBed, cmosPersistencePathInBed)
        _systemControlInBed = New AtSystemControlPorts(_pitInBed)
        _nmiGateInBed = New AtNmiGate()
        _isaMemoryHoleInBed = New AtIsaMemoryHole()
        _isaBackplaneInBed = New IsaBackplane286(_systemBusInBed, _isaMemoryHoleInBed)
        _localBusInBed = New Ht12ProcessorLocalBusFabric(_systemBusInBed, _memoryInBed, 16000000L)
    End Sub

    Public ReadOnly Property SystemBus As HardwareBus
        Get
            Return _systemBusInBed
        End Get
    End Property

    Public ReadOnly Property Memory As Ht12MemoryWorker286
        Get
            Return _memoryInBed
        End Get
    End Property

    Public ReadOnly Property ProcessorLocalBus As IProcessorLocalBusFabric
        Get
            Return _localBusInBed
        End Get
    End Property

    Public ReadOnly Property MasterPic As Pic8259
        Get
            Return _masterPicInBed
        End Get
    End Property

    Public ReadOnly Property SlavePic As Pic8259
        Get
            Return _slavePicInBed
        End Get
    End Property

    Public ReadOnly Property RealTimeClock As Hm6818Rtc
        Get
            Return _rtcInBed
        End Get
    End Property

    Public ReadOnly Property IsaBackplane As IsaBackplane286
        Get
            Return _isaBackplaneInBed
        End Get
    End Property

    Public ReadOnly Property IsInitialized As Boolean
        Get
            Return _initializedInBed
        End Get
    End Property

    Public Sub Initialize()
        If _initializedInBed Then Return

        ' Decode priority: real chipset/local devices first, unclaimed C-E ISA
        ' memory aperture last.  System ROM/shadow are owned by Ht12MemoryWorker286
        ' before HardwareBus is consulted for a memory cycle.
        _systemBusInBed.Register(_masterPicInBed)
        _systemBusInBed.Register(_slavePicInBed)
        _systemBusInBed.Register(_dmaInBed)
        _systemBusInBed.Register(_pitInBed)
        _systemBusInBed.Register(_systemControlInBed)
        _systemBusInBed.Register(_keyboardControllerInBed)
        _systemBusInBed.Register(_rtcInBed)
        _systemBusInBed.Register(_boardInBed.Chipset)
        _systemBusInBed.Register(_isaMemoryHoleInBed)
        _systemBusInBed.Register(_nmiGateInBed)

        ' Physical AT cascade: slave INT -> master IRQ2.
        AddHandler _slavePicInBed.InterruptOutputChanged,
            Sub(assertedInBed As Boolean) _masterPicInBed.SetIrqLine(2, assertedInBed)
        AddHandler _masterPicInBed.InterruptOutputChanged,
            Sub(assertedInBed As Boolean) RaiseEvent InterruptRequestChanged(assertedInBed)

        ' HRQ and timer-1 refresh remain real bus-ownership requests.  Until the
        ' neutral 80286 binding is attached, they are latched/published by the
        ' HT12 local-bus worker and are never silently granted around a CPU.
        AddHandler _dmaInBed.HoldRequestChanged,
            Sub(masterInBed As AtBusMaster286, assertedInBed As Boolean)
                _localBusInBed.SetDmaHoldRequest(masterInBed, assertedInBed)
            End Sub
        AddHandler _pitInBed.RefreshRequest,
            Sub() _localBusInBed.LatchRefreshRequest()
        AddHandler _localBusInBed.HoldRequestChanged,
            Sub(assertedInBed As Boolean) RaiseEvent ProcessorHoldRequestChanged(assertedInBed)
        AddHandler _localBusInBed.RefreshRequestLatched,
            Sub(pendingCountInBed As ULong) RaiseEvent RefreshRequestLatched(pendingCountInBed)

        ' A20 can be forced to propagate by either the 8042 output port or the
        ' HT12's PS/2-compatible fast A20 source at port 92h.
        AddHandler _keyboardControllerInBed.A20Changed,
            Sub(enabledInBed As Boolean) _memoryInBed.SetKeyboardA20Gate(enabledInBed)
        AddHandler _boardInBed.Chipset.FastA20GateChanged,
            Sub(enabledInBed As Boolean) _memoryInBed.SetFastA20Gate(enabledInBed)

        ' Both reset sources are board-level signals.  The 8042 path has no HT12
        ' port-92 delay; the alternate path publishes the datasheet timing so the
        ' future CPU binding can schedule RESET# on the machine timeline.
        AddHandler _keyboardControllerInBed.ResetRequested,
            Sub() RaiseEvent AlternateCpuResetRequested(0L, 0L)
        AddHandler _boardInBed.Chipset.AlternateCpuResetRequested,
            Sub() RaiseEvent AlternateCpuResetRequested(Ht12Port92Worker.HotResetMinimumDelayPicoseconds,
                                                        Ht12Port92Worker.HotResetOperationPicoseconds)

        AddHandler _systemControlInBed.NmiLineChanged,
            Sub(assertedInBed As Boolean) _nmiGateInBed.SetSource(assertedInBed)
        AddHandler _rtcInBed.NmiMaskChanged,
            Sub(disabledInBed As Boolean) _nmiGateInBed.SetMasked(disabledInBed)
        AddHandler _nmiGateInBed.NmiEdge,
            Sub() RaiseEvent NmiRequested()
        _nmiGateInBed.SetMasked(_rtcInBed.NmiDisabled)

        DefineIsaSlotsInBed()
        _initializedInBed = True
    End Sub

    Public Function AcknowledgeHardwareInterrupt() As Integer
        Dim masterVectorInBed As Integer = _masterPicInBed.Acknowledge()
        Dim masterIrqInBed As Integer = masterVectorInBed And 7
        If Not _masterPicInBed.IsProgrammedCascadeInput(masterIrqInBed) Then Return masterVectorInBed
        Return _slavePicInBed.Acknowledge()
    End Function

    Public Sub LoadLinearSystemRom(dataInBed As Byte())
        _memoryInBed.LoadSystemRom(dataInBed)
    End Sub

    Public Sub LoadSystemRomLanes(evenLaneInBed As Byte(), oddLaneInBed As Byte())
        _memoryInBed.LoadSystemRomLanes(evenLaneInBed, oddLaneInBed)
    End Sub

    Public Function DiagnosticText() As String
        Return "FOX II fixed fabric         : " & If(_initializedInBed, "constructed", "not initialized") & Environment.NewLine &
               "  processor execution      : not attached — Processor286 still consumes NEAT-specific bus type" & Environment.NewLine &
               "  HOLD request             : " & If(_localBusInBed.HoldRequestAsserted, "asserted", "clear") & Environment.NewLine &
               "  pending refresh requests : " & _localBusInBed.PendingRefreshRequests.ToString() & Environment.NewLine &
               "  ISA slots                : 4x16-bit + 2x8-bit, empty at construction" & Environment.NewLine &
               _memoryInBed.DiagnosticText()
    End Function

    Private Sub DefineIsaSlotsInBed()
        _isaBackplaneInBed.DefineSlot("isa16-1", "ISA 16-bit slot 1", IsaSlotConnectorWidth286.SixteenBit)
        _isaBackplaneInBed.DefineSlot("isa16-2", "ISA 16-bit slot 2", IsaSlotConnectorWidth286.SixteenBit)
        _isaBackplaneInBed.DefineSlot("isa16-3", "ISA 16-bit slot 3", IsaSlotConnectorWidth286.SixteenBit)
        _isaBackplaneInBed.DefineSlot("isa16-4", "ISA 16-bit slot 4", IsaSlotConnectorWidth286.SixteenBit)
        _isaBackplaneInBed.DefineSlot("isa8-1", "ISA 8-bit slot 1", IsaSlotConnectorWidth286.EightBit)
        _isaBackplaneInBed.DefineSlot("isa8-2", "ISA 8-bit slot 2", IsaSlotConnectorWidth286.EightBit)
    End Sub

End Class
