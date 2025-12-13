using System;
using System.Linq;
using System.ComponentModel;
using System.Composition;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectRover;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.TextView;

namespace ICSharpCode.ILSpy.ViewModels
{
    // Adapted PaneModel from ILSpy's WPF implementation. In Rover we reuse the project's ObservableObject pattern.
    public abstract class PaneModel : ObservableObject
    {
        protected static Docking.IDockWorkspace DockWorkspace => App.ExportProvider?.GetExportedValue<Docking.IDockWorkspace>()!;

        class CloseCommandImpl : ICommand
        {
            readonly PaneModel model;

            public CloseCommandImpl(PaneModel model)
            {
                this.model = model;
                this.model.PropertyChanged += Model_PropertyChanged;
            }

            private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(model.IsCloseable))
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => model.IsCloseable;

            public void Execute(object? parameter)
            {
                try
                {
                    var dw = App.ExportProvider?.GetExportedValue<Docking.IDockWorkspace>();
                    dw?.Remove(model);
                }
                catch { }
            }
        }

        private bool isSelected;
        public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }

        private bool isActive;
        public bool IsActive { get => isActive; set => SetProperty(ref isActive, value); }

        private bool isVisible;
        public bool IsVisible { get => isVisible; set { if (SetProperty(ref isVisible, value) && !value) IsActive = false; } }

        private bool isCloseable = true;
        public bool IsCloseable { get => isCloseable; set => SetProperty(ref isCloseable, value); }

        public ICommand CloseCommand => new CloseCommandImpl(this);

        private string contentId = string.Empty;
        public string ContentId { get => contentId; set => SetProperty(ref contentId, value); }

        private string title = string.Empty;
        public string Title { get => title; set { if (SetProperty(ref title, value)) OnPropertyChanged(nameof(Title)); } }
    }

    [Export(typeof(ICSharpCode.ILSpy.ViewModels.TabPageModel))]
    [Shared]
    public class TabPageModel : PaneModel
    {
        private bool supportsLanguageSwitching = true;
        public bool SupportsLanguageSwitching { get => supportsLanguageSwitching; set => SetProperty(ref supportsLanguageSwitching, value); }

        private bool frozenContent;
        public bool FrozenContent { get => frozenContent; set => SetProperty(ref frozenContent, value); }

        private object? content;
        public object? Content { get => content; set => SetProperty(ref content, value); }

        public ViewState? GetState()
        {
            return (Content as IHaveState)?.GetState();
        }
    }

    public interface IHaveState { ViewState? GetState(); }

    public static class TabPageModelExtensions
    {
        public static System.Threading.Tasks.Task<T> ShowTextViewAsync<T>(this TabPageModel tabPage, Func<object, System.Threading.Tasks.Task<T>> action)
        {
            if (tabPage.Content == null)
                tabPage.Content = new object();
            return action(tabPage.Content!);
        }

        public static System.Threading.Tasks.Task ShowTextViewAsync(this TabPageModel tabPage, Func<object, System.Threading.Tasks.Task> action)
        {
            if (tabPage.Content == null)
                tabPage.Content = new object();
            return action(tabPage.Content!);
        }

        public static void ShowTextView(this TabPageModel tabPage, Action<object> action)
        {
            if (tabPage.Content == null)
                tabPage.Content = new object();
            action(tabPage.Content!);
        }

        public static void Focus(this TabPageModel tabPage)
        {
            if (tabPage.Content is Avalonia.Controls.Control c)
                c.Focus();
        }

        public static DecompilationOptions CreateDecompilationOptions(this TabPageModel tabPage)
        {
            try
            {
                var export = App.ExportProvider;
                if (export != null)
                {
                        try
                        {
                            var provider = App.ExportProvider;
                            ICSharpCode.ILSpy.LanguageService? languageService = null;
                            ICSharpCode.ILSpy.Util.SettingsService? settingsService = null;
                            try
                            {
                                if (provider != null)
                                {
                                    languageService = provider.GetExportedValue<LanguageService>();
                                    settingsService = provider.GetExportedValue<ICSharpCode.ILSpy.Util.SettingsService>();
                                }
                            }
                            catch { }

                            if (languageService != null || settingsService != null)
                            {
                                object? languageVersion = languageService?.LanguageVersion;
                                object? decompilerSettings = settingsService?.DecompilerSettings;
                                object? displaySettings = settingsService?.DisplaySettings;

                                if (decompilerSettings == null)
                                {
                                    try { decompilerSettings = new ICSharpCode.ILSpyX.Settings.DecompilerSettings(); } catch { decompilerSettings = null; }
                                }

                                if (displaySettings == null)
                                {
                                    try { displaySettings = new ICSharpCode.ILSpy.Options.DisplaySettings(); } catch { displaySettings = null; }
                                }

                                try
                                {
                                    var decoType = typeof(DecompilationOptions);
                                    var ctor = decoType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 3);
                                    if (ctor != null)
                                    {
                                        var args = new object?[] { languageVersion, decompilerSettings, displaySettings };
                                        var obj = ctor.Invoke(args);
                                        if (obj is DecompilationOptions d)
                                            return d;
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                }
            }
            catch { }

            // Require the language and settings shims to be available via ExportProvider
            var provider2 = App.ExportProvider ?? throw new InvalidOperationException("ExportProvider is not initialized");
            var ls = provider2.GetExportedValue<ICSharpCode.ILSpy.LanguageService>();
            var ss = provider2.GetExportedValue<ICSharpCode.ILSpy.Util.SettingsService>();
            if (ls == null || ss == null)
                throw new InvalidOperationException("Required ILSpy services (LanguageService or SettingsService) are not available from the ExportProvider.");

            var languageVersion2 = ls.LanguageVersion ?? new ICSharpCode.ILSpyX.LanguageVersion("Latest");
            var decompilerSettings2 = ss.DecompilerSettings ?? new ICSharpCode.ILSpyX.Settings.DecompilerSettings();
            var displaySettings2 = ss.DisplaySettings ?? new ICSharpCode.ILSpy.Options.DisplaySettings();

            var decoType2 = typeof(DecompilationOptions);
            var ctor2 = decoType2.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 3);
            if (ctor2 == null)
                throw new InvalidOperationException("DecompilationOptions constructor with 3 parameters not found.");

            var args2 = new object?[] { languageVersion2, decompilerSettings2, displaySettings2 };
            var obj2 = ctor2.Invoke(args2);
            if (obj2 is DecompilationOptions dd)
                return dd;
            throw new InvalidOperationException("Failed to construct DecompilationOptions from provided services.");
        }
    }
}
