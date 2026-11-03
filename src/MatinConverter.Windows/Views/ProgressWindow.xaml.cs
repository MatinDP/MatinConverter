using System.ComponentModel;
namespace MatinConverter.Windows.Views;

public partial class ProgressWindow : Window
{
    public ProgressViewModel Model
    {
        get;
    }
    public ProgressWindow(int count, CancellationTokenSource cancellation)
    {
        InitializeComponent();
        Model = new(count, cancellation, Close);
        DataContext = Model;
        FlowDirection = L.Language == "fa" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (!Model.Finished)
        {
            e.Cancel = true;
            Model.RequestCancellation();
        }
    }
}
