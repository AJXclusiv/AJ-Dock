using System.Windows;
using System.Windows.Controls;
using AJDock.App.ViewModels;

namespace AJDock.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Section_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton || GeneralPanel is null)
        {
            return;
        }

        GeneralPanel.Visibility = Visibility.Collapsed;
        AppearancePanel.Visibility = Visibility.Collapsed;
        IconsPanel.Visibility = Visibility.Collapsed;
        BehaviorPanel.Visibility = Visibility.Collapsed;
        ShortcutsPanel.Visibility = Visibility.Collapsed;
        AdvancedPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Collapsed;

        var target = radioButton.Uid switch
        {
            "Appearance" => AppearancePanel,
            "Icons" => IconsPanel,
            "Behavior" => BehaviorPanel,
            "Shortcuts" => ShortcutsPanel,
            "Advanced" => AdvancedPanel,
            "About" => AboutPanel,
            _ => GeneralPanel
        };

        target.Visibility = Visibility.Visible;
    }
}
