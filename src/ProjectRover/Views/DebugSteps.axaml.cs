using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Input;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.ViewModels;
using ProjectRover; // For App
using System.Collections.ObjectModel;
using ICSharpCode.ILSpy.TextViewControl;

namespace ICSharpCode.ILSpy
{
    public partial class DebugSteps : UserControl
    {
        private readonly AssemblyTreeModel assemblyTreeModel;
        private readonly SettingsService settingsService;
        private readonly LanguageService languageService;
        private readonly DockWorkspace dockWorkspace;

        static readonly ILAstWritingOptions writingOptions = new ILAstWritingOptions {
            UseFieldSugar = true,
            UseLogicOperationSugar = true
        };

        public static ILAstWritingOptions Options => writingOptions;

        public ObservableCollection<Stepper.Node> TreeRoot { get; } = new ObservableCollection<Stepper.Node>();

#if DEBUG
        ILAstLanguage language;
#endif

        public DebugSteps()
            : this(
                ProjectRover.App.ExportProvider.GetExportedValue<AssemblyTreeModel>(),
                ProjectRover.App.ExportProvider.GetExportedValue<SettingsService>(),
                ProjectRover.App.ExportProvider.GetExportedValue<LanguageService>(),
                ProjectRover.App.ExportProvider.GetExportedValue<DockWorkspace>()
            )
        {
        }

        public DebugSteps(AssemblyTreeModel assemblyTreeModel, SettingsService settingsService, LanguageService languageService, DockWorkspace dockWorkspace)
        {
            InitializeComponent();

            this.assemblyTreeModel = assemblyTreeModel;
            this.settingsService = settingsService;
            this.languageService = languageService;
            this.dockWorkspace = dockWorkspace;

#if DEBUG
            MessageBus<SettingsChangedEventArgs>.Subscribers += (sender, e) => Settings_PropertyChanged(sender, e);
            MessageBus<AssemblyTreeSelectionChangedEventArgs>.Subscribers += SelectionChanged;

            writingOptions.PropertyChanged += WritingOptions_PropertyChanged;

            if (languageService.Language is ILAstLanguage l)
            {
                l.StepperUpdated += ILAstStepperUpdated;
                language = l;
                ILAstStepperUpdated(null, null);
            }
#endif
        }

        private void WritingOptions_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            DecompileAsync(lastSelectedStep);
        }

        private void SelectionChanged(object sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() => {
                TreeRoot.Clear();
                lastSelectedStep = int.MaxValue;
            });
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
#if DEBUG
            if (sender is not LanguageSettings)
                return;

            if (e.PropertyName == nameof(LanguageSettings.LanguageId))
            {
                if (language != null)
                {
                    language.StepperUpdated -= ILAstStepperUpdated;
                }
                if (languageService.Language is ILAstLanguage l)
                {
                    l.StepperUpdated += ILAstStepperUpdated;
                    language = l;
                    ILAstStepperUpdated(null, null);
                }
            }
#endif
        }

        private void ILAstStepperUpdated(object sender, EventArgs e)
        {
#if DEBUG
            if (language == null)
                return;
            Dispatcher.UIThread.InvokeAsync(() => {
                TreeRoot.Clear();
                if (language.Stepper?.Steps != null)
                {
                    foreach (var step in language.Stepper.Steps)
                    {
                        TreeRoot.Add(step);
                    }
                }
                lastSelectedStep = int.MaxValue;
            });
#endif
        }

        private void ShowStateAfter_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control && control.DataContext is Stepper.Node n)
            {
                DecompileAsync(n.EndStep);
            }
            else if (tree.SelectedItem is Stepper.Node selected)
            {
                DecompileAsync(selected.EndStep);
            }
        }

        private void ShowStateBefore_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control && control.DataContext is Stepper.Node n)
            {
                DecompileAsync(n.BeginStep);
            }
            else if (tree.SelectedItem is Stepper.Node selected)
            {
                DecompileAsync(selected.BeginStep);
            }
        }

        private void DebugStep_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control && control.DataContext is Stepper.Node n)
            {
                DecompileAsync(n.BeginStep, true);
            }
            else if (tree.SelectedItem is Stepper.Node selected)
            {
                DecompileAsync(selected.BeginStep, true);
            }
        }

        private void tree_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (e.KeyModifiers == KeyModifiers.Shift)
                    ShowStateBefore_Click(sender, e);
                else
                    ShowStateAfter_Click(sender, e);
                e.Handled = true;
            }
        }

        int lastSelectedStep = int.MaxValue;

        void DecompileAsync(int step, bool isDebug = false)
        {
            lastSelectedStep = step;

            if (dockWorkspace.ActiveTabPage.FrozenContent)
            {
                dockWorkspace.ActiveTabPage = dockWorkspace.AddTabPage();
            }

            var state = dockWorkspace.ActiveTabPage.GetState();
            dockWorkspace.ActiveTabPage.ShowTextViewAsync(textView => textView.DecompileAsync(assemblyTreeModel.CurrentLanguage, assemblyTreeModel.SelectedNodes, null,
                new DecompilationOptions(assemblyTreeModel.CurrentLanguageVersion, settingsService.DecompilerSettings, settingsService.DisplaySettings) {
                    StepLimit = step,
                    IsDebug = isDebug,
                    TextViewState = state as DecompilerTextViewState
                }));
        }
    }
}
