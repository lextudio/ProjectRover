// this file contains the WPF-specific part of AssemblyTreeModel
using System;
using System.Diagnostics.CodeAnalysis;

using ICSharpCode.ILSpy.AssemblyTree;

using ICSharpCode.ILSpy.TextView;

using ICSharpCode.ILSpy.ViewModels;
using ProjectRover.Views;

namespace ICSharpCode.ILSpy
{
    public partial class AssemblyTreeModel
    {
        private AssemblyListPane? activeView;

        public void SetActiveView(AssemblyListPane activeView)
		{
			this.activeView = activeView;
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

        private void RefreshInternal()
		{
			// TODO:
			// using (Keyboard.FocusedElement.PreserveFocus())
			// {
			// 	var path = GetPathForNode(SelectedItem);

			// 	ShowAssemblyList(settingsService.AssemblyListManager.LoadList(AssemblyList.ListName));
			// 	SelectNode(FindNodeByPath(path, true), inNewTabPage: false);

			// 	RefreshDecompiledView();
			// }
		}

		private void NavigateTo(EventHandler e, bool inNewTabPage = false)
		{
			// TODO: if (e.Uri.Scheme != "resource")
			/*
			{
				return;
			}

			TabPageModel tabPage = DockWorkspace.ActiveTabPage;
			ViewState? oldState = tabPage.GetState();
			ViewState? newState;

			if (navigatingToState == null)
			{
				if (oldState != null)
				{
					history.UpdateCurrent(new NavigationState(tabPage, oldState));
				}

				newState = new ViewState { ViewedUri = e.Uri };

				if (inNewTabPage)
				{
					tabPage = DockWorkspace.AddTabPage();
				}
			}
			else
			{
				newState = navigatingToState.ViewState;
				tabPage = DockWorkspace.ActiveTabPage = navigatingToState.TabPage;
			}

			bool needsNewNavigationEntry = !inNewTabPage && selectedItems?.Length == 0;

			UnselectAll();

			if (e.Uri.Host == "aboutpage")
			{
				MessageBus.Send(this, new ShowAboutPageEventArgs(DockWorkspace.ActiveTabPage));
				e.Handled = true;
			}
			else
			{
				AvalonEditTextOutput output = new AvalonEditTextOutput {
					Address = e.Uri,
					Title = e.Uri.AbsolutePath,
					EnableHyperlinks = true
				};
				using (Stream? s = typeof(App).Assembly.GetManifestResourceStream(typeof(App), e.Uri.AbsolutePath))
				{
					if (s != null)
					{
						using StreamReader r = new StreamReader(s);
						string? line;
						while ((line = r.ReadLine()) != null)
						{
							output.Write(line);
							output.WriteLine();
						}
					}
				}
				DockWorkspace.ShowText(output);
				e.Handled = true;
			}

			if (navigatingToState == null)
			{
				// the call to UnselectAll() above already creates a new navigation entry,
				// we just need to make sure it contains something useful.
				if (!needsNewNavigationEntry)
				{
					history.UpdateCurrent(new NavigationState(tabPage, tabPage.GetState()));
				}
				else
				{
					history.Record(new NavigationState(tabPage, tabPage.GetState()));
				}
			}
			*/
		}

        private static bool FormatExceptions(App.ExceptionData[] exceptions, [NotNullWhen(true)] out object? output)// AvaloniaEditTextOutput? output)
		{
			output = null;

			var result = exceptions.FormatExceptions();
			if (result.IsNullOrEmpty())
				return false;

			output = new();
			//output.Write(result);
			return true;

		}
    }
}