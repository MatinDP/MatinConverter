using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Threading;

namespace MatinConverter.Windows;

public sealed class QuickConversionBroker(App app) : IDisposable
{
    private static string Name => "MatinConverter.Quick." + WindowsIdentity.GetCurrent().User!.Value;
    private readonly Channel<ConversionRequest> queue = Channel.CreateBounded<ConversionRequest>(new BoundedChannelOptions(32) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly CancellationTokenSource stop = new();
    private Mutex? mutex; private bool ownsMutex; private DispatcherTimer? idleTimer; private bool busy; private long touched = Environment.TickCount64;
    public void Touch() => Interlocked.Exchange(ref touched, Environment.TickCount64);
    public bool Enqueue(string[] files)
    {
        var safe = files.Where(p => Path.IsPathFullyQualified(p) && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10000).ToArray();
        if (safe.Length == 0)
            return false;
        Touch();
        return queue.Writer.TryWrite(ConversionRequest.Capture(safe, App.SettingsStore.Load()));
    }
    public bool Start(string[] initial)
    {
        mutex = new Mutex(false, @"Local\" + Name);
        try
        {
            ownsMutex = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException) { ownsMutex = true; }
        if (!ownsMutex)
        {
            if (initial.Length > 0)
                SendAsync(initial).GetAwaiter().GetResult();
            return false;
        }
        app.Dispatcher.BeginInvoke(new Action(() => { _ = ListenAsync(); _ = ProcessAsync(); }));
        if (initial.Length > 0)
            Enqueue(initial);
        idleTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) =>
        {
            if (!busy && !queue.Reader.TryPeek(out _) && Environment.TickCount64 - Interlocked.Read(ref touched) > 20000)
                app.Shutdown();
        }, app.Dispatcher);
        return true;
    }
    private static async Task SendAsync(string[] files)
    {
        using var client = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(10000).ConfigureAwait(false);
        await using var writer = new StreamWriter(client, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(files)).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
        using var reader = new StreamReader(client, leaveOpen: true);
        if (await reader.ReadLineAsync().ConfigureAwait(false) != "OK")
            throw new InvalidOperationException("The conversion queue is full. Please retry shortly.");
    }
    private async Task ListenAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(stop.Token);
                using var reader = new StreamReader(server, leaveOpen: true);
                var json = await reader.ReadLineAsync(stop.Token);
                var accepted = json is { Length: < 4_000_000 } && JsonSerializer.Deserialize<string[]>(json) is { } paths && Enqueue(paths);
                await using var writer = new StreamWriter(server, leaveOpen: true);
                await writer.WriteLineAsync(accepted ? "OK" : "BUSY");
                await writer.FlushAsync();
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) { AppLog.Write("pipe", ex.Message); }
        }
    }
    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var request in queue.Reader.ReadAllAsync(stop.Token))
            {
                busy = true;
                try
                {
                    var settings = request.Settings;
                    await App.RunJobAsync(request.Files.ToArray(), settings.EffectiveQuick(), settings, true);
                }
                catch (Exception ex) { AppLog.Write("quick-job", ex.ToString()); MessageBox.Show(ex.Message, L.T("Error")); }
                finally { busy = false; Touch(); }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
    public void Dispose()
    {
        idleTimer?.Stop();
        stop.Cancel();
        queue.Writer.TryComplete();
        if (ownsMutex)
            mutex?.ReleaseMutex();
        mutex?.Dispose();
        stop.Dispose();
    }
}
