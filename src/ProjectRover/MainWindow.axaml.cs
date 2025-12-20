using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia;
using ICSharpCode.ILSpy.Search;
using ICSharpCode.ILSpy.AssemblyTree;
using System.Windows.Threading;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Presenters;
using ICSharpCode.ILSpy.ViewModels;
using Dock.Model.TomsToolbox.Controls;
using Dock.Model.Core;

namespace ICSharpCode.ILSpy
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? viewModel;

        public MainWindow()
        {
            InitializeComponent();
            var dockHost = this.FindControl<DockControl>("DockHost");
            var documentTemplate = new FuncDataTemplate<TabPageModel>(
                (data, scope) => {
                    var contentPresenter = new ContentPresenter { DataContext = data };
                    contentPresenter.Bind(ContentPresenter.ContentProperty, new Binding("Content"));
                    return contentPresenter;
                }
            );
            dockHost.DataTemplates.Add(documentTemplate);
            var toolTemplate = new FuncDataTemplate<Tool>(
                (data, scope) => new ContentPresenter { DataContext = data, Content = data.Content }
            );
            dockHost.DataTemplates.Add(toolTemplate);
#if DEBUG
            this.AttachDevTools();
            Console.WriteLine("[Log][MainWindow] DevTools attached.");
#endif
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => {
				viewModel.Workspace.InitializeLayout();
				MessageBus.Send(this, new MainWindowLoadedEventArgs());
			});
        }

        public MainWindow(MainWindowViewModel viewModel) : this()
        {
            this.viewModel = viewModel;
            DataContext = viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
