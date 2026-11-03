using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MatinConverter.Windows;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Changed(name);
        return true;
    }
}
public sealed class RelayCommand(Action<object?> action, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => action(parameter);
}
public sealed class AsyncCommand(Func<Task> action, Func<bool>? canExecute = null) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;
        running = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await action();
        }
        catch (Exception ex) { AppLog.Write("command", ex.ToString()); MessageBox.Show(ex.Message, L.T("Error"), MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { running = false; CommandManager.InvalidateRequerySuggested(); }
    }
}
