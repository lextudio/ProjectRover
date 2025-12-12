using System;
using System.ComponentModel;
using System.Composition;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectRover;

namespace ICSharpCode.ILSpy.ViewModels
{
    // Adapted PaneModel from ILSpy's WPF implementation. In Rover we reuse the project's ObservableObject pattern.
    public abstract class PaneModel : ObservableObject
    {
        protected static IDockWorkspace DockWorkspace => App.Current.ExportProvider?.GetExportedValue<IDockWorkspace>()!;

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
                    var dw = App.Current?.ExportProvider?.GetExportedValue<IDockWorkspace>();
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

    [Export]
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
                var export = App.Current?.ExportProvider;
                if (export != null)
                {
                    var languageServiceType = Type.GetType("ICSharpCode.ILSpy.Languages.LanguageService, ICSharpCode.ILSpyX") ?? Type.GetType("ICSharpCode.ILSpy.Languages.LanguageService");
                    var settingsServiceType = Type.GetType("ICSharpCode.ILSpy.Util.SettingsService, ICSharpCode.ILSpyX") ?? Type.GetType("ICSharpCode.ILSpy.Util.SettingsService");
                    object? languageService = null, settingsService = null;
                    try { var m = export.GetType().GetMethod("GetExportedValue"); if (m != null) { languageService = m.MakeGenericMethod(languageServiceType ?? typeof(object)).Invoke(export, null); settingsService = m.MakeGenericMethod(settingsServiceType ?? typeof(object)).Invoke(export, null); } }
                    catch { }

                    if (languageService != null && settingsService != null)
                    {
                        return new DecompilationOptions();
                    }
                }
            }
            catch { }

            return new DecompilationOptions();
        }
    }
}

namespace ICSharpCode.ILSpy
{
    public class ViewState { }
    public class DecompilationOptions { }
}
