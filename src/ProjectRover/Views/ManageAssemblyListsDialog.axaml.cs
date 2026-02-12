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

using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Interactivity;
using TomsToolbox.Composition;
using ICSharpCode.ILSpy.ViewModels;

namespace ICSharpCode.ILSpy
{
    public partial class ManageAssemblyListsDialog : Window
    {
        public ManageAssemblyListsDialog()
            : this(ProjectRover.App.ExportProvider.GetExportedValue<SettingsService>())
        {
        }

        public ManageAssemblyListsDialog(SettingsService settingsService)
        {
            InitializeComponent();
            DataContext = new ManageAssemblyListsViewModel(this, settingsService);
            NewButton.CommandParameter = this;
            CloneButton.CommandParameter = this;
            RenameButton.CommandParameter = this;
            DeleteButton.CommandParameter = this;
            ResetButton.CommandParameter = this;
        }

        private void PreconfiguredAssemblyListsMenuClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ManageAssemblyListsViewModel vm)
                return;

            var menu = new ContextMenu();
            var items = menu.Items;
            foreach (var item in vm.PreconfiguredAssemblyLists)
            {
                var mi = new MenuItem();
                mi.Header = item.Name;
                mi.Command = vm.CreatePreconfiguredAssemblyListCommand as ICommand;
                mi.CommandParameter = item;
                items.Add(mi);
            }

            // Show context menu anchored to the PreconfiguredButton
            var button = this.FindControl<Button>("PreconfiguredButton");
            if (button != null)
            {
                menu.PlacementTarget = button;
                menu.Open(button);
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
