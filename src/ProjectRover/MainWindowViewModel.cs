// Copyright (c) 2021 Siegfried Pammer
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

using System.Collections.ObjectModel;
using System.Composition;
using System.Windows.Input;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.Search;
using ICSharpCode.ILSpyX;

using TomsToolbox.Wpf;

namespace ICSharpCode.ILSpy
{
    public record ThemeOption(string Name, ThemeVariant Variant);
    public record SearchModeOption(string Name, string IconKey);

	[Export]
	[Shared]
	public class MainWindowViewModel : ObservableObject
	{
        private readonly SettingsService settingsService;
        private readonly LanguageService languageService;
        private readonly DockWorkspace dockWorkspace;
        private readonly AssemblyTreeModel assemblyTreeModel;
        private readonly SearchPaneModel searchPaneModel;

        public MainWindowViewModel(
            SettingsService settingsService, 
            LanguageService languageService, 
            DockWorkspace dockWorkspace, 
            AssemblyTreeModel assemblyTreeModel,
            SearchPaneModel searchPaneModel,
            ManageAssemblyListsCommand manageAssemblyListsCommand)
        {
            this.settingsService = settingsService;
            this.languageService = languageService;
            this.dockWorkspace = dockWorkspace;
            this.assemblyTreeModel = assemblyTreeModel;
            this.searchPaneModel = searchPaneModel;
            this.ManageAssemblyListsCommand = manageAssemblyListsCommand;

            OpenFileCommand = new RelayCommand(OpenFile);
            ExitCommand = new RelayCommand(Exit);

            Themes = new ObservableCollection<ThemeOption>
            {
                new("Light", ThemeVariant.Light),
                new("Dark", ThemeVariant.Dark)
            };
            SelectedTheme = Themes[0];

            SearchModes = new ObservableCollection<SearchModeOption>
            {
                new("Types and Members", "SearchIcon"),
                new("Types", "SearchIcon"),
                new("Members", "SearchIcon"),
                new("Constants", "SearchIcon"),
                new("Metadata Tokens", "SearchIcon")
            };
            SelectedSearchMode = SearchModes[0];
        }

		public DockWorkspace Workspace => dockWorkspace;

        public AssemblyTreeModel AssemblyTreeModel => assemblyTreeModel;
        public SearchPaneModel SearchPaneModel => searchPaneModel;

		public LanguageService LanguageService => languageService;

		public SessionSettings SessionSettings => settingsService.SessionSettings;

		public AssemblyListManager AssemblyListManager => settingsService.AssemblyListManager;

        public ObservableCollection<ThemeOption> Themes { get; }

        private ThemeOption selectedTheme;
        public ThemeOption SelectedTheme
        {
            get => selectedTheme;
            set => SetProperty(ref selectedTheme, value);
        }

        public IRelayCommand OpenFileCommand { get; }
        public IRelayCommand ExitCommand { get; }
        public ICommand ManageAssemblyListsCommand { get; }

        private void OpenFile()
        {
            // TODO: Implement OpenFile
        }

        private void Exit()
        {
            // TODO: Implement Exit
        }

        // Search Properties
        private string searchText;
        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value))
                {
                    IsSearching = !string.IsNullOrEmpty(value);
                    // TODO: Trigger search
                }
            }
        }

        private bool isSearching;
        public bool IsSearching
        {
            get => isSearching;
            set => SetProperty(ref isSearching, value);
        }

        private string numberOfResultsText;
        public string NumberOfResultsText
        {
            get => numberOfResultsText;
            set => SetProperty(ref numberOfResultsText, value);
        }

        public ObservableCollection<SearchModeOption> SearchModes { get; }

        private SearchModeOption selectedSearchMode;
        public SearchModeOption SelectedSearchMode
        {
            get => selectedSearchMode;
            set => SetProperty(ref selectedSearchMode, value);
        }
	}
}
