Imports System

' Runtime side of a motherboard profile.  Profiles answer "what board is this?";
' runtime backplanes answer "which fixed electrical fabric is currently wired to
' the machine bus?"  Keeping the two separate lets the configurator describe a
' real board before its processor core exists without ever substituting another
' chipset behind the label.
Public Interface IMotherboardRuntimeBackplane
    ReadOnly Property ProfileId As String
    ReadOnly Property ImplementationId As String
    ReadOnly Property RuntimeDisplayName As String
    ReadOnly Property FixedFabricAvailable As Boolean
    ReadOnly Property ProcessorExecutionAvailable As Boolean
    ReadOnly Property ProcessorArchitecture As String

    Sub InitializeFixedFabric()
    Function SupportsProcessorPackage(cpuIdInBed As String) As Boolean
    Function DiagnosticText() As String
End Interface

' Adapter over the existing, proven 286 motherboard graph.  Checkpoint 03 does
' not reconstruct or reorder any of Declares.InitializeHardware's device graph;
' it merely puts that graph behind the same factory boundary future boards use.
Public NotInheritable Class NeatAt286RuntimeBackplane
    Implements IMotherboardRuntimeBackplane

    Public ReadOnly Property ProfileId As String Implements IMotherboardRuntimeBackplane.ProfileId
        Get
            Return MotherboardCatalog.CtCs8221NeatAt286Id
        End Get
    End Property

    Public ReadOnly Property ImplementationId As String Implements IMotherboardRuntimeBackplane.ImplementationId
        Get
            Return MotherboardCatalog.CtCs8221NeatBackplaneImplementationId
        End Get
    End Property

    Public ReadOnly Property RuntimeDisplayName As String Implements IMotherboardRuntimeBackplane.RuntimeDisplayName
        Get
            Return "C&T CS8221 NEAT AT runtime backplane"
        End Get
    End Property

    Public ReadOnly Property FixedFabricAvailable As Boolean Implements IMotherboardRuntimeBackplane.FixedFabricAvailable
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property ProcessorExecutionAvailable As Boolean Implements IMotherboardRuntimeBackplane.ProcessorExecutionAvailable
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property ProcessorArchitecture As String Implements IMotherboardRuntimeBackplane.ProcessorArchitecture
        Get
            Return "80286"
        End Get
    End Property

    Public Sub InitializeFixedFabric() Implements IMotherboardRuntimeBackplane.InitializeFixedFabric
        Declares.InitializeHardware()
    End Sub

    Public Function SupportsProcessorPackage(cpuIdInBed As String) As Boolean Implements IMotherboardRuntimeBackplane.SupportsProcessorPackage
        Return String.Equals(cpuIdInBed, ProcessorPackageCatalog.NoneId, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(cpuIdInBed, ProcessorPackageCatalog.HarrisCs80C28625Id, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Function DiagnosticText() As String Implements IMotherboardRuntimeBackplane.DiagnosticText
        Return "Runtime motherboard       : " & RuntimeDisplayName & Environment.NewLine &
               "  profile                  : " & ProfileId & Environment.NewLine &
               "  implementation           : " & ImplementationId & Environment.NewLine &
               "  processor architecture   : " & ProcessorArchitecture & Environment.NewLine &
               "  fixed fabric             : wired" & Environment.NewLine &
               "  processor execution      : available"
    End Function
End Class

' CP04: the actual OPTi register-level silicon now exists.  The complete QDI
' machine fabric is still intentionally activation-gated until CP05 separates the
' shared AT devices/NVRAM from the NEAT-only global graph.  This object lets host
' diagnostics instantiate and validate the real board chipset without pretending
' an 80286 is a 486.
Public NotInheritable Class QdiOpti895RuntimeBackplane
    Implements IMotherboardRuntimeBackplane

    Private ReadOnly _chipsetInBed As Opti895Chipset486

    Public Sub New()
        _chipsetInBed = New Opti895Chipset486()
        _chipsetInBed.ValidateResetDefaultsOrThrow()
    End Sub

    Public ReadOnly Property Chipset As Opti895Chipset486
        Get
            Return _chipsetInBed
        End Get
    End Property

    Public ReadOnly Property ProfileId As String Implements IMotherboardRuntimeBackplane.ProfileId
        Get
            Return MotherboardCatalog.QdiV4P895P3SmtV5Id
        End Get
    End Property

    Public ReadOnly Property ImplementationId As String Implements IMotherboardRuntimeBackplane.ImplementationId
        Get
            Return MotherboardCatalog.QdiV4P895P3BackplaneImplementationId
        End Get
    End Property

    Public ReadOnly Property RuntimeDisplayName As String Implements IMotherboardRuntimeBackplane.RuntimeDisplayName
        Get
            Return "QDI V4P895P3/SMT OPTi 82C895 + 82C602 backplane"
        End Get
    End Property

    Public ReadOnly Property FixedFabricAvailable As Boolean Implements IMotherboardRuntimeBackplane.FixedFabricAvailable
        Get
            ' Register-level fixed silicon is implemented, but it is not safe to
            ' attach to the singleton machine bus until the common AT fabric is
            ' separated from the current NEAT-only initialization graph in CP05.
            Return False
        End Get
    End Property

    Public ReadOnly Property ProcessorExecutionAvailable As Boolean Implements IMotherboardRuntimeBackplane.ProcessorExecutionAvailable
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property ProcessorArchitecture As String Implements IMotherboardRuntimeBackplane.ProcessorArchitecture
        Get
            Return "80486"
        End Get
    End Property

    Public Sub InitializeFixedFabric() Implements IMotherboardRuntimeBackplane.InitializeFixedFabric
        Throw New InvalidOperationException("The QDI OPTi silicon model is present, but whole-machine bus attachment is gated until the AT fabric split is complete.")
    End Sub

    Public Function SupportsProcessorPackage(cpuIdInBed As String) As Boolean Implements IMotherboardRuntimeBackplane.SupportsProcessorPackage
        ' Empty socket is electrically honest; all actual 486 packages remain
        ' execution-gated until the corresponding processor core exists.
        Return String.Equals(cpuIdInBed, ProcessorPackageCatalog.NoneId, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Function DiagnosticText() As String Implements IMotherboardRuntimeBackplane.DiagnosticText
        Return "Runtime motherboard       : " & RuntimeDisplayName & Environment.NewLine &
               "  profile                  : " & ProfileId & Environment.NewLine &
               "  implementation           : " & ImplementationId & Environment.NewLine &
               "  processor architecture   : " & ProcessorArchitecture & Environment.NewLine &
               "  OPTi register silicon    : implemented and reset-validated" & Environment.NewLine &
               "  common AT fabric wiring  : pending CP05" & Environment.NewLine &
               "  processor execution      : 80486 core not present" & Environment.NewLine &
               _chipsetInBed.DiagnosticText()
    End Function
End Class

Public NotInheritable Class MotherboardRuntimeFactory
    Private Sub New()
    End Sub

    Public Shared Function Create(profileIdInBed As String) As IMotherboardRuntimeBackplane
        Select Case MotherboardCatalog.NormalizeId(profileIdInBed)
            Case MotherboardCatalog.CtCs8221NeatAt286Id
                Return New NeatAt286RuntimeBackplane()
            Case MotherboardCatalog.QdiV4P895P3SmtV5Id
                Return New QdiOpti895RuntimeBackplane()
            Case Else
                Throw New InvalidOperationException("No runtime motherboard factory is registered for '" & If(profileIdInBed, String.Empty) & "'.")
        End Select
    End Function
End Class

Public NotInheritable Class MotherboardRuntimeManager
    Private _currentInBed As IMotherboardRuntimeBackplane

    Public ReadOnly Property Current As IMotherboardRuntimeBackplane
        Get
            Return _currentInBed
        End Get
    End Property

    Public Sub Initialize(profileIdInBed As String)
        If _currentInBed IsNot Nothing Then Return
        Dim nextInBed As IMotherboardRuntimeBackplane = MotherboardRuntimeFactory.Create(profileIdInBed)
        If Not nextInBed.FixedFabricAvailable Then
            Throw New InvalidOperationException(nextInBed.RuntimeDisplayName & " cannot be activated because its fixed fabric is not implemented.")
        End If
        nextInBed.InitializeFixedFabric()
        _currentInBed = nextInBed
    End Sub

    Public Function CanActivate(profileIdInBed As String, ByRef reasonInBed As String) As Boolean
        Return CanActivate(profileIdInBed, ProcessorPackageCatalog.NoneId, reasonInBed)
    End Function

    Public Function CanActivate(profileIdInBed As String,
                                cpuIdInBed As String,
                                ByRef reasonInBed As String) As Boolean
        Dim candidateInBed As IMotherboardRuntimeBackplane = MotherboardRuntimeFactory.Create(profileIdInBed)
        If Not candidateInBed.FixedFabricAvailable Then
            reasonInBed = candidateInBed.RuntimeDisplayName & " is not yet attachable as the live machine fabric."
            Return False
        End If
        If Not candidateInBed.SupportsProcessorPackage(cpuIdInBed) Then
            reasonInBed = candidateInBed.RuntimeDisplayName & " cannot execute processor package '" &
                          ProcessorPackageCatalog.DisplayName(cpuIdInBed) & "' in this build."
            Return False
        End If
        reasonInBed = String.Empty
        Return True
    End Function

    Public Function DiagnosticText() As String
        If _currentInBed Is Nothing Then Return "Runtime motherboard       : not initialized"
        Return _currentInBed.DiagnosticText()
    End Function
End Class
