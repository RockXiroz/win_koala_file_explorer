using System.Windows;
using System.Windows.Input;
using KoalaFileExplorer.Models;
using KoalaFileExplorer.Services;

namespace KoalaFileExplorer.Views;

public partial class KeyBindingsDialog : Window
{
    private readonly KeyBindingsService _service;
    private KeyBindingEntry? _editingEntry;

    public KeyBindingsDialog(KeyBindingsService service)
    {
        InitializeComponent();
        _service = service;
        BindingsGrid.ItemsSource = service.Bindings;
    }

    private void ChangeBtn_Click(object sender, RoutedEventArgs e)
    {
        _editingEntry = (sender as System.Windows.Controls.Button)?.Tag as KeyBindingEntry;
        if (_editingEntry == null) return;
        RecordingText.Text = $"Recording for \"{_editingEntry.Description}\" — press a key (Esc to cancel)";
        RecordingBorder.Visibility = Visibility.Visible;
        Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_editingEntry == null) { base.OnPreviewKeyDown(e); return; }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _editingEntry = null;
            RecordingBorder.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        // Ignore modifier-only keys
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                 or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        _editingEntry.KeyCode = (int)key;
        _editingEntry.ModifierCode = (int)Keyboard.Modifiers;
        _editingEntry = null;
        RecordingBorder.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _service.ResetDefaults();
        BindingsGrid.Items.Refresh();
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _service.Save();
        Close();
    }
}
