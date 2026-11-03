using System.Windows.Controls;
using Microsoft.Win32;

namespace MatinConverter.Windows.Views;

public partial class OptionsEditor : UserControl
{
    public OptionsEditor()
    {
        InitializeComponent();
        FormatBox.ItemsSource = App.Formats.OutputFormats;
    }
    private void FormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QualityPanel is not null)
            QualityPanel.Visibility = FormatBox.SelectedItem is OutputFormat { Quality: true } ? Visibility.Visible : Visibility.Collapsed;
    }
    private void DestinationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomPanel is null)
            return;
        CustomPanel.Visibility = Equals(DestinationBox.SelectedValue, DestinationMode.Custom) ? Visibility.Visible : Visibility.Collapsed;
        SubfolderPanel.Visibility = Equals(DestinationBox.SelectedValue, DestinationMode.Subfolder) ? Visibility.Visible : Visibility.Collapsed;
    }
    private void BrowseFolder(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog();
        if (picker.ShowDialog() == true && DataContext is ConversionOptions options)
        {
            options.OutputDirectory = picker.FolderName;
            var current = DataContext;
            DataContext = null;
            DataContext = current;
        }
    }
}
