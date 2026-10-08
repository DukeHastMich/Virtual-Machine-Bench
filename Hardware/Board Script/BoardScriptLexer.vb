Imports System
Imports System.Globalization
Imports System.Text

Public Enum BoardScriptTokenKind
    Identifier = 0
    StringLiteral = 1
    Number = 2
    LeftBrace = 3
    RightBrace = 4
    Colon = 5
    Dot = 6
    Equals = 7
    Semicolon = 8
    EndOfFile = 9
End Enum

Public Structure BoardScriptToken
    Public Kind As BoardScriptTokenKind
    Public Text As String
    Public Line As Integer
    Public Column As Integer

    Public Sub New(kindInBed As BoardScriptTokenKind, textInBed As String, lineInBed As Integer, columnInBed As Integer)
        Kind = kindInBed
        Text = textInBed
        Line = lineInBed
        Column = columnInBed
    End Sub
End Structure

Public NotInheritable Class BoardScriptParseException
    Inherits Exception

    Public Sub New(messageInBed As String, tokenInBed As BoardScriptToken)
        MyBase.New("Board Script line " & tokenInBed.Line & ", column " & tokenInBed.Column & ": " & messageInBed)
    End Sub
End Class

Public NotInheritable Class BoardScriptLexer
    Private ReadOnly _source As String
    Private _position As Integer
    Private _line As Integer = 1
    Private _column As Integer = 1

    Public Sub New(sourceInBed As String)
        _source = If(sourceInBed, String.Empty)
    End Sub

    Public Function NextToken() As BoardScriptToken
        SkipWhitespaceAndComments()
        If _position >= _source.Length Then
            Return New BoardScriptToken(BoardScriptTokenKind.EndOfFile, String.Empty, _line, _column)
        End If

        Dim startLineInBed As Integer = _line
        Dim startColumnInBed As Integer = _column
        Dim chInBed As Char = _source(_position)

        Select Case chInBed
            Case "{"c
                Advance()
                Return New BoardScriptToken(BoardScriptTokenKind.LeftBrace, "{", startLineInBed, startColumnInBed)
            Case "}"c
                Advance()
                Return New BoardScriptToken(BoardScriptTokenKind.RightBrace, "}", startLineInBed, startColumnInBed)
            Case ":"c
                Advance()
                Return New BoardScriptToken(BoardScriptTokenKind.Colon, ":", startLineInBed, startColumnInBed)
            Case "."c
                Advance()
                Return New BoardScriptToken(BoardScriptTokenKind.Dot, ".", startLineInBed, startColumnInBed)
            Case "="c
                Advance()
                Return New BoardScriptToken(BoardScriptTokenKind.Equals, "=", startLineInBed, startColumnInBed)
            Case ";"c
                Advance()
                Return New BoardScriptToken(BoardScriptTokenKind.Semicolon, ";", startLineInBed, startColumnInBed)
            Case """"c
                Return ReadString()
        End Select

        If Char.IsDigit(chInBed) OrElse (chInBed = "-"c AndAlso PeekDigit()) Then Return ReadNumber()
        If IsIdentifierStart(chInBed) Then Return ReadIdentifier()

        Throw New BoardScriptParseException("Unexpected character '" & chInBed & "'.",
                                            New BoardScriptToken(BoardScriptTokenKind.Identifier, chInBed.ToString(), startLineInBed, startColumnInBed))
    End Function

    Private Function ReadString() As BoardScriptToken
        Dim startLineInBed As Integer = _line
        Dim startColumnInBed As Integer = _column
        Advance()
        Dim builderInBed As New StringBuilder()
        While _position < _source.Length
            Dim chInBed As Char = _source(_position)
            If chInBed = """"c Then
                Advance()
                Return New BoardScriptToken(BoardScriptTokenKind.StringLiteral, builderInBed.ToString(), startLineInBed, startColumnInBed)
            End If
            If chInBed = "\"c Then
                Advance()
                If _position >= _source.Length Then Exit While
                Dim escapedInBed As Char = _source(_position)
                Select Case escapedInBed
                    Case "n"c : builderInBed.Append(ControlChars.Lf)
                    Case "r"c : builderInBed.Append(ControlChars.Cr)
                    Case "t"c : builderInBed.Append(ControlChars.Tab)
                    Case """"c : builderInBed.Append(""""c)
                    Case "\"c : builderInBed.Append("\"c)
                    Case Else : builderInBed.Append(escapedInBed)
                End Select
                Advance()
            Else
                builderInBed.Append(chInBed)
                Advance()
            End If
        End While
        Throw New BoardScriptParseException("Unterminated string literal.",
                                            New BoardScriptToken(BoardScriptTokenKind.StringLiteral, builderInBed.ToString(), startLineInBed, startColumnInBed))
    End Function

    Private Function ReadNumber() As BoardScriptToken
        Dim startInBed As Integer = _position
        Dim startLineInBed As Integer = _line
        Dim startColumnInBed As Integer = _column
        If _source(_position) = "-"c Then Advance()
        While _position < _source.Length AndAlso Char.IsDigit(_source(_position))
            Advance()
        End While
        If _position < _source.Length AndAlso _source(_position) = "."c Then
            Advance()
            While _position < _source.Length AndAlso Char.IsDigit(_source(_position))
                Advance()
            End While
        End If
        Return New BoardScriptToken(BoardScriptTokenKind.Number,
                                    _source.Substring(startInBed, _position - startInBed),
                                    startLineInBed,
                                    startColumnInBed)
    End Function

    Private Function ReadIdentifier() As BoardScriptToken
        Dim startInBed As Integer = _position
        Dim startLineInBed As Integer = _line
        Dim startColumnInBed As Integer = _column
        While _position < _source.Length AndAlso IsIdentifierPart(_source(_position))
            Advance()
        End While
        Return New BoardScriptToken(BoardScriptTokenKind.Identifier,
                                    _source.Substring(startInBed, _position - startInBed),
                                    startLineInBed,
                                    startColumnInBed)
    End Function

    Private Sub SkipWhitespaceAndComments()
        Do
            While _position < _source.Length AndAlso Char.IsWhiteSpace(_source(_position))
                Advance()
            End While
            If _position >= _source.Length Then Return

            If _source(_position) = "#"c Then
                SkipToEndOfLine()
                Continue Do
            End If
            If _source(_position) = "/"c AndAlso _position + 1 < _source.Length AndAlso _source(_position + 1) = "/"c Then
                Advance()
                Advance()
                SkipToEndOfLine()
                Continue Do
            End If
            Exit Do
        Loop
    End Sub

    Private Sub SkipToEndOfLine()
        While _position < _source.Length AndAlso _source(_position) <> ControlChars.Lf
            Advance()
        End While
    End Sub

    Private Function PeekDigit() As Boolean
        Return _position + 1 < _source.Length AndAlso Char.IsDigit(_source(_position + 1))
    End Function

    Private Shared Function IsIdentifierStart(chInBed As Char) As Boolean
        Return Char.IsLetter(chInBed) OrElse chInBed = "_"c OrElse chInBed = "$"c
    End Function

    Private Shared Function IsIdentifierPart(chInBed As Char) As Boolean
        Return Char.IsLetterOrDigit(chInBed) OrElse chInBed = "_"c OrElse chInBed = "-"c OrElse chInBed = "$"c
    End Function

    Private Sub Advance()
        If _position >= _source.Length Then Return
        If _source(_position) = ControlChars.Lf Then
            _line += 1
            _column = 1
        Else
            _column += 1
        End If
        _position += 1
    End Sub
End Class
