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
using Avalonia.Interactivity;
using System.Threading.Tasks;
using ICSharpCode.ILSpy.Util;

namespace ICSharpCode.ILSpy.Options
{
    public partial class OptionsDialog : Window
    {
        public OptionsDialog()
        {
            InitializeComponent();
            var settingsService = ProjectRover.App.ExportProvider?.GetExportedValue<SettingsService>() ?? new SettingsService();
            DataContext = new OptionsDialogViewModel(settingsService);
        }

        public OptionsDialog(SettingsService settingsService)
        {
            InitializeComponent();
            DataContext = new OptionsDialogViewModel(settingsService);
        }

        public Task<bool?> ShowDialogAsync(Window? owner = null)
        {
            if (owner != null)
                this.Owner = owner;
            return this.ShowDialog<bool?>(owner);
        }

        private void OnOkClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is OptionsDialogViewModel viewModel
                && viewModel.CommitCommand.CanExecute(null))
            {
                viewModel.CommitCommand.Execute(null);
            }

            Close(true);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
