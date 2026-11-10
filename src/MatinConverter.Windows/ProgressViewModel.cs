using System.Windows.Input;
namespace MatinConverter.Windows;

public sealed class ProgressViewModel(int total, CancellationTokenSource cancellation, Action close) : Observable
{
    private string title = L.T("Working"); public string Title
    {
        get => title; set => Set(ref title, value);
    }
    private string current = ""; public string Current
    {
        get => current; set => Set(ref current, value);
    }
    private string summary = ""; public string Summary
    {
        get => summary; set => Set(ref summary, value);
    }
    private int completed; public int Completed
    {
        get => completed; set
        {
            Set(ref completed, value);
            Changed(nameof(Percentage));
            Changed(nameof(PercentLabel));
            Changed(nameof(Count));
            Changed(nameof(Indeterminate));
        }
    }
    public double Percentage => total == 0 ? 0 : 100d * Completed / total;
    public string PercentLabel => $"{Percentage:0}%";
    public string Count => $"{Completed} / {total}";
    public bool Indeterminate => total == 0 && !Finished;
    public bool Finished
    {
        get; private set;
    }
    public string ActionLabel => Finished ? L.T("Close") : cancellation.IsCancellationRequested ? L.T("Canceling") : L.T("Cancel");
    public string Errors { get; private set; } = "";
    public Visibility ErrorVisibility => Errors.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    private IReadOnlyList<string> directories = [];
    public ICommand CancelOrClose => new RelayCommand(_ => { if (Finished) close(); else RequestCancellation(); });
    public ICommand CopyErrors => new RelayCommand(_ => Clipboard.SetText(Errors), _ => Errors.Length > 0);
    public ICommand OpenOutput => new RelayCommand(_ => ShellActions.OpenDirectories(directories), _ => directories.Count > 0);
    public void RequestCancellation()
    {
        cancellation.Cancel();
        Changed(nameof(ActionLabel));
    }
    public void Report(ConversionProgress p)
    {
        total = p.Total;
        Completed = Math.Max(Completed, p.Completed);
        Current = Path.GetFileName(p.Current);
        Summary = $"{p.Succeeded} {L.T("Success")} · {p.Failed} {L.T("Failed")} · {p.Skipped} {L.T("Skipped")}";
    }
    public void Finish(BatchResult result)
    {
        total = result.Files.Count;
        Finished = true;
        Title = L.T(result.Canceled ? "Canceled" : "Done");
        Completed = result.Files.Count(f => !f.Canceled);
        Current = "";
        Summary = $"{result.Succeeded} {L.T("Success")} · {result.Failed} {L.T("Failed")} · {result.Skipped} {L.T("Skipped")}";
        Errors = string.Join(Environment.NewLine + Environment.NewLine, result.Files.Where(f => f.Error is not null).Select(f => $"{Path.GetFileName(f.Source)}: {f.Error}"));
        directories = result.OutputDirectories;
        Changed(nameof(Errors));
        Changed(nameof(ErrorVisibility));
        Changed(nameof(ActionLabel));
        Changed(nameof(Indeterminate));
        CommandManager.InvalidateRequerySuggested();
    }
}
