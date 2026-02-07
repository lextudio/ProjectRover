// Copyright (c) 2025-2026 LeXtudio Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.TextView;
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

			// Initialize the dispatcher throttle that coalesces refresh requests.
			refreshThrottle = new DispatcherThrottle(RefreshInternal);

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
