using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media;
using Microsoft.Win32;

namespace MatinConverter.Windows.Views;

public sealed class QualityRangeRule : ValidationRule
{
    public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        => int.TryParse(value as string, out var number) && number is >= 1 and <= 100
            ? ValidationResult.ValidResult : new ValidationResult(false, L.T("QualityRange"));
}

public partial class OptionsEditor : UserControl
{
    public static readonly DependencyProperty SampleSourceProperty = DependencyProperty.Register(
        nameof(SampleSource), typeof(string), typeof(OptionsEditor), new PropertyMetadata(null, (d, _) => ((OptionsEditor)d).ScheduleEstimate()));
    public string? SampleSource { get => (string?)GetValue(SampleSourceProperty); set => SetValue(SampleSourceProperty, value); }
    private readonly DispatcherTimer estimateTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private CancellationTokenSource? estimateCancellation;
    private int estimateGeneration;

    public OptionsEditor()
    {
        InitializeComponent();
        FormatBox.ItemsSource = App.Formats.OutputFormats;
        estimateTimer.Tick += async (_, _) => { estimateTimer.Stop(); await UpdateEstimateAsync(); };
        AddHandler(Slider.ValueChangedEvent, new RoutedPropertyChangedEventHandler<double>((_, _) => ScheduleEstimate()));
        AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => ScheduleEstimate()));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => ScheduleEstimate()));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => ScheduleEstimate()));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => ScheduleEstimate()));
        DataContextChanged += (_, _) => ScheduleEstimate();
        Loaded += (_, _) => { UpdateQuality(); ScheduleEstimate(); };
        Unloaded += (_, _) => StopEstimate();
        IsVisibleChanged += (_, _) => ScheduleEstimate();
    }
    private void FormatChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateQuality();
    }
    private void UpdateQuality()
    {
        if (QualitySlider is null || QualityNumber is null || QualityHint is null) return;
        bool adjustable = FormatBox.SelectedItem is OutputFormat { Quality: true };
        QualitySlider.IsEnabled = QualityNumber.IsEnabled = adjustable;
        QualityHint.SetResourceReference(TextBlock.TextProperty, adjustable ? "QualityHint" : "LosslessHint");
    }

    private void StopEstimate()
    {
        estimateGeneration++;
        estimateTimer.Stop();
        estimateCancellation?.Cancel();
    }

    private void ScheduleEstimate()
    {
        StopEstimate();
        if (EstimateStatus is null || !IsLoaded || !IsVisible) return;
        EstimateSample.Text = Path.GetFileName(SampleSource ?? "");
        EstimateSample.ToolTip = SampleSource;
        if (string.IsNullOrEmpty(SampleSource))
        {
            EstimateStatus.SetResourceReference(TextBlock.TextProperty, "EstimateEmpty");
            return;
        }
        EstimateStatus.SetResourceReference(TextBlock.TextProperty, "EstimateBusy");
        estimateTimer.Start();
    }

    public async Task UpdateEstimateAsync()
    {
        estimateTimer.Stop();
        if (!IsLoaded || !IsVisible || DataContext is not ConversionOptions options || string.IsNullOrEmpty(SampleSource)) return;
        StopEstimate();
        var generation = estimateGeneration;
        using var cancellation = new CancellationTokenSource();
        estimateCancellation = cancellation;
        try
        {
            if (HasValidationError(this)) throw new ArgumentException("Invalid output setting.");
            EstimateStatus.SetResourceReference(TextBlock.TextProperty, "EstimateBusy");
            var bytes = await App.Engine.EstimateSizeAsync(SampleSource, options with { }, cancellation.Token);
            if (generation == estimateGeneration)
            {
                EstimateStatus.Text = bytes >= 1024 * 1024 ? $"≈ {bytes / 1048576d:0.##} MB" : $"≈ {bytes / 1024d:0.#} KB";
                EstimateStatus.FlowDirection = FlowDirection.LeftToRight;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (generation == estimateGeneration)
                EstimateStatus.SetResourceReference(TextBlock.TextProperty, "EstimateError");
            AppLog.Write("estimate", ex.Message);
        }
        finally { if (ReferenceEquals(estimateCancellation, cancellation)) estimateCancellation = null; }
    }

    private static bool HasValidationError(DependencyObject element)
    {
        if (Validation.GetHasError(element)) return true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (HasValidationError(VisualTreeHelper.GetChild(element, i))) return true;
        return false;
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
