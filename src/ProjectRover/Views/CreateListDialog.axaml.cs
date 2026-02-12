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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ICSharpCode.ILSpy
{
    public partial class CreateListDialog : Window
    {
        // Preserve the WPF-like public Owner setter used by linked view-model code.
        public new object? Owner { get; set; }

        public bool? DialogResult { get; private set; }

        // Preserve the API expected by linked WPF view-model code.
        public TextBox ListNameBox => listNameBox;

        public string ListName
        {
            get => listNameBox.Text ?? string.Empty;
            set
            {
                listNameBox.Text = value;
                UpdateOkButtonState();
            }
        }

        public CreateListDialog()
            : this(string.Empty)
        {
        }

        public CreateListDialog(string title)
        {
            InitializeComponent();
            Title = title;
            Opened += (_, _) =>
            {
                listNameBox.Focus();
                UpdateOkButtonState();
            };
        }

        public bool? ShowDialog()
        {
            DialogResult = null;

            var owner = ResolveOwnerWindow();
            if (owner != null)
            {
                return RunSync(this.ShowDialog<bool?>(owner));
            }

            // Fallback path when no owner is available.
            var tcs = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
            void ClosedHandler(object? sender, System.EventArgs e)
            {
                Closed -= ClosedHandler;
                tcs.TrySetResult(DialogResult);
            }

            Closed += ClosedHandler;
            Show();
            return RunSync(tcs.Task);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (e.Cancel)
            {
                if (DialogResult == true)
                    DialogResult = null;
                return;
            }

            DialogResult ??= false;
        }

        private Window? ResolveOwnerWindow()
        {
            if (Owner is Window owner)
                return owner;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.FirstOrDefault(static w => w.IsActive) ?? desktop.MainWindow;
            }

            return null;
        }

        private static T RunSync<T>(Task<T> task)
        {
            if (task.IsCompleted)
                return task.Result;

            var cts = new CancellationTokenSource();
            task.ContinueWith(_ => cts.Cancel(), TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.MainLoop(cts.Token);
            return task.GetAwaiter().GetResult();
        }

        private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void OKButton_Click(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(listNameBox.Text))
                return;

            DialogResult = true;
            Close(DialogResult);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close(DialogResult);
        }

        private void UpdateOkButtonState()
        {
            okButton.IsEnabled = !string.IsNullOrWhiteSpace(listNameBox.Text);
        }
    }
}
