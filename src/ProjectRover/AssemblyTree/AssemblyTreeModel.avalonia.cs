// this file contains the WPF-specific part of AssemblyTreeModel
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.TextViewControl;
using ICSharpCode.ILSpy.Updates;
using ICSharpCode.ILSpy.Util;
using ICSharpCode.ILSpy.ViewModels;
using ICSharpCode.ILSpyX;
using ICSharpCode.ILSpyX.TreeView;

using ICSharpCode.ILSpy.Views;

using TomsToolbox.Composition;

using ProjectRover;
using ICSharpCode.ILSpy.Properties;
using Avalonia.Input;
using System.Windows.Threading;
using ICSharpCode.ILSpy.AppEnv;
using Avalonia.Controls;

namespace ICSharpCode.ILSpy.AssemblyTree
{
    public partial class AssemblyTreeModel
    {
		public AssemblyTreeModel(SettingsService settingsService, LanguageService languageService, IExportProvider exportProvider)
		{
			this.settingsService = settingsService;
			this.languageService = languageService;
			this.exportProvider = exportProvider;

			Title = Resources.Assemblies;
			ContentId = PaneContentId;
			IsCloseable = false;
			ShortcutKey = new KeyGesture(Key.F6);

			MessageBus<NavigateToReferenceEventArgs>.Subscribers += JumpToReference;
			MessageBus<SettingsChangedEventArgs>.Subscribers += (sender, e) => Settings_PropertyChanged(sender, e);
			MessageBus<ApplySessionSettingsEventArgs>.Subscribers += ApplySessionSettings;
			MessageBus<ActiveTabPageChangedEventArgs>.Subscribers += ActiveTabPageChanged;
			MessageBus<TabPagesCollectionChangedEventArgs>.Subscribers += (_, e) => history.RemoveAll(s => !DockWorkspace.TabPages.Contains(s.TabPage));
			MessageBus<ResetLayoutEventArgs>.Subscribers += ResetLayout;
			MessageBus<NavigateToEventArgs>.Subscribers += (_, e) => NavigateTo(e.Request, e.InNewTabPage);
			MessageBus<MainWindowLoadedEventArgs>.Subscribers += (_, _) => {
				Initialize();
				Show();
			};

			// TODO: EventManager.RegisterClassHandler(typeof(Window), Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler((_, e) => NavigateTo(e)));

			// refreshThrottle = new(DispatcherPriority.Background, RefreshInternal);

			AssemblyList = settingsService.CreateEmptyAssemblyList();
		}

		private static void LoadInitialAssemblies(AssemblyList assemblyList)
		{
			// Called when loading an empty assembly list; so that
			// the user can see something initially.
			System.Reflection.Assembly[] initialAssemblies = {
				typeof(object).Assembly,
				typeof(Uri).Assembly,
				typeof(System.Linq.Enumerable).Assembly,
				typeof(System.Xml.XmlDocument).Assembly,
				typeof(Avalonia.Markup.Xaml.MarkupExtension).Assembly,
				typeof(Avalonia.Rect).Assembly,
				typeof(Avalonia.Controls.Control).Assembly,
				typeof(Avalonia.Controls.UserControl).Assembly
			};
			foreach (System.Reflection.Assembly asm in initialAssemblies)
				assemblyList.OpenAssembly(asm.Location);
		}
	}
}