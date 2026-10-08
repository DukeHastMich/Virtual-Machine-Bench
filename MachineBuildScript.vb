Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text

' Machine-level construction language.
'
' Board Script (.bs) describes the fixed motherboard fabric.  Machine Build
' Script (.vmbuild) describes which physical board, packages, expansion cards,
' jumpers, RAM/cache/clock straps and IDE devices are installed in one chassis.
' The configurator emits this text; the compiler turns it back into an immutable
' MachineDefinition; the materializer is the only layer allowed to construct the
' live runtime graph from that definition.
Public Enum MachineBuildStepKind
    ValidateDefinition = 0
    ActivateMotherboardFabric = 1
    PopulatePhysicalHardware = 2
    CreateExecutionRuntime = 3
End Enum

Public NotInheritable Class MachineBuildStep
    Public Sub New(kindInBed As MachineBuildStepKind, descriptionInBed As String)
        Kind = kindInBed
        Description = If(descriptionInBed, String.Empty)
    End Sub

    Public ReadOnly Property Kind As MachineBuildStepKind
    Public ReadOnly Property Description As String
End Class

Public NotInheritable Class MachineBuildPlan
    Private ReadOnly _stepsInBed As List(Of MachineBuildStep)

    Friend Sub New(sourceTextInBed As String,
                   definitionInBed As MachineDefinition,
                   stepsInBed As IEnumerable(Of MachineBuildStep))
        If definitionInBed Is Nothing Then Throw New ArgumentNullException(NameOf(definitionInBed))
        SourceText = If(sourceTextInBed, String.Empty)
        Definition = definitionInBed
        _stepsInBed = New List(Of MachineBuildStep)(If(stepsInBed, Array.Empty(Of MachineBuildStep)()))
    End Sub

    Public ReadOnly Property SourceText As String
    Public ReadOnly Property Definition As MachineDefinition

    Public Function Steps() As MachineBuildStep()
        Return _stepsInBed.ToArray()
    End Function

    Public Function DiagnosticText() As String
        Dim textInBed As New StringBuilder()
        textInBed.AppendLine("Machine build plan       : compiled")
        textInBed.AppendLine("  definition             : " & Definition.Summary())
        For iInBed As Integer = 0 To _stepsInBed.Count - 1
            textInBed.AppendLine("  step " & (iInBed + 1).ToString(CultureInfo.InvariantCulture) &
                                 "                  : " & _stepsInBed(iInBed).Description)
        Next
        Return textInBed.ToString().TrimEnd()
    End Function
End Class

Public NotInheritable Class MachineBuildScriptEngine
    Public Const CurrentVersion As Integer = 1
    Public Const HeaderPrefix As String = "VMB-MACHINE-BUILD"
    Public Const DefaultBuildScriptFileName As String = "VirtualComputer.build.vmbuild"

    Private Shared ReadOnly KnownKeysInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "machine.motherboard",
        "machine.cpu",
        "machine.npx",
        "machine.memory_mb",
        "machine.cache_kb",
        "machine.clock",
        "card.video",
        "card.audio",
        "card.network",
        "ide.primary_master.kind",
        "ide.primary_master.model",
        "ide.primary_slave.kind",
        "ide.primary_slave.model",
        "ide.secondary_master.kind",
        "ide.secondary_master.model",
        "ide.secondary_slave.kind",
        "ide.secondary_slave.model",
        "sb16.base_port",
        "sb16.irq",
        "sb16.dma8",
        "sb16.dma16",
        "sb16.mpu_port",
        "sb16.game_port",
        "sb16.wavetable.installed",
        "sb16.wavetable.profile",
        "sb16.wavetable.bank",
        "ne2000.base_port",
        "ne2000.irq"
    }

    Private Sub New()
    End Sub

    ' The configurator does not materialize hardware directly.  It captures its
    ' selected physical state, emits canonical text, and recompiles that text.
    ' That round-trip makes the Build Script the inspectable/replayable authority
    ' for what the operator actually asked the bench to construct.
    Public Shared Function CaptureConfiguredMachine(baseDirectoryInBed As String,
                                                    hardwarePopulationInBed As MachineHardwarePopulationConfiguration,
                                                    isaConfigurationInBed As IsaExpansionCardConfiguration) As MachineBuildPlan
        Dim capturedInBed As MachineDefinition =
            MachineDefinition.CaptureConfiguredMachine(baseDirectoryInBed,
                                                       hardwarePopulationInBed,
                                                       isaConfigurationInBed)
        Dim sourceInBed As String = Serialize(capturedInBed)
        Return Compile(baseDirectoryInBed, sourceInBed)
    End Function

    Public Shared Function Serialize(definitionInBed As MachineDefinition) As String
        If definitionInBed Is Nothing Then Throw New ArgumentNullException(NameOf(definitionInBed))
        definitionInBed.ValidateOrThrow()

        Dim ideInBed As IdeHardwareConfiguration = definitionInBed.IdeHardware
        Dim sbInBed As SoundBlaster16JumperSettings = definitionInBed.SoundBlaster16Jumpers
        Dim neInBed As Ne2000JumperSettings = definitionInBed.Ne2000Jumpers
        Dim wtInBed As WavetableAttachmentSettings = If(sbInBed.Wavetable, New WavetableAttachmentSettings())
        Dim textInBed As New StringBuilder()

        textInBed.AppendLine(HeaderPrefix & " " & CurrentVersion.ToString(CultureInfo.InvariantCulture))
        textInBed.AppendLine("# Generated by the VMB configurator. This is host-side physical chassis state.")
        textInBed.AppendLine("# Board Script owns motherboard composition/wiring; this file owns machine population.")
        AppendSettingInBed(textInBed, "machine.motherboard", definitionInBed.MotherboardId)
        AppendSettingInBed(textInBed, "machine.cpu", definitionInBed.CpuId)
        AppendSettingInBed(textInBed, "machine.npx", definitionInBed.NumericCoprocessorId)
        AppendSettingInBed(textInBed, "machine.memory_mb", definitionInBed.MemoryMegabytes.ToString(CultureInfo.InvariantCulture))
        AppendSettingInBed(textInBed, "machine.cache_kb", definitionInBed.CacheKilobytes.ToString(CultureInfo.InvariantCulture))
        AppendSettingInBed(textInBed, "machine.clock", definitionInBed.ClockOptionId)
        AppendSettingInBed(textInBed, "card.video", definitionInBed.VideoCardId)
        AppendSettingInBed(textInBed, "card.audio", definitionInBed.AudioCardId)
        AppendSettingInBed(textInBed, "card.network", definitionInBed.NetworkCardId)

        AppendIdeSlotInBed(textInBed, "primary_master", ideInBed.PrimaryMasterKind, ideInBed.PrimaryMasterModelId)
        AppendIdeSlotInBed(textInBed, "primary_slave", ideInBed.PrimarySlaveKind, ideInBed.PrimarySlaveModelId)
        AppendIdeSlotInBed(textInBed, "secondary_master", ideInBed.SecondaryMasterKind, ideInBed.SecondaryMasterModelId)
        AppendIdeSlotInBed(textInBed, "secondary_slave", ideInBed.SecondarySlaveKind, ideInBed.SecondarySlaveModelId)

        AppendSettingInBed(textInBed, "sb16.base_port", FormatPortInBed(sbInBed.BasePort))
        AppendSettingInBed(textInBed, "sb16.irq", sbInBed.Irq.ToString(CultureInfo.InvariantCulture))
        AppendSettingInBed(textInBed, "sb16.dma8", sbInBed.Dma8.ToString(CultureInfo.InvariantCulture))
        AppendSettingInBed(textInBed, "sb16.dma16", sbInBed.Dma16.ToString(CultureInfo.InvariantCulture))
        AppendSettingInBed(textInBed, "sb16.mpu_port", FormatPortInBed(sbInBed.MpuPort))
        AppendSettingInBed(textInBed, "sb16.game_port", If(sbInBed.GamePortEnabled, "on", "off"))
        AppendSettingInBed(textInBed, "sb16.wavetable.installed", If(wtInBed.Installed, "on", "off"))
        AppendSettingInBed(textInBed, "sb16.wavetable.profile", WavetableAttachmentSettings.NormalizeProfileId(wtInBed.ProfileId))
        AppendSettingInBed(textInBed, "sb16.wavetable.bank", WavetableAttachmentSettings.NormalizeBankFileName(wtInBed.BankFileName))

        AppendSettingInBed(textInBed, "ne2000.base_port", FormatPortInBed(neInBed.BasePort))
        AppendSettingInBed(textInBed, "ne2000.irq", neInBed.Irq.ToString(CultureInfo.InvariantCulture))
        Return textInBed.ToString()
    End Function

    Public Shared Function Compile(baseDirectoryInBed As String, sourceTextInBed As String) As MachineBuildPlan
        If String.IsNullOrWhiteSpace(baseDirectoryInBed) Then Throw New ArgumentNullException(NameOf(baseDirectoryInBed))
        If sourceTextInBed Is Nothing Then Throw New ArgumentNullException(NameOf(sourceTextInBed))

        Dim settingsInBed As Dictionary(Of String, String) = ParseSettingsInBed(sourceTextInBed)
        Dim definitionInBed As MachineDefinition = BuildDefinitionInBed(Path.GetFullPath(baseDirectoryInBed), settingsInBed)
        definitionInBed.ValidateOrThrow()

        Dim stepsInBed As New List(Of MachineBuildStep) From {
            New MachineBuildStep(MachineBuildStepKind.ValidateDefinition,
                                 "validate physical population, board straps and ISA resource claims"),
            New MachineBuildStep(MachineBuildStepKind.ActivateMotherboardFabric,
                                 "activate the selected board's real fixed-fabric runtime"),
            New MachineBuildStep(MachineBuildStepKind.PopulatePhysicalHardware,
                                 "install processor/NPX, IDE socket kinds and configured ISA boards while chassis power is off"),
            New MachineBuildStep(MachineBuildStepKind.CreateExecutionRuntime,
                                 "create the deterministic machine execution owner around the installed processor package")
        }
        Return New MachineBuildPlan(sourceTextInBed, definitionInBed, stepsInBed)
    End Function

    Public Shared Function CompileFile(baseDirectoryInBed As String, pathInBed As String) As MachineBuildPlan
        If String.IsNullOrWhiteSpace(pathInBed) Then Throw New ArgumentNullException(NameOf(pathInBed))
        Dim fullPathInBed As String = Path.GetFullPath(pathInBed)
        If Not File.Exists(fullPathInBed) Then Throw New FileNotFoundException("Machine Build Script was not found.", fullPathInBed)
        Return Compile(baseDirectoryInBed, File.ReadAllText(fullPathInBed))
    End Function

    Public Shared Sub SaveCanonical(pathInBed As String, planInBed As MachineBuildPlan)
        If String.IsNullOrWhiteSpace(pathInBed) Then Throw New ArgumentNullException(NameOf(pathInBed))
        If planInBed Is Nothing Then Throw New ArgumentNullException(NameOf(planInBed))
        Dim fullPathInBed As String = Path.GetFullPath(pathInBed)
        Dim parentInBed As String = Path.GetDirectoryName(fullPathInBed)
        If Not String.IsNullOrWhiteSpace(parentInBed) Then Directory.CreateDirectory(parentInBed)
        File.WriteAllText(fullPathInBed, Serialize(planInBed.Definition), New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
    End Sub

    Private Shared Sub AppendIdeSlotInBed(textInBed As StringBuilder,
                                          slotInBed As String,
                                          kindInBed As IdeDeviceKind,
                                          modelInBed As String)
        AppendSettingInBed(textInBed, "ide." & slotInBed & ".kind", IdeHardwareConfiguration.KindId(kindInBed))
        AppendSettingInBed(textInBed, "ide." & slotInBed & ".model", If(modelInBed, String.Empty).Trim())
    End Sub

    Private Shared Sub AppendSettingInBed(textInBed As StringBuilder, keyInBed As String, valueInBed As String)
        textInBed.Append(keyInBed).Append(" = ").AppendLine(If(valueInBed, String.Empty))
    End Sub

    Private Shared Function FormatPortInBed(valueInBed As UInt16) As String
        Return valueInBed.ToString("X3", CultureInfo.InvariantCulture) & "h"
    End Function

    Private Shared Function ParseSettingsInBed(sourceTextInBed As String) As Dictionary(Of String, String)
        Dim settingsInBed As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim sawHeaderInBed As Boolean = False
        Dim normalizedTextInBed As String = sourceTextInBed.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        Dim linesInBed As String() = normalizedTextInBed.Split(ControlChars.Lf)

        For iInBed As Integer = 0 To linesInBed.Length - 1
            Dim rawInBed As String = linesInBed(iInBed)
            Dim trimmedInBed As String = rawInBed.Trim()
            If trimmedInBed.Length = 0 OrElse trimmedInBed.StartsWith("#", StringComparison.Ordinal) Then Continue For

            If Not sawHeaderInBed Then
                Dim expectedInBed As String = HeaderPrefix & " " & CurrentVersion.ToString(CultureInfo.InvariantCulture)
                If Not String.Equals(trimmedInBed, expectedInBed, StringComparison.OrdinalIgnoreCase) Then
                    Throw BuildScriptErrorInBed(iInBed + 1,
                                                "Expected header '" & expectedInBed & "'.")
                End If
                sawHeaderInBed = True
                Continue For
            End If

            Dim equalsInBed As Integer = rawInBed.IndexOf("="c)
            If equalsInBed <= 0 Then
                Throw BuildScriptErrorInBed(iInBed + 1, "Expected 'key = value'.")
            End If
            Dim keyInBed As String = rawInBed.Substring(0, equalsInBed).Trim()
            Dim valueInBed As String = rawInBed.Substring(equalsInBed + 1).Trim()
            If Not KnownKeysInBed.Contains(keyInBed) Then
                Throw BuildScriptErrorInBed(iInBed + 1, "Unknown machine build key '" & keyInBed & "'.")
            End If
            If settingsInBed.ContainsKey(keyInBed) Then
                Throw BuildScriptErrorInBed(iInBed + 1, "Duplicate machine build key '" & keyInBed & "'.")
            End If
            settingsInBed.Add(keyInBed, valueInBed)
        Next

        If Not sawHeaderInBed Then Throw New InvalidOperationException("Machine Build Script is empty or has no version header.")
        For Each keyInBed As String In KnownKeysInBed
            If Not settingsInBed.ContainsKey(keyInBed) Then
                Throw New InvalidOperationException("Machine Build Script is missing required key '" & keyInBed & "'.")
            End If
        Next
        Return settingsInBed
    End Function

    Private Shared Function BuildDefinitionInBed(baseDirectoryInBed As String,
                                                 settingsInBed As Dictionary(Of String, String)) As MachineDefinition
        Dim populationInBed As New MachineHardwarePopulationConfiguration(baseDirectoryInBed) With {
            .MotherboardId = RequiredValueInBed(settingsInBed, "machine.motherboard"),
            .CpuId = RequiredValueInBed(settingsInBed, "machine.cpu"),
            .NumericCoprocessorId = RequiredValueInBed(settingsInBed, "machine.npx"),
            .VideoCardId = RequiredValueInBed(settingsInBed, "card.video"),
            .AudioCardId = RequiredValueInBed(settingsInBed, "card.audio"),
            .NetworkCardId = RequiredValueInBed(settingsInBed, "card.network")
        }

        ValidatePopulationIdentifiersInBed(populationInBed)

        Dim memoryInBed As Integer = ParseIntegerInBed(settingsInBed, "machine.memory_mb")
        Dim cacheInBed As Integer = ParseIntegerInBed(settingsInBed, "machine.cache_kb")
        Dim clockInBed As String = RequiredValueInBed(settingsInBed, "machine.clock")

        Dim ideInBed As New IdeHardwareConfiguration(baseDirectoryInBed)
        ApplyIdeSlotInBed(ideInBed, settingsInBed, "primary-master", "primary_master")
        ApplyIdeSlotInBed(ideInBed, settingsInBed, "primary-slave", "primary_slave")
        ApplyIdeSlotInBed(ideInBed, settingsInBed, "secondary-master", "secondary_master")
        ApplyIdeSlotInBed(ideInBed, settingsInBed, "secondary-slave", "secondary_slave")

        Dim sbInBed As New SoundBlaster16JumperSettings With {
            .BasePort = ParsePortInBed(settingsInBed, "sb16.base_port"),
            .Irq = ParseIntegerInBed(settingsInBed, "sb16.irq"),
            .Dma8 = ParseIntegerInBed(settingsInBed, "sb16.dma8"),
            .Dma16 = ParseIntegerInBed(settingsInBed, "sb16.dma16"),
            .MpuPort = ParsePortInBed(settingsInBed, "sb16.mpu_port"),
            .GamePortEnabled = ParseBooleanInBed(settingsInBed, "sb16.game_port")
        }
        sbInBed.Wavetable = New WavetableAttachmentSettings With {
            .Installed = ParseBooleanInBed(settingsInBed, "sb16.wavetable.installed"),
            .ProfileId = WavetableAttachmentSettings.NormalizeProfileId(settingsInBed("sb16.wavetable.profile")),
            .BankFileName = WavetableAttachmentSettings.NormalizeBankFileName(settingsInBed("sb16.wavetable.bank"))
        }

        Dim neInBed As New Ne2000JumperSettings With {
            .BasePort = ParsePortInBed(settingsInBed, "ne2000.base_port"),
            .Irq = ParseIntegerInBed(settingsInBed, "ne2000.irq")
        }
        ValidateJumperChoicesInBed(sbInBed, neInBed, populationInBed)

        Return New MachineDefinition(baseDirectoryInBed,
                                     populationInBed,
                                     memoryInBed,
                                     cacheInBed,
                                     clockInBed,
                                     ideInBed,
                                     sbInBed,
                                     neInBed)
    End Function

    Private Shared Sub ValidatePopulationIdentifiersInBed(populationInBed As MachineHardwarePopulationConfiguration)
        If MotherboardCatalog.Find(populationInBed.MotherboardId) Is Nothing Then
            Throw New InvalidOperationException("Machine Build Script names unknown motherboard '" & populationInBed.MotherboardId & "'.")
        End If
        If ProcessorPackageCatalog.Find(populationInBed.CpuId) Is Nothing Then
            Throw New InvalidOperationException("Machine Build Script names unknown CPU package '" & populationInBed.CpuId & "'.")
        End If
        If NumericCoprocessorPackageCatalog.Find(populationInBed.NumericCoprocessorId) Is Nothing Then
            Throw New InvalidOperationException("Machine Build Script names unknown numeric coprocessor package '" & populationInBed.NumericCoprocessorId & "'.")
        End If
        ValidateCardIdInBed(populationInBed.VideoCardId,
                            MachineHardwarePopulationConfiguration.DiamondStealthPro928Id,
                            "video")
        ValidateCardIdInBed(populationInBed.AudioCardId,
                            MachineHardwarePopulationConfiguration.SoundBlaster16Id,
                            "audio")
        ValidateCardIdInBed(populationInBed.NetworkCardId,
                            MachineHardwarePopulationConfiguration.Ne2000Id,
                            "network")
    End Sub

    Private Shared Sub ValidateCardIdInBed(valueInBed As String, supportedInBed As String, classInBed As String)
        If String.Equals(valueInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then Return
        If String.Equals(valueInBed, supportedInBed, StringComparison.OrdinalIgnoreCase) Then Return
        If String.Equals(classInBed, "network", StringComparison.OrdinalIgnoreCase) AndAlso valueInBed.StartsWith("vcb:", StringComparison.OrdinalIgnoreCase) Then
            If VmbCompiledCardStore.FindById(valueInBed.Substring(4)) IsNot Nothing Then Return
        End If
        Throw New InvalidOperationException("Machine Build Script names unknown " & classInBed & " card '" & valueInBed & "'.")
    End Sub

    Private Shared Sub ApplyIdeSlotInBed(ideInBed As IdeHardwareConfiguration,
                                         settingsInBed As Dictionary(Of String, String),
                                         slotApiNameInBed As String,
                                         slotScriptNameInBed As String)
        Dim kindKeyInBed As String = "ide." & slotScriptNameInBed & ".kind"
        Dim modelKeyInBed As String = "ide." & slotScriptNameInBed & ".model"
        Dim kindInBed As IdeDeviceKind = ParseIdeKindStrictInBed(settingsInBed(kindKeyInBed), kindKeyInBed)
        Dim modelInBed As String = settingsInBed(modelKeyInBed).Trim()
        ideInBed.SetKind(slotApiNameInBed, kindInBed)

        If kindInBed = IdeDeviceKind.AtaHardDisk Then
            If modelInBed.Length = 0 Then
                Throw New InvalidOperationException("Machine Build Script requires an ATA model for '" & slotScriptNameInBed & "'.")
            End If
            ideInBed.SetModelId(slotApiNameInBed, modelInBed)
        Else
            If modelInBed.Length <> 0 Then
                Throw New InvalidOperationException("Machine Build Script gives non-ATA slot '" & slotScriptNameInBed & "' an ATA model.")
            End If
            ideInBed.SetModelId(slotApiNameInBed, String.Empty)
        End If
    End Sub

    Private Shared Function ParseIdeKindStrictInBed(valueInBed As String, keyInBed As String) As IdeDeviceKind
        Select Case If(valueInBed, String.Empty).Trim().ToLowerInvariant()
            Case "ata" : Return IdeDeviceKind.AtaHardDisk
            Case "atapi" : Return IdeDeviceKind.AtapiCdRom
            Case "empty" : Return IdeDeviceKind.Empty
            Case Else
                Throw New InvalidOperationException("Machine Build Script key '" & keyInBed & "' has invalid IDE kind '" & valueInBed & "'.")
        End Select
    End Function

    Private Shared Sub ValidateJumperChoicesInBed(sbInBed As SoundBlaster16JumperSettings,
                                                   neInBed As Ne2000JumperSettings,
                                                   populationInBed As MachineHardwarePopulationConfiguration)
        RequireChoiceInBed(sbInBed.BasePort, IsaExpansionCardConfiguration.Sb16BasePorts, "sb16.base_port")
        RequireChoiceInBed(sbInBed.Irq, IsaExpansionCardConfiguration.Sb16Irqs, "sb16.irq")
        RequireChoiceInBed(sbInBed.Dma8, IsaExpansionCardConfiguration.Sb16Dma8Channels, "sb16.dma8")
        RequireChoiceInBed(sbInBed.Dma16, IsaExpansionCardConfiguration.Sb16Dma16Channels, "sb16.dma16")
        RequireChoiceInBed(sbInBed.MpuPort, IsaExpansionCardConfiguration.Sb16MpuPorts, "sb16.mpu_port")
        If populationInBed.NetworkCardId.StartsWith("vcb:", StringComparison.OrdinalIgnoreCase) Then
            Dim packageInBed As VmbCompiledCardPackage = VmbCompiledCardStore.FindById(populationInBed.NetworkCardId.Substring(4))
            If packageInBed Is Nothing Then Throw New InvalidOperationException("Machine Build Script names a VCB that is absent from VCB Store.")
            For Each pInBed As VmbCompiledCardProperty In packageInBed.Header.Properties
                If String.Equals(pInBed.Name, "IO_BASE", StringComparison.OrdinalIgnoreCase) AndAlso Not pInBed.AllowedValues.Contains(CInt(neInBed.BasePort).ToString()) Then Throw New InvalidOperationException("ne2000.base_port is not physically selectable on VCB.")
                If String.Equals(pInBed.Name, "IRQ", StringComparison.OrdinalIgnoreCase) AndAlso Not pInBed.AllowedValues.Contains(neInBed.Irq.ToString()) Then Throw New InvalidOperationException("ne2000.irq is not physically selectable on VCB.")
            Next
        Else
            RequireChoiceInBed(neInBed.BasePort, IsaExpansionCardConfiguration.Ne2000BasePorts, "ne2000.base_port")
            RequireChoiceInBed(neInBed.Irq, IsaExpansionCardConfiguration.Ne2000Irqs, "ne2000.irq")
        End If
    End Sub

    Private Shared Sub RequireChoiceInBed(Of TValue)(valueInBed As TValue, allowedInBed As TValue(), keyInBed As String)
        If Array.IndexOf(allowedInBed, valueInBed) >= 0 Then Return
        Throw New InvalidOperationException("Machine Build Script key '" & keyInBed & "' selects a position not available on the physical jumpers/DIP switches.")
    End Sub

    Private Shared Function RequiredValueInBed(settingsInBed As Dictionary(Of String, String), keyInBed As String) As String
        Dim valueInBed As String = settingsInBed(keyInBed).Trim()
        If valueInBed.Length = 0 Then Throw New InvalidOperationException("Machine Build Script key '" & keyInBed & "' may not be blank.")
        Return valueInBed
    End Function

    Private Shared Function ParseIntegerInBed(settingsInBed As Dictionary(Of String, String), keyInBed As String) As Integer
        Dim parsedInBed As Integer
        If Not Integer.TryParse(settingsInBed(keyInBed), NumberStyles.Integer, CultureInfo.InvariantCulture, parsedInBed) Then
            Throw New InvalidOperationException("Machine Build Script key '" & keyInBed & "' is not an integer.")
        End If
        Return parsedInBed
    End Function

    Private Shared Function ParsePortInBed(settingsInBed As Dictionary(Of String, String), keyInBed As String) As UInt16
        Dim rawInBed As String = settingsInBed(keyInBed).Trim()
        If rawInBed.EndsWith("h", StringComparison.OrdinalIgnoreCase) Then rawInBed = rawInBed.Substring(0, rawInBed.Length - 1)
        If rawInBed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) Then rawInBed = rawInBed.Substring(2)
        Dim parsedInBed As Integer
        If Not Integer.TryParse(rawInBed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, parsedInBed) OrElse
           parsedInBed < 0 OrElse parsedInBed > UInt16.MaxValue Then
            Throw New InvalidOperationException("Machine Build Script key '" & keyInBed & "' is not a 16-bit hexadecimal I/O port.")
        End If
        Return CUShort(parsedInBed)
    End Function

    Private Shared Function ParseBooleanInBed(settingsInBed As Dictionary(Of String, String), keyInBed As String) As Boolean
        Select Case settingsInBed(keyInBed).Trim().ToLowerInvariant()
            Case "on", "true", "yes", "1" : Return True
            Case "off", "false", "no", "0" : Return False
            Case Else
                Throw New InvalidOperationException("Machine Build Script key '" & keyInBed & "' must be on/off or true/false.")
        End Select
    End Function

    Private Shared Function BuildScriptErrorInBed(lineNumberInBed As Integer, messageInBed As String) As Exception
        Return New InvalidOperationException("Machine Build Script line " & lineNumberInBed.ToString(CultureInfo.InvariantCulture) & ": " & messageInBed)
    End Function
End Class
