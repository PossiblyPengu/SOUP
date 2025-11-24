using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace BusinessToolsSuite.Shared.Controls;

public partial class InAppDialogHost : UserControl
{
    private TaskCompletionSource<object?>? _tcs;

    public static InAppDialogHost? Instance { get; private set; }

    public bool IsOpen { get; private set; }

    public InAppDialogHost()
    {
        Instance = this;
    }

    public Task<T?> ShowDialogAsync<T>(Control content)
    {
        if (content == null) return Task.FromResult<T?>(default);
        _tcs = new TaskCompletionSource<object?>();

        // set presenter content on UI thread
        Dispatcher.UIThread.Post(() =>
        {
            var host = this.FindControl<ContentControl>("PART_DialogHost");
            if (host != null)
            {
                host.Content = content;
            }
            IsOpen = true;
            InvalidateVisual();
        });

        return _tcs.Task.ContinueWith(t => (T?)t.Result);
    }

    public void CloseDialog(object? result)
    {
        if (_tcs == null) return;
        var tcs = _tcs;
        _tcs = null;

        Dispatcher.UIThread.Post(() =>
        {
            var host = this.FindControl<ContentControl>("PART_DialogHost");
            if (host != null)
            {
                host.Content = null;
            }
            IsOpen = false;
            InvalidateVisual();
        });

        tcs.TrySetResult(result);
    }
}
