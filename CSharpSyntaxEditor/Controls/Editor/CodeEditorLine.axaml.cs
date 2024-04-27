using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CSharpSyntaxEditor.Models;
using CSharpSyntaxEditor.Utilities;
using System;

namespace CSharpSyntaxEditor.Controls;

public partial class CodeEditorLine : UserControl
{
    private static readonly SolidColorBrush _selectedLineBackgroundBrush = new(0x80102020);
    private static readonly SolidColorBrush _unselectedLineBackgroundBrush = new(Colors.Transparent);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<CodeEditorLine, string>(nameof(Text), defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set
        {
            SetValue(TextProperty, value);
            Inlines = [new Run(value)];
        }
    }

    public static readonly StyledProperty<bool> SelectedLineProperty =
        AvaloniaProperty.Register<CodeEditorLine, bool>(nameof(SelectedLine), defaultValue: false);

    public bool SelectedLine
    {
        get => GetValue(SelectedLineProperty);
        set
        {
            SetValue(SelectedLineProperty, value);
            var backgroundBrush = value
                ? _selectedLineBackgroundBrush
                : _unselectedLineBackgroundBrush;

            lineContentPanel.Background = backgroundBrush;
            cursor.SetVisible(value);
        }
    }

    public static readonly StyledProperty<InlineCollection> InlinesProperty =
        AvaloniaProperty.Register<CodeEditorLine, InlineCollection>(nameof(Inlines), defaultValue: []);

    public InlineCollection? Inlines
    {
        get => lineContentText.Inlines;
        set
        {
            SetValue(InlinesProperty!, value!);
            lineContentText.Inlines = value;
        }
    }

    public static readonly StyledProperty<int> CursorCharacterIndexProperty =
        AvaloniaProperty.Register<CodeEditorLine, int>(nameof(CursorCharacterIndex), defaultValue: 0);

    public int CursorCharacterIndex
    {
        get => GetValue(CursorCharacterIndexProperty);
        set
        {
            SetValue(CursorCharacterIndexProperty, value);
            double newLeftPosition = value * CodeEditor.CharWidth + 1;
            cursor.LeftOffset = newLeftPosition;
        }
    }

    public CodeEditorLine()
    {
        InitializeComponent();
    }

    // this is ready to be used for whenever syntax highlighting is implemented
    public void HighlightText(ReadOnlySpan<LineHighlightRange> sortedHighlights)
    {
        var runs = new InlineCollection();
        int firstUnhandledIndex = 0;
        var text = Text;
        foreach (var highlight in sortedHighlights)
        {
            int start = highlight.Start;
            if (start > firstUnhandledIndex)
            {
                var intermediateSubstring = text[firstUnhandledIndex..start];
                var intermediateRun = new Run(intermediateSubstring);
                runs.Add(intermediateRun);
            }

            int end = highlight.End;
            var substring = text[start..end];
            var highlightRun = new Run(substring)
            {
                Foreground = new SolidColorBrush(highlight.Highlight),
            };

            runs.Add(highlightRun);
        }

        lineContentText.Inlines = runs;
    }

    public void RestartCursorAnimation()
    {
        cursor.RunAnimation();
    }

    public void HideCursor()
    {
        cursor.Hide();
    }

    public void ShowCursor()
    {
        cursor.Show();
    }

    public void StopCursorAnimation()
    {
        cursor.StopAnimation();
    }

    public void SetStaticCursorColor(Color color)
    {
        cursor.SetStaticColor(color);
    }
}
