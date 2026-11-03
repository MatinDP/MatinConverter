namespace MatinConverter.Windows.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        FlowDirection = L.Language == "fa" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ApplyZoom(((MainViewModel)DataContext).Settings.ZoomPercent);
    }
    public void ApplyZoom(int percent) => RootLayout.LayoutTransform = new System.Windows.Media.ScaleTransform(percent / 100d, percent / 100d);
    private async void FilesDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            await ((MainViewModel)DataContext).AddAsync(files);
    }
}
