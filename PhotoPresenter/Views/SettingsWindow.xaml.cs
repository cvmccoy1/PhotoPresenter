using System.Windows;
using System.Windows.Controls;
using PhotoPresenter.Services;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        InitThemeComboBox();
        InitTextComboBox();
        InitAutoplayIntervalComboBox();
        ThemeComboBox.SelectionChanged           += ThemeComboBox_SelectionChanged;
        TextComboBox.SelectionChanged            += TextComboBox_SelectionChanged;
        AutoplayIntervalComboBox.SelectionChanged += AutoplayIntervalComboBox_SelectionChanged;
    }

    private void InitThemeComboBox()
    {
        var theme = UserSettings.Load().Theme ?? "Light";
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if ((string)item.Tag == theme) { ThemeComboBox.SelectedItem = item; return; }
        }
        ThemeComboBox.SelectedIndex = 0;
    }

    private void InitTextComboBox()
    {
        var size = UserSettings.Load().TextSize ?? "Normal";
        foreach (ComboBoxItem item in TextComboBox.Items)
        {
            if ((string)item.Tag == size) { TextComboBox.SelectedItem = item; return; }
        }
        TextComboBox.SelectedIndex = 1;
    }

    private void InitAutoplayIntervalComboBox()
    {
        var seconds = _vm.PresentVM.AutoplayIntervalSeconds.ToString();
        foreach (ComboBoxItem item in AutoplayIntervalComboBox.Items)
        {
            if ((string)item.Tag == seconds) { AutoplayIntervalComboBox.SelectedItem = item; return; }
        }
        AutoplayIntervalComboBox.SelectedIndex = 2; // "5 seconds"
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is not ComboBoxItem item) return;
        var themeName = (string)item.Tag;
        ThemeService.ApplyColor(themeName);
        var settings = UserSettings.Load();
        settings.Theme = themeName;
        settings.Save();
    }

    private void TextComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextComboBox.SelectedItem is not ComboBoxItem item) return;
        var textSize = (string)item.Tag;
        ThemeService.ApplyTextSize(textSize);
        var settings = UserSettings.Load();
        settings.TextSize = textSize;
        settings.Save();
    }

    private void AutoplayIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AutoplayIntervalComboBox.SelectedItem is not ComboBoxItem item) return;
        _vm.PresentVM.AutoplayIntervalSeconds = int.Parse((string)item.Tag);
    }
}
