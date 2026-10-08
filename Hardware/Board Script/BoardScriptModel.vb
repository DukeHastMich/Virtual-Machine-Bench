Imports System
Imports System.Collections.Generic

Public Enum BoardScriptValueKind
    Text = 0
    Number = 1
    BooleanLiteral = 2
End Enum

Public NotInheritable Class BoardScriptValue
    Public ReadOnly Property Kind As BoardScriptValueKind
    Public ReadOnly Property TextValue As String
    Public ReadOnly Property NumberValue As Double
    Public ReadOnly Property BooleanValue As Boolean

    Private Sub New(kindInBed As BoardScriptValueKind,
                    textInBed As String,
                    numberInBed As Double,
                    booleanInBed As Boolean)
        Kind = kindInBed
        TextValue = If(textInBed, String.Empty)
        NumberValue = numberInBed
        BooleanValue = booleanInBed
    End Sub

    Public Shared Function FromText(valueInBed As String) As BoardScriptValue
        Return New BoardScriptValue(BoardScriptValueKind.Text, valueInBed, 0, False)
    End Function

    Public Shared Function FromNumber(valueInBed As Double) As BoardScriptValue
        Return New BoardScriptValue(BoardScriptValueKind.Number,
                                    valueInBed.ToString(Globalization.CultureInfo.InvariantCulture),
                                    valueInBed,
                                    False)
    End Function

    Public Shared Function FromBoolean(valueInBed As Boolean) As BoardScriptValue
        Return New BoardScriptValue(BoardScriptValueKind.BooleanLiteral,
                                    If(valueInBed, "true", "false"),
                                    0,
                                    valueInBed)
    End Function

    Public Function AsInteger(nameInBed As String) As Integer
        If Kind <> BoardScriptValueKind.Number OrElse Math.Truncate(NumberValue) <> NumberValue Then
            Throw New InvalidOperationException(nameInBed & " requires an integer value.")
        End If
        Return CInt(NumberValue)
    End Function

    Public Function AsBoolean(nameInBed As String) As Boolean
        If Kind <> BoardScriptValueKind.BooleanLiteral Then
            Throw New InvalidOperationException(nameInBed & " requires true or false.")
        End If
        Return BooleanValue
    End Function

    Public Overrides Function ToString() As String
        Return TextValue
    End Function
End Class

Public NotInheritable Class BoardScriptPlacement
    Public Property X As Double
    Public Property Y As Double
    Public Property RotationDegrees As Integer
    Public Property Side As String = "top"
End Class

Public NotInheritable Class BoardScriptComponentDefinition
    Public Property RefDes As String = String.Empty
    Public Property PartId As String = String.Empty
    Public Property Placement As BoardScriptPlacement
    Public ReadOnly Property Straps As New Dictionary(Of String, BoardScriptValue)(StringComparer.OrdinalIgnoreCase)
End Class

Public NotInheritable Class BoardScriptEndpoint
    Public Property RefDes As String = String.Empty
    Public Property PinName As String = String.Empty

    Public Overrides Function ToString() As String
        Return RefDes & "." & PinName
    End Function
End Class

Public NotInheritable Class BoardScriptNetDefinition
    Public Property Name As String = String.Empty
    Public ReadOnly Property Endpoints As New List(Of BoardScriptEndpoint)()
End Class

Public NotInheritable Class BoardScriptClockDefinition
    Public Property Name As String = String.Empty
    Public Property FrequencyHz As Double
    Public Property Source As BoardScriptEndpoint
End Class

Public NotInheritable Class BoardScriptPowerDomainDefinition
    Public Property Name As String = String.Empty
    Public Property Voltage As Double
    Public ReadOnly Property Members As New List(Of BoardScriptEndpoint)()
End Class

Public NotInheritable Class BoardScriptProgrammedDeviceDefinition
    Public Property RefDes As String = String.Empty
    Public Property Kind As String = String.Empty
    Public Property PayloadId As String = String.Empty
    Public Property Required As Boolean
End Class

Public NotInheritable Class BoardScriptConstraintDefinition
    Public Property Name As String = String.Empty
    Public ReadOnly Property Values As New Dictionary(Of String, BoardScriptValue)(StringComparer.OrdinalIgnoreCase)
End Class

Public NotInheritable Class BoardScriptBoardDefinition
    Public Property BoardId As String = String.Empty
    Public Property Title As String = String.Empty
    Public Property Units As String = "mm"
    Public Property Width As Double
    Public Property Height As Double
    Public ReadOnly Property Components As New List(Of BoardScriptComponentDefinition)()
    Public ReadOnly Property Nets As New List(Of BoardScriptNetDefinition)()
    Public ReadOnly Property Clocks As New List(Of BoardScriptClockDefinition)()
    Public ReadOnly Property PowerDomains As New List(Of BoardScriptPowerDomainDefinition)()
    Public ReadOnly Property ProgrammedDevices As New List(Of BoardScriptProgrammedDeviceDefinition)()
    Public ReadOnly Property Constraints As New List(Of BoardScriptConstraintDefinition)()
End Class

Public NotInheritable Class BoardScriptDiagnostic
    Public Property IsError As Boolean
    Public Property Message As String = String.Empty

    Public Overrides Function ToString() As String
        Return If(IsError, "ERROR: ", "INFO : ") & Message
    End Function
End Class

Public NotInheritable Class BoardScriptPinMetadata
    Public Property Name As String = String.Empty
    Public Property Number As Integer?
    Public Property Direction As String = String.Empty
End Class

Public NotInheritable Class BoardScriptPartMetadata
    Public Property PartId As String = String.Empty
    Public Property Manufacturer As String = String.Empty
    Public Property PartNumber As String = String.Empty
    Public Property Package As String = String.Empty
    Public Property Role As String = String.Empty
    Public Property WorkerId As String = String.Empty
    Public Property PhysicalPinMapComplete As Boolean
    Public Property FootprintKnown As Boolean
    Public ReadOnly Property Pins As New Dictionary(Of String, BoardScriptPinMetadata)(StringComparer.OrdinalIgnoreCase)
    Public Property PartsMetadataSource As String = String.Empty
    Public Property WorkerMetadataSource As String = String.Empty
End Class

Public Interface IBoardScriptWorker
    ReadOnly Property WorkerId As String
    ReadOnly Property SiliconIdentity As String
    Function DiagnosticText() As String
End Interface

Public NotInheritable Class BoardScriptCompiledComponent
    Public Property Definition As BoardScriptComponentDefinition
    Public Property Metadata As BoardScriptPartMetadata
    Public Property Worker As IBoardScriptWorker
End Class

Public NotInheritable Class BoardScriptCompiledBoard
    Public Property Definition As BoardScriptBoardDefinition
    Public ReadOnly Property Components As New List(Of BoardScriptCompiledComponent)()
    Public ReadOnly Property Diagnostics As New List(Of BoardScriptDiagnostic)()
    Public Property ManufacturingReady As Boolean

    Public ReadOnly Property HasErrors As Boolean
        Get
            For Each diagnosticInBed As BoardScriptDiagnostic In Diagnostics
                If diagnosticInBed.IsError Then Return True
            Next
            Return False
        End Get
    End Property
End Class
