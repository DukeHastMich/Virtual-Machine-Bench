Imports System
Imports System.Globalization

Public NotInheritable Class BoardScriptParser
    Private ReadOnly _lexer As BoardScriptLexer
    Private _current As BoardScriptToken

    Public Sub New(sourceInBed As String)
        _lexer = New BoardScriptLexer(sourceInBed)
        _current = _lexer.NextToken()
    End Sub

    Public Function ParseBoard() As BoardScriptBoardDefinition
        ExpectKeyword("board")
        Dim resultInBed As New BoardScriptBoardDefinition With {.BoardId = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.LeftBrace)

        While _current.Kind <> BoardScriptTokenKind.RightBrace
            If _current.Kind = BoardScriptTokenKind.EndOfFile Then Throw ErrorInBed("Expected '}' before end of file.")
            Select Case _current.Text.ToLowerInvariant()
                Case "title"
                    Advance()
                    resultInBed.Title = Expect(BoardScriptTokenKind.StringLiteral).Text
                    Expect(BoardScriptTokenKind.Semicolon)
                Case "units"
                    Advance()
                    resultInBed.Units = ExpectIdentifier()
                    Expect(BoardScriptTokenKind.Semicolon)
                Case "outline"
                    Advance()
                    resultInBed.Width = ExpectNumber()
                    resultInBed.Height = ExpectNumber()
                    Expect(BoardScriptTokenKind.Semicolon)
                Case "component"
                    resultInBed.Components.Add(ParseComponent())
                Case "net"
                    resultInBed.Nets.Add(ParseNet())
                Case "clock"
                    resultInBed.Clocks.Add(ParseClock())
                Case "power"
                    resultInBed.PowerDomains.Add(ParsePowerDomain())
                Case "program"
                    resultInBed.ProgrammedDevices.Add(ParseProgrammedDevice())
                Case "constraint"
                    resultInBed.Constraints.Add(ParseConstraint())
                Case Else
                    Throw ErrorInBed("Unknown board statement '" & _current.Text & "'.")
            End Select
        End While
        Expect(BoardScriptTokenKind.RightBrace)
        Expect(BoardScriptTokenKind.EndOfFile)
        If String.IsNullOrWhiteSpace(resultInBed.Title) Then resultInBed.Title = resultInBed.BoardId
        Return resultInBed
    End Function

    Private Function ParseComponent() As BoardScriptComponentDefinition
        ExpectKeyword("component")
        Dim resultInBed As New BoardScriptComponentDefinition With {.RefDes = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.Colon)
        resultInBed.PartId = ExpectIdentifier()
        Expect(BoardScriptTokenKind.LeftBrace)

        While _current.Kind <> BoardScriptTokenKind.RightBrace
            Select Case _current.Text.ToLowerInvariant()
                Case "place"
                    Advance()
                    Dim placementInBed As New BoardScriptPlacement()
                    placementInBed.X = ExpectNumber()
                    placementInBed.Y = ExpectNumber()
                    ExpectKeyword("rot")
                    placementInBed.RotationDegrees = CInt(ExpectNumber())
                    ExpectKeyword("side")
                    placementInBed.Side = ExpectIdentifier()
                    Expect(BoardScriptTokenKind.Semicolon)
                    resultInBed.Placement = placementInBed
                Case "strap"
                    Advance()
                    Dim nameInBed As String = ExpectIdentifier()
                    Expect(BoardScriptTokenKind.Equals)
                    resultInBed.Straps(nameInBed) = ParseValue()
                    Expect(BoardScriptTokenKind.Semicolon)
                Case Else
                    Throw ErrorInBed("Unknown component statement '" & _current.Text & "'.")
            End Select
        End While
        Expect(BoardScriptTokenKind.RightBrace)
        Return resultInBed
    End Function

    Private Function ParseNet() As BoardScriptNetDefinition
        ExpectKeyword("net")
        Dim resultInBed As New BoardScriptNetDefinition With {.Name = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.LeftBrace)
        While _current.Kind <> BoardScriptTokenKind.RightBrace
            ExpectKeyword("endpoint")
            Dim endpointInBed As New BoardScriptEndpoint With {.RefDes = ExpectIdentifier()}
            Expect(BoardScriptTokenKind.Dot)
            endpointInBed.PinName = ExpectIdentifier()
            Expect(BoardScriptTokenKind.Semicolon)
            resultInBed.Endpoints.Add(endpointInBed)
        End While
        Expect(BoardScriptTokenKind.RightBrace)
        Return resultInBed
    End Function

    Private Function ParseEndpoint() As BoardScriptEndpoint
        Dim resultInBed As New BoardScriptEndpoint With {.RefDes = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.Dot)
        resultInBed.PinName = ExpectIdentifier()
        Return resultInBed
    End Function

    Private Function ParseClock() As BoardScriptClockDefinition
        ExpectKeyword("clock")
        Dim resultInBed As New BoardScriptClockDefinition With {.Name = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.LeftBrace)
        While _current.Kind <> BoardScriptTokenKind.RightBrace
            Select Case _current.Text.ToLowerInvariant()
                Case "frequency"
                    Advance() : Dim valueInBed As Double = ExpectNumber() : Dim unitInBed As String = ExpectIdentifier().ToLowerInvariant()
                    Select Case unitInBed
                        Case "hz" : resultInBed.FrequencyHz = valueInBed
                        Case "khz" : resultInBed.FrequencyHz = valueInBed * 1000.0
                        Case "mhz" : resultInBed.FrequencyHz = valueInBed * 1000000.0
                        Case Else : Throw ErrorInBed("Clock frequency unit must be Hz, kHz, or MHz.")
                    End Select
                    Expect(BoardScriptTokenKind.Semicolon)
                Case "source"
                    Advance() : resultInBed.Source = ParseEndpoint() : Expect(BoardScriptTokenKind.Semicolon)
                Case Else : Throw ErrorInBed("Unknown clock statement '" & _current.Text & "'.")
            End Select
        End While
        Expect(BoardScriptTokenKind.RightBrace) : Return resultInBed
    End Function

    Private Function ParsePowerDomain() As BoardScriptPowerDomainDefinition
        ExpectKeyword("power")
        Dim resultInBed As New BoardScriptPowerDomainDefinition With {.Name = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.LeftBrace)
        While _current.Kind <> BoardScriptTokenKind.RightBrace
            Select Case _current.Text.ToLowerInvariant()
                Case "voltage"
                    Advance() : resultInBed.Voltage = ExpectNumber() : ExpectKeyword("v") : Expect(BoardScriptTokenKind.Semicolon)
                Case "member"
                    Advance() : resultInBed.Members.Add(ParseEndpoint()) : Expect(BoardScriptTokenKind.Semicolon)
                Case Else : Throw ErrorInBed("Unknown power statement '" & _current.Text & "'.")
            End Select
        End While
        Expect(BoardScriptTokenKind.RightBrace) : Return resultInBed
    End Function

    Private Function ParseProgrammedDevice() As BoardScriptProgrammedDeviceDefinition
        ExpectKeyword("program")
        Dim resultInBed As New BoardScriptProgrammedDeviceDefinition With {.RefDes = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.LeftBrace)
        While _current.Kind <> BoardScriptTokenKind.RightBrace
            Select Case _current.Text.ToLowerInvariant()
                Case "kind" : Advance() : resultInBed.Kind = ExpectIdentifier() : Expect(BoardScriptTokenKind.Semicolon)
                Case "payload" : Advance() : resultInBed.PayloadId = Expect(BoardScriptTokenKind.StringLiteral).Text : Expect(BoardScriptTokenKind.Semicolon)
                Case "required" : Advance() : resultInBed.Required = ParseValue().AsBoolean("program required") : Expect(BoardScriptTokenKind.Semicolon)
                Case Else : Throw ErrorInBed("Unknown program statement '" & _current.Text & "'.")
            End Select
        End While
        Expect(BoardScriptTokenKind.RightBrace) : Return resultInBed
    End Function

    Private Function ParseConstraint() As BoardScriptConstraintDefinition
        ExpectKeyword("constraint")
        Dim resultInBed As New BoardScriptConstraintDefinition With {.Name = ExpectIdentifier()}
        Expect(BoardScriptTokenKind.LeftBrace)
        While _current.Kind <> BoardScriptTokenKind.RightBrace
            Dim keyInBed As String = ExpectIdentifier() : Expect(BoardScriptTokenKind.Equals)
            resultInBed.Values(keyInBed) = ParseValue() : Expect(BoardScriptTokenKind.Semicolon)
        End While
        Expect(BoardScriptTokenKind.RightBrace) : Return resultInBed
    End Function

    Private Function ParseValue() As BoardScriptValue
        Select Case _current.Kind
            Case BoardScriptTokenKind.StringLiteral
                Dim textInBed As String = _current.Text
                Advance()
                Return BoardScriptValue.FromText(textInBed)
            Case BoardScriptTokenKind.Number
                Return BoardScriptValue.FromNumber(ExpectNumber())
            Case BoardScriptTokenKind.Identifier
                If String.Equals(_current.Text, "true", StringComparison.OrdinalIgnoreCase) Then
                    Advance()
                    Return BoardScriptValue.FromBoolean(True)
                End If
                If String.Equals(_current.Text, "false", StringComparison.OrdinalIgnoreCase) Then
                    Advance()
                    Return BoardScriptValue.FromBoolean(False)
                End If
                Dim identifierInBed As String = _current.Text
                Advance()
                Return BoardScriptValue.FromText(identifierInBed)
            Case Else
                Throw ErrorInBed("Expected a string, number, true, false, or identifier value.")
        End Select
    End Function

    Private Function ExpectNumber() As Double
        Dim tokenInBed As BoardScriptToken = Expect(BoardScriptTokenKind.Number)
        Dim resultInBed As Double
        If Not Double.TryParse(tokenInBed.Text, NumberStyles.Float, CultureInfo.InvariantCulture, resultInBed) Then
            Throw New BoardScriptParseException("Invalid number '" & tokenInBed.Text & "'.", tokenInBed)
        End If
        Return resultInBed
    End Function

    Private Function ExpectIdentifier() As String
        Return Expect(BoardScriptTokenKind.Identifier).Text
    End Function

    Private Sub ExpectKeyword(keywordInBed As String)
        If _current.Kind <> BoardScriptTokenKind.Identifier OrElse
           Not String.Equals(_current.Text, keywordInBed, StringComparison.OrdinalIgnoreCase) Then
            Throw ErrorInBed("Expected '" & keywordInBed & "'.")
        End If
        Advance()
    End Sub

    Private Function Expect(kindInBed As BoardScriptTokenKind) As BoardScriptToken
        If _current.Kind <> kindInBed Then Throw ErrorInBed("Expected " & kindInBed.ToString() & ".")
        Dim resultInBed As BoardScriptToken = _current
        Advance()
        Return resultInBed
    End Function

    Private Sub Advance()
        _current = _lexer.NextToken()
    End Sub

    Private Function ErrorInBed(messageInBed As String) As BoardScriptParseException
        Return New BoardScriptParseException(messageInBed, _current)
    End Function
End Class
