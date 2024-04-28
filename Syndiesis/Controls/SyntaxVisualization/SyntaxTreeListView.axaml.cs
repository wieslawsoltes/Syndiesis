using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.CodeAnalysis;
using System;

namespace Syndiesis.Controls;

public partial class SyntaxTreeListView : UserControl
{
    private const double extraScrollHeight = 50;
    private const double extraScrollWidth = 20;

    private bool _allowedHover;
    private SyntaxTreeListNode? _hoveredNode;

    public static readonly StyledProperty<SyntaxTreeListNode> RootNodeProperty =
        AvaloniaProperty.Register<CodeEditorLine, SyntaxTreeListNode>(
            nameof(RootNode),
            defaultValue: new());

    public SyntaxTreeListNode RootNode
    {
        get => GetValue(RootNodeProperty);
        set
        {
            SetValue(RootNodeProperty, value);
            topLevelNodeContent.Content = value;

            value.SetListViewRecursively(this);
            value.SizeChanged += HandleRootNodeSizeAdjusted;
            UpdateRootChanged();
        }
    }

    public SyntaxTree? AnalyzedTree { get; set; }

    private void HandleRootNodeSizeAdjusted(object? sender, SizeChangedEventArgs e)
    {
        UpdateScrollLimits();
    }

    private void UpdateRootChanged()
    {
        RootNode.Loaded += NewRootNodeLoaded;
    }

    private void NewRootNodeLoaded(object? sender, RoutedEventArgs e)
    {
        RootNode.Loaded -= NewRootNodeLoaded;
        UpdateScrollLimits();
        CorrectContainedNodeWidths(Bounds.Size);
    }

    public event Action<SyntaxTreeListNode?>? HoveredNode;

    public SyntaxTreeListView()
    {
        InitializeComponent();
        InitializeEvents();
    }

    private bool _isUpdatingScrollLimits = false;

    private void UpdateScrollLimits()
    {
        var node = RootNode;

        _isUpdatingScrollLimits = true;

        using (verticalScrollBar.BeginUpdateBlock())
        {
            verticalScrollBar.MaxValue = node.Bounds.Height + extraScrollHeight;
            verticalScrollBar.StartPosition = -Canvas.GetTop(topLevelNodeContent);
            verticalScrollBar.EndPosition = verticalScrollBar.StartPosition + contentCanvas.Bounds.Height;
            verticalScrollBar.SetAvailableScrollOnScrollableWindow();
        }

        using (horizontalScrollBar.BeginUpdateBlock())
        {
            horizontalScrollBar.MaxValue = Math.Max(node.Bounds.Width - extraScrollWidth, 0);
            horizontalScrollBar.StartPosition = -Canvas.GetLeft(topLevelNodeContent);
            horizontalScrollBar.EndPosition = horizontalScrollBar.StartPosition + contentCanvas.Bounds.Width;
            horizontalScrollBar.SetAvailableScrollOnScrollableWindow();
        }

        _isUpdatingScrollLimits = false;
    }

    private void InitializeEvents()
    {
        verticalScrollBar.ScrollChanged += OnVerticalScroll;
        horizontalScrollBar.ScrollChanged += OnHorizontalScroll;
    }

    private void OnVerticalScroll()
    {
        if (_isUpdatingScrollLimits)
            return;

        var top = verticalScrollBar.StartPosition;
        Canvas.SetTop(topLevelNodeContent, -top);
        InvalidateArrange();
    }

    private void OnHorizontalScroll()
    {
        if (_isUpdatingScrollLimits)
            return;

        var left = horizontalScrollBar.StartPosition;
        Canvas.SetLeft(topLevelNodeContent, -left);
        InvalidateArrange();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        EvaluateHovering(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        EvaluateHovering(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        const double scrollMultiplier = 50;

        base.OnPointerWheelChanged(e);

        var pointerPosition = e.GetCurrentPoint(this).Position;
        if (!contentCanvasContainer.Bounds.Contains(pointerPosition))
        {
            return;
        }

        double steps = -e.Delta.Y * scrollMultiplier;
        double verticalSteps = steps;
        double horizontalSteps = -e.Delta.X * scrollMultiplier;
        if (horizontalSteps is 0)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                horizontalSteps = verticalSteps;
                verticalSteps = 0;
            }
        }

        verticalScrollBar.Step(verticalSteps);
        horizontalScrollBar.Step(horizontalSteps);
        EvaluateHovering(e);
    }

    private void EvaluateHovering(PointerEventArgs e)
    {
        var pointerPosition = e.GetCurrentPoint(this).Position;
        if (!contentCanvasContainer.Bounds.Contains(pointerPosition))
        {
            _allowedHover = false;
            RootNode.SetHoveringRecursively(false);
        }
        else
        {
            _allowedHover = true;
            RootNode.EvaluateHoveringRecursively(e);
        }
    }

    protected override Size MeasureCore(Size availableSize)
    {
        availableSize = CorrectContainedNodeWidths(availableSize);
        return base.MeasureCore(availableSize);
    }

    protected override void ArrangeCore(Rect finalRect)
    {
        base.ArrangeCore(finalRect);
        UpdateScrollLimits();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        CorrectPositionFromHorizontalScroll(finalSize);
        return base.ArrangeOverride(finalSize);
    }

    private void CorrectPositionFromHorizontalScroll(Size availableSize)
    {
        var offset = -Canvas.GetLeft(topLevelNodeContent);
        var rootWidth = topLevelNodeContent.Bounds.Width;
        // ensure that the width has been initialized
        if (rootWidth is not > 0)
            return;

        var availableRight = rootWidth - offset;
        var scrollBarWidth = verticalScrollBar.Bounds.Width;
        var missing = availableSize.Width - availableRight - scrollBarWidth;
        if (missing > 0)
        {
            var reducedOffset = offset - missing;
            var targetOffset = Math.Min(-reducedOffset, 0);
            Canvas.SetLeft(topLevelNodeContent, targetOffset);
        }
    }

    private Size CorrectContainedNodeWidths(Size availableSize)
    {
        topLevelNodeContent.MinWidth = contentCanvas.Bounds.Width + extraScrollWidth;
        CorrectPositionFromHorizontalScroll(availableSize);
        return availableSize;
    }

    #region Node hovers
    public bool RequestHover(SyntaxTreeListNode node)
    {
        if (!_allowedHover)
            return false;

        return true;
    }

    public bool IsHovered(SyntaxTreeListNode node)
    {
        return _hoveredNode == node;
    }

    public void RemoveHover(SyntaxTreeListNode node)
    {
        if (node != _hoveredNode)
            return;

        _hoveredNode = null;
        node.UpdateHovering(false);
        HoveredNode?.Invoke(null);
    }

    public void OverrideHover(SyntaxTreeListNode node)
    {
        if (_hoveredNode == node)
            return;

        var previousHover = _hoveredNode;
        previousHover?.UpdateHovering(false);
        _hoveredNode = node;
        node.UpdateHovering(true);
        HoveredNode?.Invoke(node);
    }
    #endregion
}