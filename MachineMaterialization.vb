Imports System
Imports System.Collections.Generic
Imports System.IO

' Immutable host-side blueprint for one physical VMB machine.
'
' This is deliberately NOT guest state.  It captures what the configurator says
' is physically installed on the bench so that Form1 can eventually stop owning
' the running machine.  Runtime objects are created from this definition by
' MachineMaterializer; CMOS/BIOS/device behavior remains owned by the emulated
' hardware after materialization.
Public NotInheritable Class MachineDefinition
    Private ReadOnly _baseDirectoryInBed As String
    Private ReadOnly _hardwarePopulationInBed As MachineHardwarePopulationConfiguration
    Private ReadOnly _ideHardwareInBed As IdeHardwareConfiguration
    Private ReadOnly _soundBlaster16InBed As SoundBlaster16JumperSettings
    Private ReadOnly _ne2000InBed As Ne2000JumperSettings

    Public Sub New(baseDirectoryInBed As String,
                   hardwarePopulationInBed As MachineHardwarePopulationConfiguration,
                   memoryMegabytesInBed As Integer,
                   cacheKilobytesInBed As Integer,
                   clockOptionIdInBed As String,
                   ideHardwareInBed As IdeHardwareConfiguration,
                   soundBlaster16InBed As SoundBlaster16JumperSettings,
                   ne2000InBed As Ne2000JumperSettings)
        If String.IsNullOrWhiteSpace(baseDirectoryInBed) Then Throw New ArgumentNullException(NameOf(baseDirectoryInBed))
        If hardwarePopulationInBed Is Nothing Then Throw New ArgumentNullException(NameOf(hardwarePopulationInBed))
        If ideHardwareInBed Is Nothing Then Throw New ArgumentNullException(NameOf(ideHardwareInBed))
        If soundBlaster16InBed Is Nothing Then Throw New ArgumentNullException(NameOf(soundBlaster16InBed))
        If ne2000InBed Is Nothing Then Throw New ArgumentNullException(NameOf(ne2000InBed))

        _baseDirectoryInBed = Path.GetFullPath(baseDirectoryInBed)
        _hardwarePopulationInBed = hardwarePopulationInBed.CloneConfiguration()
        _ideHardwareInBed = ideHardwareInBed.CloneConfiguration()
        _soundBlaster16InBed = soundBlaster16InBed.CloneSettings()
        _ne2000InBed = ne2000InBed.CloneSettings()

        MemoryMegabytes = memoryMegabytesInBed
        CacheKilobytes = cacheKilobytesInBed
        ClockOptionId = If(clockOptionIdInBed, String.Empty).Trim()
    End Sub

    Public ReadOnly Property BaseDirectory As String
        Get
            Return _baseDirectoryInBed
        End Get
    End Property

    Public ReadOnly Property MotherboardId As String
        Get
            Return _hardwarePopulationInBed.MotherboardId
        End Get
    End Property

    Public ReadOnly Property CpuId As String
        Get
            Return _hardwarePopulationInBed.CpuId
        End Get
    End Property

    Public ReadOnly Property NumericCoprocessorId As String
        Get
            Return _hardwarePopulationInBed.NumericCoprocessorId
        End Get
    End Property

    Public ReadOnly Property VideoCardId As String
        Get
            Return _hardwarePopulationInBed.VideoCardId
        End Get
    End Property

    Public ReadOnly Property AudioCardId As String
        Get
            Return _hardwarePopulationInBed.AudioCardId
        End Get
    End Property

    Public ReadOnly Property NetworkCardId As String
        Get
            Return _hardwarePopulationInBed.NetworkCardId
        End Get
    End Property

    Public ReadOnly Property MemoryMegabytes As Integer
    Public ReadOnly Property CacheKilobytes As Integer
    Public ReadOnly Property ClockOptionId As String

    Public ReadOnly Property HardwarePopulation As MachineHardwarePopulationConfiguration
        Get
            Return _hardwarePopulationInBed.CloneConfiguration()
        End Get
    End Property

    Public ReadOnly Property IdeHardware As IdeHardwareConfiguration
        Get
            Return _ideHardwareInBed.CloneConfiguration()
        End Get
    End Property

    Public ReadOnly Property SoundBlaster16Jumpers As SoundBlaster16JumperSettings
        Get
            Return _soundBlaster16InBed.CloneSettings()
        End Get
    End Property

    Public ReadOnly Property Ne2000Jumpers As Ne2000JumperSettings
        Get
            Return _ne2000InBed.CloneSettings()
        End Get
    End Property

    ' Capture the currently selected chassis population plus the motherboard's
    ' own saved jumper/SIMM/cache context.  RAM/IDE are read without applying
    ' them; materialization must remain a pure blueprint step until validation is
    ' complete.
    Public Shared Function CaptureConfiguredMachine(baseDirectoryInBed As String,
                                                    hardwarePopulationInBed As MachineHardwarePopulationConfiguration,
                                                    isaConfigurationInBed As IsaExpansionCardConfiguration) As MachineDefinition
        If hardwarePopulationInBed Is Nothing Then Throw New ArgumentNullException(NameOf(hardwarePopulationInBed))
        If isaConfigurationInBed Is Nothing Then Throw New ArgumentNullException(NameOf(isaConfigurationInBed))

        Dim populationInBed As MachineHardwarePopulationConfiguration = hardwarePopulationInBed.CloneConfiguration()
        populationInBed.MotherboardId = MotherboardCatalog.NormalizeId(populationInBed.MotherboardId)
        populationInBed.CpuId = MotherboardCatalog.NormalizeCpuIdForBoard(populationInBed.MotherboardId, populationInBed.CpuId)
        populationInBed.NumericCoprocessorId = MotherboardCatalog.NormalizeNumericCoprocessorIdForBoard(populationInBed.MotherboardId, populationInBed.NumericCoprocessorId)
        populationInBed.VideoCardId = MachineHardwarePopulationConfiguration.NormalizeVideoCardId(populationInBed.VideoCardId)
        populationInBed.AudioCardId = MachineHardwarePopulationConfiguration.NormalizeAudioCardId(populationInBed.AudioCardId)
        populationInBed.NetworkCardId = MachineHardwarePopulationConfiguration.NormalizeNetworkCardId(populationInBed.NetworkCardId)

        Dim boardContextInBed As New MotherboardContextConfiguration(baseDirectoryInBed, populationInBed.MotherboardId)
        boardContextInBed.LoadConfiguration()

        Dim ideInBed As New IdeHardwareConfiguration(baseDirectoryInBed)
        ideInBed.LoadConfiguration()

        Dim sbInBed As SoundBlaster16JumperSettings = If(isaConfigurationInBed.SoundBlaster16, New SoundBlaster16JumperSettings())
        Dim neInBed As Ne2000JumperSettings = If(isaConfigurationInBed.Ne2000, New Ne2000JumperSettings())

        Return New MachineDefinition(baseDirectoryInBed,
                                     populationInBed,
                                     boardContextInBed.MemoryMegabytes,
                                     boardContextInBed.CacheKilobytes,
                                     boardContextInBed.ClockOptionId,
                                     ideInBed,
                                     sbInBed,
                                     neInBed)
    End Function

    Public Function ValidateConfiguration() As List(Of String)
        Dim errorsInBed As New List(Of String)()
        Dim profileInBed As MotherboardProfile = Nothing

        Try
            profileInBed = MotherboardCatalog.Require(MotherboardId)
        Catch ex As Exception
            errorsInBed.Add("Unknown motherboard profile '" & MotherboardId & "'.")
            Return errorsInBed
        End Try

        If Not profileInBed.SupportsMemoryMegabytes(MemoryMegabytes) Then
            errorsInBed.Add(MemoryMegabytes.ToString() & " MB is not a legal memory population for " & profileInBed.DisplayName & ".")
        End If
        If Not profileInBed.SupportsCacheKilobytes(CacheKilobytes) Then
            errorsInBed.Add(CacheKilobytes.ToString() & " KB cache is not a legal population for " & profileInBed.DisplayName & ".")
        End If
        If profileInBed.FindClockOption(ClockOptionId) Is Nothing Then
            errorsInBed.Add("Clock option '" & ClockOptionId & "' is not valid for " & profileInBed.DisplayName & ".")
        End If
        If Not profileInBed.SupportsCpu(CpuId) Then
            errorsInBed.Add("CPU package '" & CpuId & "' does not fit " & profileInBed.DisplayName & ".")
        End If
        If Not profileInBed.SupportsNumericCoprocessor(NumericCoprocessorId) Then
            errorsInBed.Add("Numeric coprocessor package '" & NumericCoprocessorId & "' does not fit " & profileInBed.DisplayName & ".")
        End If

        Dim fitReasonInBed As String = String.Empty
        If Not profileInBed.ValidateIsa16CardCount(InstalledIsa16CardCountInBed(), fitReasonInBed) Then
            errorsInBed.Add(fitReasonInBed)
        End If

        Dim soundInstalledInBed As Boolean = Not String.Equals(AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase)
        Dim networkInstalledInBed As Boolean = Not String.Equals(NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase)
        Dim npxInstalledInBed As Boolean = Not String.Equals(NumericCoprocessorId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase)
        errorsInBed.AddRange(IsaResourceConflictDetector.Validate(_soundBlaster16InBed,
                                                                  _ne2000InBed,
                                                                  soundInstalledInBed,
                                                                  networkInstalledInBed,
                                                                  npxInstalledInBed))
        Return errorsInBed
    End Function

    Public Sub ValidateOrThrow()
        Dim errorsInBed As List(Of String) = ValidateConfiguration()
        If errorsInBed.Count = 0 Then Return
        Throw New InvalidOperationException("Machine definition is not materializable:" & Environment.NewLine &
                                            String.Join(Environment.NewLine, errorsInBed))
    End Sub

    Public Function Summary() As String
        Return "board=" & MotherboardId &
               ", CPU=" & CpuId &
               ", NPX=" & NumericCoprocessorId &
               ", RAM=" & MemoryMegabytes.ToString() & " MB" &
               ", cache=" & CacheKilobytes.ToString() & " KB" &
               ", clock=" & ClockOptionId &
               ", video=" & VideoCardId &
               ", audio=" & AudioCardId &
               ", network=" & NetworkCardId &
               ", IDE[" & _ideHardwareInBed.Summary() & "]"
    End Function

    Private Function InstalledIsa16CardCountInBed() As Integer
        Dim countInBed As Integer = 0
        If Not String.Equals(VideoCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then countInBed += 1
        If Not String.Equals(AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then countInBed += 1
        If Not String.Equals(NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then countInBed += 1
        Return countInBed
    End Function
End Class

' A materialized machine owns the runtime objects, installed removable hardware
' population, and chassis lifecycle. Form1 is a configurator/presentation host; it
' does not construct the processor, ISA cards, execution worker, or power/reset tree.
Public NotInheritable Class MaterializedMachine
    Implements IDisposable

    Private ReadOnly _buildPlanInBed As MachineBuildPlan
    Private ReadOnly _definitionInBed As MachineDefinition
    Private ReadOnly _motherboardRuntimeInBed As MotherboardRuntimeManager
    Private ReadOnly _executionRuntimeInBed As MachineRuntime286
    Private _poweredInBed As Boolean
    Private _disposedInBed As Boolean

    Friend Sub New(buildPlanInBed As MachineBuildPlan,
                   motherboardRuntimeInBed As MotherboardRuntimeManager,
                   executionRuntimeInBed As MachineRuntime286)
        If buildPlanInBed Is Nothing Then Throw New ArgumentNullException(NameOf(buildPlanInBed))
        If motherboardRuntimeInBed Is Nothing Then Throw New ArgumentNullException(NameOf(motherboardRuntimeInBed))
        If executionRuntimeInBed Is Nothing Then Throw New ArgumentNullException(NameOf(executionRuntimeInBed))
        _buildPlanInBed = buildPlanInBed
        _definitionInBed = buildPlanInBed.Definition
        _motherboardRuntimeInBed = motherboardRuntimeInBed
        _executionRuntimeInBed = executionRuntimeInBed
    End Sub

    Public ReadOnly Property BuildPlan As MachineBuildPlan
        Get
            Return _buildPlanInBed
        End Get
    End Property

    Public ReadOnly Property Definition As MachineDefinition
        Get
            Return _definitionInBed
        End Get
    End Property

    Public ReadOnly Property MotherboardRuntime As MotherboardRuntimeManager
        Get
            Return _motherboardRuntimeInBed
        End Get
    End Property

    Public ReadOnly Property ExecutionRuntime As MachineRuntime286
        Get
            ThrowIfDisposedInBed()
            Return _executionRuntimeInBed
        End Get
    End Property

    Public ReadOnly Property IsPowered As Boolean
        Get
            Return _poweredInBed AndAlso Not _disposedInBed
        End Get
    End Property

    Public Sub PowerOn()
        ThrowIfDisposedInBed()
        If _poweredInBed Then Return

        ' A cold start is a physical chassis transition. Volatile motherboard RAM
        ' is re-populated from the Build Script's installed-memory declaration;
        ' persistent CMOS and mounted media remain attached exactly as before.
        _executionRuntimeInBed.Stop()
        IsaBackplane.SetChassisPowered(False)
        Try
            _executionRuntimeInBed.ExecuteWithHostTimeRebase(
                Sub()
                    If RamBankConfiguration.IsSupported(_definitionInBed.MemoryMegabytes) Then
                        MotherboardMemory.ConfigureInstalledMemoryMegabytes(_definitionInBed.MemoryMegabytes,
                                                                            clearRam:=True)
                    End If
                    If CPU0 IsNot Nothing Then CPU0.HostFirmwareInterrupts = False
                    PowerCycleHardwareMachine()
                    ApplyFirmwareResetVectorSafetyGuardInBed()
                    FrontPanel.SetCpuStateByte(0)
                End Sub)
            _executionRuntimeInBed.ResetHostSchedulingState()

            FrontPanel.SetPower(True)
            IsaBackplane.SetChassisPowered(True)
            _executionRuntimeInBed.Start()
            _poweredInBed = True
        Catch
            _poweredInBed = False
            IsaBackplane.SetChassisPowered(False)
            FrontPanel.SetPower(False)
            Throw
        End Try
    End Sub

    Public Sub PowerOff()
        If _disposedInBed Then Return
        _executionRuntimeInBed.Stop()
        IsaBackplane.SetChassisPowered(False)
        _poweredInBed = False
        FrontPanel.SetCpuStateByte(0)
        FrontPanel.SetPower(False)
    End Sub

    Public Sub ResetThroughHardware()
        ThrowIfDisposedInBed()
        If Not _poweredInBed Then Return

        _executionRuntimeInBed.Stop()
        Dim resetSucceededInBed As Boolean = False
        Try
            _executionRuntimeInBed.ExecuteWithHostTimeRebase(
                Sub()
                    If CPU0 IsNot Nothing Then CPU0.HostFirmwareInterrupts = False
                    ResetHardwareMachine()
                    ApplyFirmwareResetVectorSafetyGuardInBed()
                    FrontPanel.SetCpuStateByte(0)
                End Sub)
            _executionRuntimeInBed.ResetHostSchedulingState()
            resetSucceededInBed = True
        Finally
            If resetSucceededInBed AndAlso _poweredInBed AndAlso Not _disposedInBed Then
                _executionRuntimeInBed.Start()
            End If
        End Try
    End Sub

    Public Function AcquireHostQuiesceLease(Optional resumedInBed As Action = Nothing) As IDisposable
        ThrowIfDisposedInBed()
        Dim resumeRuntimeInBed As Boolean = _executionRuntimeInBed.IsRunning
        If resumeRuntimeInBed Then _executionRuntimeInBed.Stop()

        Return New HostActionLease(
            Sub()
                If resumeRuntimeInBed AndAlso _poweredInBed AndAlso Not _disposedInBed Then
                    _executionRuntimeInBed.ResetHostSchedulingState()
                    _executionRuntimeInBed.Start()
                    If resumedInBed IsNot Nothing Then resumedInBed()
                End If
            End Sub)
    End Function

    Public Function DiagnosticText() As String
        Return "Materialized machine      : " & _definitionInBed.Summary() & Environment.NewLine &
               "  ownership stage         : motherboard + execution runtime + power/reset lifecycle" & Environment.NewLine &
               "  chassis power           : " & If(IsPowered, "on", "off") & Environment.NewLine &
               _buildPlanInBed.DiagnosticText() & Environment.NewLine &
               _motherboardRuntimeInBed.DiagnosticText()
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposedInBed Then Return
        Try
            _executionRuntimeInBed.SetBoundaryService(Nothing)
        Catch ex As ObjectDisposedException
            ' Idempotent teardown if a host fault raced the close path.
        End Try
        PowerOff()
        _executionRuntimeInBed.Dispose()
        MachineMaterializer.ReleasePhysicalPopulationInBed()
        _disposedInBed = True
    End Sub

    Private Shared Sub ApplyFirmwareResetVectorSafetyGuardInBed()
        If CPU0 Is Nothing Then Return
        If CPU0.ReadWord(&HFFFF0UI) = 0 AndAlso CPU0.ReadWord(&HFFFF2UI) = 0 Then
            CPU0.Halted = True
        End If
    End Sub

    Private Sub ThrowIfDisposedInBed()
        If _disposedInBed Then Throw New ObjectDisposedException(NameOf(MaterializedMachine))
    End Sub
End Class

Public NotInheritable Class MachineMaterializer
    Private Sub New()
    End Sub

    Public Shared Function Materialize(buildPlanInBed As MachineBuildPlan) As MaterializedMachine
        If buildPlanInBed Is Nothing Then Throw New ArgumentNullException(NameOf(buildPlanInBed))
        Dim definitionInBed As MachineDefinition = buildPlanInBed.Definition
        definitionInBed.ValidateOrThrow()

        Dim motherboardRuntimeInBed As New MotherboardRuntimeManager()
        Dim activationReasonInBed As String = String.Empty
        If Not motherboardRuntimeInBed.CanActivate(definitionInBed.MotherboardId,
                                                   definitionInBed.CpuId,
                                                   activationReasonInBed) Then
            Throw New InvalidOperationException("Machine definition cannot be materialized: " & activationReasonInBed)
        End If

        Try
            motherboardRuntimeInBed.Initialize(definitionInBed.MotherboardId)
            LoadConfiguredMotherboardFirmwareInBed(definitionInBed)

            ' Materialization constructs a complete but electrically cold chassis.
            ' Board fixed fabric exists first; socket/card population is then
            ' installed from the immutable definition before the execution owner is
            ' created around the selected processor package.
            IsaBackplane.SetChassisPowered(False)
            FrontPanel.SetPower(False)
            PopulatePhysicalHardwareInBed(definitionInBed)

            Dim executionRuntimeInBed As New MachineRuntime286(CPU0, SystemBus, MachineClock)
            Return New MaterializedMachine(buildPlanInBed,
                                           motherboardRuntimeInBed,
                                           executionRuntimeInBed)
        Catch
            Try
                IsaBackplane.SetChassisPowered(False)
                ReleasePhysicalPopulationInBed()
            Catch
                ' Preserve the original materialization failure.
            End Try
            Throw
        End Try
    End Function

    Public Shared Function Materialize(definitionInBed As MachineDefinition) As MaterializedMachine
        If definitionInBed Is Nothing Then Throw New ArgumentNullException(NameOf(definitionInBed))
        Dim sourceInBed As String = MachineBuildScriptEngine.Serialize(definitionInBed)
        Dim buildPlanInBed As MachineBuildPlan = MachineBuildScriptEngine.Compile(definitionInBed.BaseDirectory,
                                                                                  sourceInBed)
        Return Materialize(buildPlanInBed)
    End Function

    Private Shared Sub LoadConfiguredMotherboardFirmwareInBed(definitionInBed As MachineDefinition)
        Dim boardProfileInBed As MotherboardProfile = MotherboardCatalog.Require(definitionInBed.MotherboardId)
        Dim relativePathInBed As String = boardProfileInBed.FirmwareImageRelativePath.Replace("\"c, Path.DirectorySeparatorChar)
        Dim romPathInBed As String = Path.Combine(definitionInBed.BaseDirectory, relativePathInBed)
        If Not File.Exists(romPathInBed) Then
            Throw New FileNotFoundException("The configured motherboard firmware image is required before the machine can be materialized.",
                                            romPathInBed)
        End If

        If String.Equals(definitionInBed.MotherboardId,
                         MotherboardCatalog.CtCs8221NeatAt286Id,
                         StringComparison.OrdinalIgnoreCase) Then
            MotherboardMemory.MirrorLegacyMemory = False
            MotherboardMemory.LoadSystemRom(File.ReadAllBytes(romPathInBed))
        End If
    End Sub

    Private Shared Sub PopulatePhysicalHardwareInBed(definitionInBed As MachineDefinition)
        ReleasePhysicalPopulationInBed()
        IsaBackplane.SetChassisPowered(False)

        If Not String.Equals(definitionInBed.CpuId,
                             MachineHardwarePopulationConfiguration.NoneId,
                             StringComparison.OrdinalIgnoreCase) Then
            SetProcessorPackageInBed(ProcessorCatalog286.CreateProcessor(definitionInBed.CpuId))
        End If
        If Not String.Equals(definitionInBed.NumericCoprocessorId,
                             MachineHardwarePopulationConfiguration.NoneId,
                             StringComparison.OrdinalIgnoreCase) Then
            SetNumericCoprocessorPackageInBed(NumericCoprocessorCatalog286.CreateNumericCoprocessor(definitionInBed.NumericCoprocessorId))
        End If
        If CPU0 IsNot Nothing Then
            CPU0.MirrorLegacyTextCells = False
            CPU0.HostFirmwareInterrupts = False
        End If

        Dim ideInBed As IdeHardwareConfiguration = definitionInBed.IdeHardware
        Declares.IdeController.ConfigureDeviceKinds(ideInBed.PrimaryMasterKind,
                                                    ideInBed.PrimarySlaveKind)
        Declares.SecondaryIdeController.ConfigureDeviceKinds(ideInBed.SecondaryMasterKind,
                                                             ideInBed.SecondarySlaveKind)

        If Not String.Equals(definitionInBed.VideoCardId,
                             MachineHardwarePopulationConfiguration.NoneId,
                             StringComparison.OrdinalIgnoreCase) Then
            Dim videoDefinitionInBed As New IsaCardDefinition286(
                MachineHardwarePopulationConfiguration.DiamondStealthPro928Id,
                IsaCardCatalog286.VideoDisplayName(definitionInBed.VideoCardId),
                IsaSlotConnectorWidth286.SixteenBit,
                Function() New DiamondStealthPro928())
            VideoCard = DirectCast(IsaBackplane.InstallCard(VideoIsaSlotId, videoDefinitionInBed), DiamondStealthPro928)
            Dim videoRomPathInBed As String = Path.Combine(definitionInBed.BaseDirectory, "Firmware", "stealthpro.rom")
            If File.Exists(videoRomPathInBed) Then VideoCard.LoadOptionRom(File.ReadAllBytes(videoRomPathInBed))
            VideoCard.DiagnosticOverlayJumperClosed = False
        End If

        If Not String.Equals(definitionInBed.AudioCardId,
                             MachineHardwarePopulationConfiguration.NoneId,
                             StringComparison.OrdinalIgnoreCase) Then
            Dim sbInBed As SoundBlaster16JumperSettings = definitionInBed.SoundBlaster16Jumpers
            Dim audioDefinitionInBed As New IsaCardDefinition286(
                MachineHardwarePopulationConfiguration.SoundBlaster16Id,
                IsaCardCatalog286.AudioDisplayName(definitionInBed.AudioCardId),
                IsaSlotConnectorWidth286.SixteenBit,
                Function()
                    Dim cardInBed As New SoundBlaster16Isa(sbInBed.BasePort,
                                                          sbInBed.Irq,
                                                          sbInBed.Dma8,
                                                          sbInBed.Dma16,
                                                          MasterPic,
                                                          SlavePic,
                                                          DmaController)
                    cardInBed.ConfigureHardware(sbInBed.BasePort,
                                                sbInBed.Irq,
                                                sbInBed.Dma8,
                                                sbInBed.Dma16,
                                                sbInBed.MpuPort,
                                                sbInBed.GamePortEnabled)
                    cardInBed.ConfigureWavetableHardware(sbInBed.Wavetable)
                    Return cardInBed
                End Function)
            SoundBlaster16 = DirectCast(IsaBackplane.InstallCard(AudioIsaSlotId, audioDefinitionInBed), SoundBlaster16Isa)
        End If

        If Not String.Equals(definitionInBed.NetworkCardId,
                             MachineHardwarePopulationConfiguration.NoneId,
                             StringComparison.OrdinalIgnoreCase) Then
            Dim neInBed As Ne2000JumperSettings = definitionInBed.Ne2000Jumpers
            If definitionInBed.NetworkCardId.StartsWith("vcb:", StringComparison.OrdinalIgnoreCase) Then
                Dim packageInBed As VmbCompiledCardPackage = VmbCompiledCardStore.FindById(definitionInBed.NetworkCardId.Substring(4))
                If packageInBed Is Nothing Then Throw New FileNotFoundException("Selected compiled network card is missing from the VCB Store.")
                Ne2000 = TryCast(VmbCompiledCardLoader.InstallIsaCard(packageInBed, NetworkIsaSlotId, neInBed.BasePort, neInBed.Irq), Ne2000Isa)
                If Ne2000 Is Nothing Then Throw New InvalidOperationException("Compiled network card root module is not a Ne2000Isa-compatible runtime module.")
            Else
                Dim networkDefinitionInBed As New IsaCardDefinition286(
                    MachineHardwarePopulationConfiguration.Ne2000Id,
                    IsaCardCatalog286.NetworkDisplayName(definitionInBed.NetworkCardId),
                    IsaSlotConnectorWidth286.SixteenBit,
                    Function()
                        Dim cardInBed As New Ne2000Isa(neInBed.BasePort, neInBed.Irq, MasterPic, SlavePic)
                        cardInBed.ConfigureHardware(neInBed.BasePort, neInBed.Irq)
                        Return cardInBed
                    End Function)
                Ne2000 = DirectCast(IsaBackplane.InstallCard(NetworkIsaSlotId, networkDefinitionInBed), Ne2000Isa)
            End If
        End If
    End Sub

    Friend Shared Sub ReleasePhysicalPopulationInBed()
        IsaBackplane.SetChassisPowered(False)
        For Each slotIdInBed As String In New String() {VideoIsaSlotId, AudioIsaSlotId, NetworkIsaSlotId}
            Dim slotInBed As IsaBackplaneSlot286 = IsaBackplane.FindSlot(slotIdInBed)
            If slotInBed IsNot Nothing AndAlso Not slotInBed.IsEmpty Then IsaBackplane.RemoveCard(slotIdInBed)
        Next
        VideoCard = Nothing
        SoundBlaster16 = Nothing
        Ne2000 = Nothing
        SetNumericCoprocessorPackageInBed(Nothing)
        SetProcessorPackageInBed(Nothing)
    End Sub
End Class

