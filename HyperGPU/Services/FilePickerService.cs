using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;

namespace HyperGPU.Services;

public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickIsoFileAsync(CancellationToken cancellationToken)
    {
        return await InvokeOnWindowThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileOpenPicker picker = new(GetWindowId())
            {
                CommitButtonText = "Select ISO",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List,
            };
            picker.FileTypeFilter.Add(".iso");

            PickFileResult? file = await picker.PickSingleFileAsync().AsTask(cancellationToken);
            return file?.Path;
        });
    }

    public async Task<string?> PickVhdFolderAsync(CancellationToken cancellationToken)
    {
        return await InvokeOnWindowThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            FolderPicker picker = new(GetWindowId())
            {
                CommitButtonText = "Select folder",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List,
            };

            PickFolderResult? folder = await picker.PickSingleFolderAsync().AsTask(cancellationToken);
            return folder?.Path;
        });
    }

    private static WindowId GetWindowId()
    {
        Microsoft.UI.Xaml.Window? window = App.MainWindow;
        if (window is null)
        {
            throw new InvalidOperationException("Main window is not available for file picker initialization.");
        }

        nint hwnd = WindowNative.GetWindowHandle(window);
        return Win32Interop.GetWindowIdFromWindow(hwnd);
    }

    private static Task<string?> InvokeOnWindowThreadAsync(Func<Task<string?>> callback)
    {
        Microsoft.UI.Xaml.Window? window = App.MainWindow;
        if (window?.DispatcherQueue is null)
        {
            throw new InvalidOperationException("Main window dispatcher is not available for file picker initialization.");
        }

        if (window.DispatcherQueue.HasThreadAccess)
        {
            return callback();
        }

        TaskCompletionSource<string?> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = window.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                taskCompletionSource.SetResult(await callback());
            }
            catch (Exception exception)
            {
                taskCompletionSource.SetException(exception);
            }
        });

        if (!enqueued)
        {
            throw new InvalidOperationException("Unable to access the main window thread for file picker initialization.");
        }

        return taskCompletionSource.Task;
    }
}