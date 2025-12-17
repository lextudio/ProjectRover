using System;
using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Interactivity;
using Avalonia.Input;
using System.Linq;
using TomsToolbox.Composition;

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
            // Resource strings are provided via AXAML static bindings

            // ListBox triggers are handled in AXAML via Interaction.Behaviors

            // set CommandParameter to this window so commands that expect a parent can use it
            try
            {
                NewButton.CommandParameter = this;
                CloneButton.CommandParameter = this;
                RenameButton.CommandParameter = this;
                DeleteButton.CommandParameter = this;
                ResetButton.CommandParameter = this;
            }
            catch { }
        }

        private void PreconfiguredAssemblyListsMenuClick(object? sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as dynamic;
            var menuItems = vm?.PreconfiguredAssemblyLists as System.Collections.IEnumerable;
            if (menuItems == null) return;

            var menu = new ContextMenu();
            var items = menu.Items;
            foreach (var item in menuItems)
            {
                var mi = new MenuItem();
                mi.Header = item.ToString();
                mi.Command = vm?.CreatePreconfiguredAssemblyListCommand as ICommand;
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
    }
}
