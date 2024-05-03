﻿using Microsoft.CodeAnalysis.Text;
using Syndiesis.Models;
using System;
using System.Diagnostics;
using System.Globalization;

namespace Syndiesis.Utilities;

public sealed class CursoredStringEditor
{
    private readonly MultilineStringEditor _editor = new();

    private LinePosition _cursorPosition;
    private readonly SelectionSpan _selectionSpan = new();

    private int _preferredCursorCharacterIndex;
    private bool _isSelectingText;

    public event Action? CodeChanged;
    public event Action<LinePosition>? CursorMoved;

    public int TabSize { get; set; } = 4;

    public int CursorLineIndex
    {
        get => _cursorPosition.Line;
        set
        {
            _cursorPosition.SetLineIndex(value);
            HandleTriggerCursorMoved();
        }
    }

    public int CursorCharacterIndex
    {
        get => _cursorPosition.Character;
        set
        {
            _cursorPosition.SetCharacterIndex(value);
            HandleTriggerCursorMoved();
        }
    }

    public LinePosition CursorPosition
    {
        get => _cursorPosition;
        set
        {
            _cursorPosition = value;
            HandleTriggerCursorMoved();
        }
    }

    public bool HasSelection => _selectionSpan.HasSelection;

    public LinePositionSpan SelectionLineSpan
    {
        get => _selectionSpan.SelectionPositionSpan;
    }

    public int LineCount
    {
        get => _editor.LineCount;
    }

    public MultilineStringEditor MultilineEditor
    {
        get => _editor;
    }

    #region Selection
    public void SelectAll()
    {
        _selectionSpan.SelectionStart = new(0, 0);
        _selectionSpan.SelectionEnd = LastCharacterPosition();
        _isSelectingText = _selectionSpan.HasSelection;
        TriggerCursorMoved();
    }

    public void SetSelectionMode(bool inRangeSelection)
    {
        _isSelectingText = inRangeSelection;
        if (!inRangeSelection)
        {
            _selectionSpan.SetBoth(_cursorPosition);
        }
    }
    #endregion

    #region Manipulation
    public LinePosition LastCharacterPosition()
    {
        var line = LineCount - 1;
        var lineLength = _editor.LineLength(line);
        var column = lineLength;
        return new(line, column);
    }

    public void SetSource(string source)
    {
        _editor.SetText(source);
        CursorPosition = new(0, 0);
        TriggerCodeChanged();
    }

    public void InsertTab()
    {
        int tabSize = TabSize;
        var column = CursorCharacterIndex;
        int existingInTab = column % tabSize;
        int spacesToInsert = tabSize - existingInTab;
        InsertText(new string(' ', spacesToInsert));
    }

    public void MoveCursorLeftWord()
    {
        var position = LeftmostContiguousCommonCategory();
        CursorPosition = position;
        CapturePreferredCursorCharacter();
    }

    public void MoveCursorNextWord()
    {
        var position = RightmostContiguousCommonCategory();
        CursorPosition = position;
        CapturePreferredCursorCharacter();
    }

    public void MoveCursorLineStart()
    {
        CursorCharacterIndex = 0;
        CapturePreferredCursorCharacter();
    }

    public void MoveCursorLineEnd()
    {
        CursorCharacterIndex = _editor.LineLength(_cursorPosition.Line);
        CapturePreferredCursorCharacter();
    }

    public void MoveCursorDocumentStart()
    {
        CursorPosition = new(0, 0);
        CapturePreferredCursorCharacter();
    }

    public void MoveCursorDocumentEnd()
    {
        CursorLineIndex = _editor.LineCount - 1;
        int lineLength = _editor.LineLength(_cursorPosition.Line);
        CursorCharacterIndex = lineLength;
        CapturePreferredCursorCharacter();
    }

    public void MoveCursorDown()
    {
        MoveCursorToLine(CursorLineIndex + 1);
    }

    public void MoveCursorUp()
    {
        MoveCursorToLine(CursorLineIndex - 1);
    }

    public void MoveCursorToLine(int nextLine)
    {
        GetCurrentTextPosition(out var line, out var column);
        if (line == nextLine)
            return;

        if (nextLine < 0)
            return;

        if (nextLine >= _editor.LineCount)
            return;

        CursorLineIndex = nextLine;
        PlaceCursorCharacterIndexAfterVerticalMovement(column);
    }

    private void PlaceCursorCharacterIndexAfterVerticalMovement(int column)
    {
        var lineLength = _editor.LineLength(CursorLineIndex);
        if (column > lineLength)
        {
            CursorCharacterIndex = lineLength;
        }
        else
        {
            CursorCharacterIndex = Math.Min(_preferredCursorCharacterIndex, lineLength);
        }
    }

    public void MoveCursorLeft()
    {
        if (_cursorPosition.IsStart())
            return;

        GetCurrentTextPosition(out var line, out var column);
        if (column is 0)
        {
            CursorLineIndex = line - 1;
            CursorCharacterIndex = _editor.AtLine(line - 1).Length;
            CapturePreferredCursorCharacter();
            return;
        }

        CursorCharacterIndex--;
        CapturePreferredCursorCharacter();
    }

    public void MoveCursorRight()
    {
        GetCurrentTextPosition(out var line, out var column);
        var lineContent = _editor.AtLine(line);
        int lineLength = lineContent.Length;
        if (column == lineLength)
        {
            if (line == _editor.LineCount - 1)
            {
                // no more text to go to
                return;
            }

            CursorLineIndex = line + 1;
            CursorCharacterIndex = 0;
            CapturePreferredCursorCharacter();
            return;
        }

        CursorCharacterIndex++;
        CapturePreferredCursorCharacter();
    }

    private void HandleTriggerCursorMoved()
    {
        HandleCursorMoved();
        TriggerCursorMoved();
    }

    private void HandleCursorMoved()
    {
        if (_isSelectingText)
        {
            _selectionSpan.SelectionEnd = _cursorPosition;
        }
        else
        {
            _selectionSpan.SetBoth(_cursorPosition);
        }
    }

    private void TriggerCodeChanged()
    {
        CodeChanged?.Invoke();
    }

    private void TriggerCursorMoved()
    {
        CursorMoved?.Invoke(CursorPosition);
    }

    public string GetCurrentSelectionString()
    {
        return _editor.SectionString(_selectionSpan.SelectionPositionSpan);
    }

    public void DeleteCurrentSelection()
    {
        if (!_isSelectingText || !_selectionSpan.HasSelection)
        {
            return;
        }

        var span = _selectionSpan.SelectionPositionSpan;
        var start = span.Start;
        var end = span.End;

        _editor.RemoveRange(start.Line, end.Line, start.Character, end.Character);

        CursorPosition = start;
        SetSelectionMode(false);
        TriggerCodeChanged();
    }

    public void InsertLine()
    {
        DeleteCurrentSelection();
        GetCurrentTextPosition(out int line, out int column);
        _editor.InsertLineAtColumn(line, column);
        CursorPosition = new(CursorLineIndex + 1, 0);
        CapturePreferredCursorCharacter();
        TriggerCodeChanged();
    }

    public void DeleteCurrentCharacterBackwards()
    {
        DeleteCurrentSelection();

        if (_cursorPosition.IsStart())
            return;

        GetCurrentTextPosition(out int line, out int column);
        _editor.RemoveBackwardsAt(line, column, 1);
        MoveCursorLeft();
        CapturePreferredCursorCharacter();
        TriggerCodeChanged();
    }

    public void DeleteCurrentCharacterForwards()
    {
        DeleteCurrentSelection();

        GetCurrentTextPosition(out int line, out int column);
        int lastLine = _editor.LineCount - 1;
        var lastLineLength = _editor.LineLength(lastLine);
        if (line == lastLine && column == lastLineLength)
            return;

        _editor.RemoveForwardsAt(line, column, 1);
        TriggerCodeChanged();
    }

    public void DeleteCommonCharacterGroupBackwards()
    {
        DeleteCurrentSelection();

        if (_cursorPosition.IsStart())
            return;

        GetCurrentTextPosition(out int line, out int column);
        if ((line, column) is ( > 0, 0))
        {
            DeleteCurrentSelection();
            DeleteCurrentCharacterBackwards();
            return;
        }

        int previousColumn = column - 1;
        var start = LeftmostContiguousCommonCategory().Character;
        var lineContents = _editor.AtLine(line);
        var startChar = lineContents[start];
        var endChar = lineContents[previousColumn];
        bool coveringWhitespace =
            EditorCategory(startChar) is TextEditorCharacterCategory.Whitespace
            && EditorCategory(endChar) is TextEditorCharacterCategory.Whitespace;
        if (!coveringWhitespace)
        {
            // avoid removing the left whitespace
            var rightmostWhitespace = RightmostWhitespaceInCurrentLine(line, start);
            start = rightmostWhitespace.Character;
        }

        _editor.RemoveRangeInLine(line, start, previousColumn);
        CursorCharacterIndex = start;
        CapturePreferredCursorCharacter();
        TriggerCodeChanged();
    }

    public void DeleteCommonCharacterGroupForwards()
    {
        DeleteCurrentSelection();

        GetCurrentTextPosition(out int line, out int column);

        if (line >= _editor.LineCount)
            return;

        var currentLine = _editor.AtLine(line);

        if (column >= currentLine.Length)
        {
            if (line == _editor.LineCount - 1)
            {
                return;
            }

            _editor.RemoveNewLineIntoBelow(line);
            TriggerCodeChanged();
            return;
        }

        var end = RightmostContiguousCommonCategory().Character;
        if (end < currentLine.Length)
        {
            var leftmostWhitespace = LeftmostWhitespaceInCurrentLine(line, end);
            end = leftmostWhitespace.Character;
        }

        _editor.RemoveRangeInLine(line, column, end - 1);
        TriggerCodeChanged();
    }

    private void CapturePreferredCursorCharacter()
    {
        _preferredCursorCharacterIndex = CursorCharacterIndex;
    }

    public void InsertText(string? text)
    {
        DeleteCurrentSelection();

        if (string.IsNullOrEmpty(text))
            return;

        GetCurrentTextPosition(out int line, out int column);
        var nextCursorPosition = _editor.InsertAt(line, column, text);
        CursorPosition = nextCursorPosition;
        TriggerCodeChanged();
    }

    private LinePosition LeftmostContiguousCommonCategory()
    {
        // we assume that the caller has sanitized the positions

        var (line, column) = _cursorPosition;
        Debug.Assert(line >= 0 && line < _editor.LineCount);
        var lineLength = _editor.LineLength(line);

        Debug.Assert(column >= 0 && column <= lineLength);

        bool hasConsumedWhitespace = false;
        if (column is 0)
        {
            if (line is 0)
            {
                return new(0, 0);
            }

            line--;
            column = _editor.LineLength(line);
            hasConsumedWhitespace = true;
        }

        int leftmost = column - 1;
        var lineContent = _editor.AtLine(line);

        if (lineContent.Length is 0)
            return new(line, 0);

        var targetCategory = TextEditorCharacterCategory.Whitespace;
        var firstCharacter = lineContent[leftmost];
        var previousCategory = EditorCategory(firstCharacter);

        while (leftmost >= 0)
        {
            var c = lineContent[leftmost];

            bool include = EvaluateContiguousCharacter(
                c,
                ref hasConsumedWhitespace,
                ref targetCategory,
                ref previousCategory);
            if (!include)
                break;

            leftmost--;
        }

        return new(line, leftmost + 1);
    }

    // copy-pasting this, although ugly, seems to be better in terms of
    // guaranteeing flexibility and maintainability in this particular case
    private LinePosition RightmostContiguousCommonCategory()
    {
        // we assume that the caller has sanitized the positions

        var (line, column) = _cursorPosition;
        Debug.Assert(line >= 0 && line < _editor.LineCount);
        var lineLength = _editor.LineLength(line);

        Debug.Assert(column >= 0 && column <= lineLength);

        bool hasConsumedWhitespace = false;
        if (column == lineLength)
        {
            if (line == _editor.LineCount - 1)
            {
                return _cursorPosition;
            }

            line++;
            column = 0;
            hasConsumedWhitespace = true;
        }

        int rightmost = column;
        var lineContent = _editor.AtLine(line);

        if (lineContent.Length is 0)
            return new(line, 0);

        var targetCategory = TextEditorCharacterCategory.Whitespace;
        var firstCharacter = lineContent[rightmost];
        var previousCategory = EditorCategory(firstCharacter);

        while (rightmost < lineContent.Length)
        {
            var c = lineContent[rightmost];

            bool include = EvaluateContiguousCharacter(
                c,
                ref hasConsumedWhitespace,
                ref targetCategory,
                ref previousCategory);
            if (!include)
                break;

            rightmost++;
        }

        return new(line, rightmost);
    }

    private static bool EvaluateContiguousCharacter(
        char c,
        ref bool hasConsumedWhitespace,
        ref TextEditorCharacterCategory targetCategory,
        ref TextEditorCharacterCategory previousCategory)
    {
        var category = EditorCategory(c);

        if (category is TextEditorCharacterCategory.Whitespace)
        {
            if (previousCategory is not TextEditorCharacterCategory.Whitespace)
            {
                if (hasConsumedWhitespace)
                    return false;
            }

            hasConsumedWhitespace = true;
        }
        else
        {
            if (category != previousCategory)
            {
                if (previousCategory is TextEditorCharacterCategory.Whitespace)
                {
                    if (hasConsumedWhitespace &&
                        targetCategory is not TextEditorCharacterCategory.Whitespace)
                    {
                        return false;
                    }
                }
            }

            // try to determine what char category we are seeking for
            if (targetCategory is TextEditorCharacterCategory.Whitespace)
            {
                targetCategory = category;
            }

            if (category != targetCategory)
            {
                return false;
            }
        }

        previousCategory = category;

        return true;
    }

    private LinePosition LeftmostWhitespaceInCurrentLine()
    {
        GetCurrentTextPosition(out int line, out int column);
        return RightmostWhitespaceInCurrentLine(line, column);
    }

    private LinePosition LeftmostWhitespaceInCurrentLine(int line, int column)
    {
        var currentLine = _editor.AtLine(line);
        int next = column;
        while (next > 0)
        {
            var c = currentLine[next];
            if (!char.IsWhiteSpace(c))
            {
                break;
            }

            column = next;
            next--;
        }

        return new(line, column);
    }

    private LinePosition RightmostWhitespaceInCurrentLine()
    {
        GetCurrentTextPosition(out int line, out int column);
        return RightmostWhitespaceInCurrentLine(line, column);
    }

    private LinePosition RightmostWhitespaceInCurrentLine(int line, int column)
    {
        var currentLine = _editor.AtLine(line);
        var currentLength = currentLine.Length;
        while (column < currentLength - 1)
        {
            var c = currentLine[column];
            if (!char.IsWhiteSpace(c))
            {
                break;
            }

            column++;
        }

        return new(line, column);
    }

    public static TextEditorCharacterCategory EditorCategory(char c)
    {
        if (c is '_')
            return TextEditorCharacterCategory.Identifier;

        var unicode = char.GetUnicodeCategory(c);
        switch (unicode)
        {
            case UnicodeCategory.DecimalDigitNumber:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.UppercaseLetter:
                return TextEditorCharacterCategory.Identifier;

            case UnicodeCategory.SpaceSeparator:
                return TextEditorCharacterCategory.Whitespace;
        }

        return TextEditorCharacterCategory.General;
    }

    private void GetCurrentTextPosition(out int line, out int column)
    {
        line = _cursorPosition.Line;
        column = _cursorPosition.Character;
    }
    #endregion
}