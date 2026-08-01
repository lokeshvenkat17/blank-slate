using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using BlankSlate.ViewModels;

namespace BlankSlate.Views;

/// <summary>
/// Per-tab container around the primary editor. Adds the split-view clone and the
/// document map on demand, driven by MainViewModel's IsSplitViewActive / IsDocumentMapVisible.
/// </summary>
public partial class DocumentEditorHost : UserControl
{
    private MainViewModel? _mainVm;
    private EditorView? _cloneEditor;
    private GridSplitter? _splitter;
    private bool _minimapSyncing;

    public DocumentEditorHost()
    {
        InitializeComponent();

        Minimap.AddHandler(PointerPressedEvent, OnMinimapPointer, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Minimap.AddHandler(PointerMovedEvent, OnMinimapPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        PrimaryEditor.Editor.TextArea.TextView.ScrollOffsetChanged += OnPrimaryScrollChanged;

        AttachedToVisualTree += (_, _) =>
        {
            if (this.FindAncestorOfType<Window>()?.DataContext is MainViewModel vm)
            {
                _mainVm = vm;
                vm.PropertyChanged += OnMainViewModelChanged;
                ApplyViewState();
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_mainVm is not null)
                _mainVm.PropertyChanged -= OnMainViewModelChanged;
            _mainVm = null;
        };
    }

    private void OnMainViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSplitViewActive) or nameof(MainViewModel.IsDocumentMapVisible))
            ApplyViewState();
    }

    private void ApplyViewState()
    {
        if (_mainVm is null)
            return;
        MinimapPane.IsVisible = _mainVm.IsDocumentMapVisible;
        SetSplit(_mainVm.IsSplitViewActive);
    }

    private void SetSplit(bool active)
    {
        if (active && _cloneEditor is null)
        {
            EditorsGrid.ColumnDefinitions = new ColumnDefinitions("*,4,*");
            _splitter = new GridSplitter { Width = 4 };
            Grid.SetColumn(_splitter, 1);
            _cloneEditor = new EditorView { IsPrimary = false };
            Grid.SetColumn(_cloneEditor, 2);
            EditorsGrid.Children.Add(_splitter);
            EditorsGrid.Children.Add(_cloneEditor);
        }
        else if (!active && _cloneEditor is not null)
        {
            EditorsGrid.Children.Remove(_cloneEditor);
            if (_splitter is not null)
                EditorsGrid.Children.Remove(_splitter);
            _cloneEditor = null;
            _splitter = null;
            EditorsGrid.ColumnDefinitions = new ColumnDefinitions("*");
        }
    }

    // ---- Minimap navigation & scroll sync ----

    private void OnMinimapPointer(object? sender, PointerPressedEventArgs e)
    {
        ScrollMainToMinimapPosition(e.GetPosition(Minimap).Y);
        e.Handled = true;
    }

    private void OnMinimapPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(Minimap).Properties.IsLeftButtonPressed)
        {
            ScrollMainToMinimapPosition(e.GetPosition(Minimap).Y);
            e.Handled = true;
        }
    }

    private void ScrollMainToMinimapPosition(double y)
    {
        var editor = PrimaryEditor.Editor;
        var minimapExtent = Math.Max(1, Minimap.ExtentHeight);
        var visibleRatio = Math.Clamp((y + Minimap.VerticalOffset) / minimapExtent, 0, 1);
        var target = visibleRatio * editor.ExtentHeight - editor.ViewportHeight / 2;
        editor.ScrollToVerticalOffset(Math.Max(0, target));
    }

    private void OnPrimaryScrollChanged(object? sender, EventArgs e)
    {
        if (_minimapSyncing || !MinimapPane.IsVisible)
            return;
        _minimapSyncing = true;
        try
        {
            var editor = PrimaryEditor.Editor;
            var mainScrollable = Math.Max(1, editor.ExtentHeight - editor.ViewportHeight);
            var minimapScrollable = Math.Max(0, Minimap.ExtentHeight - Minimap.ViewportHeight);
            var ratio = Math.Clamp(editor.VerticalOffset / mainScrollable, 0, 1);
            Minimap.ScrollToVerticalOffset(ratio * minimapScrollable);
        }
        finally
        {
            _minimapSyncing = false;
        }
    }
}
