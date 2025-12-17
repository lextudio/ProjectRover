using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Microsoft.Win32
{
    internal static class DialogHelper
    {
        public static TopLevel? GetTopLevel(Window? owner)
        {
            if (owner != null) return TopLevel.GetTopLevel(owner);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    var topLevel = TopLevel.GetTopLevel(mainWindow);
                    if (topLevel != null) return topLevel;
                }

                // Fallback: try to find any active window
                foreach (var window in desktop.Windows)
                {
                    if (window.IsActive)
                    {
                        var topLevel = TopLevel.GetTopLevel(window);
                        if (topLevel != null) return topLevel;
                    }
                }
                
                // Fallback: use the first window
                if (desktop.Windows.Count > 0)
                {
                     return TopLevel.GetTopLevel(desktop.Windows[0]);
                }
            }
            Console.WriteLine("DialogHelper: Could not find TopLevel");
            return null;
        }

        public static T RunSync<T>(Task<T> task)
        {
            if (task.IsCompleted)
                return task.Result;

            Console.WriteLine("DialogHelper: Waiting for async dialog...");
            var cts = new CancellationTokenSource();
            task.ContinueWith(_ => {
                Console.WriteLine("DialogHelper: Task completed, cancelling token.");
                cts.Cancel();
            }, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.MainLoop(cts.Token);
            return task.GetAwaiter().GetResult();
        }

        public static async Task<IStorageFolder?> GetStartLocationAsync(IStorageProvider provider, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                // Handle local paths
                if (!path.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && !Uri.IsWellFormedUriString(path, UriKind.Absolute))
                {
                     return await provider.TryGetFolderFromPathAsync(new Uri(path));
                }
                return await provider.TryGetFolderFromPathAsync(new Uri(path));
            }
            catch
            {
                return null;
            }
        }

        public static List<FilePickerFileType> ParseFilter(string filter)
        {
            var result = new List<FilePickerFileType>();
            if (string.IsNullOrEmpty(filter)) return result;

            var parts = filter.Split('|');
            for (int i = 0; i < parts.Length; i += 2)
            {
                if (i + 1 >= parts.Length) break;
                var name = parts[i];
                var patterns = parts[i + 1].Split(';');
                
                var fileType = new FilePickerFileType(name)
                {
                    Patterns = patterns
                };
                result.Add(fileType);
            }
            return result;
        }
    }

    public class OpenFolderDialog
    {
        public bool Multiselect { get; set; }
        public string Title { get; set; }
        public string FolderName { get; set; }
        public string[] FolderNames { get; set; } = Array.Empty<string>();
        public string InitialDirectory { get; set; }

        public bool? ShowDialog()
        {
            return DialogHelper.RunSync(ShowDialogAsync());
        }

        public async Task<bool?> ShowDialogAsync(Window? owner = null)
        {
            Console.WriteLine("OpenFolderDialog.ShowDialogAsync: Enter");
            var topLevel = DialogHelper.GetTopLevel(owner);
            if (topLevel == null)
            {
                Console.WriteLine("OpenFolderDialog.ShowDialogAsync: TopLevel is null");
                return false;
            }

            Console.WriteLine($"OpenFolderDialog.ShowDialogAsync: InitialDirectory={InitialDirectory}");
            var startLocation = await DialogHelper.GetStartLocationAsync(topLevel.StorageProvider, InitialDirectory);
            Console.WriteLine($"OpenFolderDialog.ShowDialogAsync: StartLocation={startLocation?.Path}");

            var options = new FolderPickerOpenOptions
            {
                Title = Title,
                AllowMultiple = Multiselect,
                SuggestedStartLocation = startLocation
            };

            Console.WriteLine("OpenFolderDialog.ShowDialogAsync: Calling OpenFolderPickerAsync");
            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            Console.WriteLine($"OpenFolderDialog.ShowDialogAsync: Result count={result.Count}");
            if (result.Count > 0)
            {
                FolderName = result[0].Path.LocalPath;
                FolderNames = result.Select(x => x.Path.LocalPath).ToArray();
                return true;
            }
            return false;
        }
    }

    public class SaveFileDialog
    {
        public string FileName { get; set; } = string.Empty;
        public string Filter { get; set; } = string.Empty;
        public string InitialDirectory { get; set; } = string.Empty;
        public string Title { get; set; }
		public string DefaultExt { get; internal set; }

		public bool? ShowDialog()
        {
            return DialogHelper.RunSync(ShowDialogAsync());
        }

        public async Task<bool?> ShowDialogAsync(Window? owner = null)
        {
            Console.WriteLine("SaveFileDialog.ShowDialogAsync: Enter");
            var topLevel = DialogHelper.GetTopLevel(owner);
            if (topLevel == null)
            {
                Console.WriteLine("SaveFileDialog.ShowDialogAsync: TopLevel is null");
                return false;
            }

            Console.WriteLine($"SaveFileDialog.ShowDialogAsync: InitialDirectory={InitialDirectory}");
            var startLocation = await DialogHelper.GetStartLocationAsync(topLevel.StorageProvider, InitialDirectory);
            Console.WriteLine($"SaveFileDialog.ShowDialogAsync: StartLocation={startLocation?.Path}");

            var options = new FilePickerSaveOptions
            {
                Title = Title,
                SuggestedFileName = FileName,
                FileTypeChoices = DialogHelper.ParseFilter(Filter),
                SuggestedStartLocation = startLocation,
                DefaultExtension = DefaultExt
            };

            Console.WriteLine("SaveFileDialog.ShowDialogAsync: Calling SaveFilePickerAsync");
            var result = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            Console.WriteLine($"SaveFileDialog.ShowDialogAsync: Result={result?.Path}");
            if (result != null)
            {
                FileName = result.Path.LocalPath;
                return true;
            }
            return false;
        }
    }

    public class OpenFileDialog
    {
        public string FileName { get; set; } = string.Empty;
        public string[] FileNames { get; set; } = Array.Empty<string>();
        public string Filter { get; set; } = string.Empty;
        public string InitialDirectory { get; set; } = string.Empty;
        public bool Multiselect { get; set; }
        public bool RestoreDirectory { get; set; }
        public string Title { get; set; }

        public bool? ShowDialog()
        {
            return DialogHelper.RunSync(ShowDialogAsync());
        }

        public async Task<bool?> ShowDialogAsync(Window? owner = null)
        {
            Console.WriteLine("OpenFileDialog.ShowDialogAsync: Enter");
            var topLevel = DialogHelper.GetTopLevel(owner);
            if (topLevel == null)
            {
                Console.WriteLine("OpenFileDialog.ShowDialogAsync: TopLevel is null");
                return false;
            }

            Console.WriteLine($"OpenFileDialog.ShowDialogAsync: InitialDirectory={InitialDirectory}");
            var startLocation = await DialogHelper.GetStartLocationAsync(topLevel.StorageProvider, InitialDirectory);
            Console.WriteLine($"OpenFileDialog.ShowDialogAsync: StartLocation={startLocation?.Path}");

            var options = new FilePickerOpenOptions
            {
                Title = Title,
                AllowMultiple = Multiselect,
                FileTypeFilter = DialogHelper.ParseFilter(Filter),
                SuggestedStartLocation = startLocation
            };

            Console.WriteLine("OpenFileDialog.ShowDialogAsync: Calling OpenFilePickerAsync");
            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            Console.WriteLine($"OpenFileDialog.ShowDialogAsync: Result count={result.Count}");
            if (result.Count > 0)
            {
                FileName = result[0].Path.LocalPath;
                FileNames = result.Select(x => x.Path.LocalPath).ToArray();
                return true;
            }
            return false;
        }
    }
}
