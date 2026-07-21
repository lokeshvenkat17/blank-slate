using Avalonia.Controls;
using BlankSlate.ViewModels;

namespace BlankSlate.Views;

public partial class EditorView : UserControl, IEditorHandle
{
    public EditorView()
    {
        InitializeComponent();

        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretPosition();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DocumentViewModel vm)
            {
                vm.EditorHandle = this;
                UpdateCaretPosition();
            }
        };
        AttachedToVisualTree += (_, _) => Editor.Focus();
    }

    private void UpdateCaretPosition()
    {
        if (DataContext is not DocumentViewModel vm)
            return;
        vm.CaretLine = Editor.TextArea.Caret.Line;
        vm.CaretColumn = Editor.TextArea.Caret.Column;
    }

    public void Undo() => Editor.Undo();
    public void Redo() => Editor.Redo();
    public void Cut() => Editor.Cut();
    public void Copy() => Editor.Copy();
    public void Paste() => Editor.Paste();
    public void SelectAll() => Editor.SelectAll();
}
