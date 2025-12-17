using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia;
using TomsToolbox.Composition;
using System.Collections.Generic;
using ICSharpCode.ILSpy.Commands;
using System.Windows.Input;

namespace ICSharpCode.ILSpy.Controls
{
    public partial class MainMenu : UserControl
    {
        public MainMenu()
        {
            InitializeComponent();

            this.AttachedToVisualTree += (_, _) => {
                var exportProvider = ProjectRover.App.ExportProvider;
                if (exportProvider != null)
                {
                    // Delay to ensure visual tree is fully constructed and attached to a TopLevel
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        InitMainMenu(Menu, exportProvider);
                    });
                }
            };
        }

        static void InitMainMenu(Menu mainMenu, IExportProvider exportProvider)
        {
            // Get all main menu commands exported with contract "MainMenuCommand"
            var mainMenuCommands = exportProvider.GetExports<ICommand, IMainMenuCommandMetadata>("MainMenuCommand").ToArray();

            var parentMenuItems = new Dictionary<string, MenuItem>();
            var menuGroups = mainMenuCommands.OrderBy(c => c.Metadata?.MenuOrder).GroupBy(c => c.Metadata?.ParentMenuID).ToArray();

            foreach (var menu in menuGroups)
            {
                var parentMenuItem = GetOrAddParentMenuItem(mainMenu, parentMenuItems, menu.Key);
                foreach (var category in menu.GroupBy(c => c.Metadata?.MenuCategory))
                {
                    if (parentMenuItem.Items.Count > 0)
                    {
                        parentMenuItem.Items.Add(new Separator());
                    }
                    foreach (var entry in category)
                    {
                        if (menuGroups.Any(g => g.Key == entry.Metadata?.MenuID))
                        {
                            var subParent = GetOrAddParentMenuItem(mainMenu, parentMenuItems, entry.Metadata?.MenuID);
                            subParent.Header = entry.Metadata?.Header ?? subParent.Header?.ToString();
                            parentMenuItem.Items.Add(subParent);
                        }
                        else
                        {
                            var cmd = entry.Value;
                            var headerText = ICSharpCode.ILSpy.Util.ResourceHelper.GetString(entry.Metadata?.Header);
                            var menuItem = new MenuItem
                            {
                                Command = cmd,
                                Tag = entry.Metadata?.MenuID,
                                Header = headerText ?? entry.Metadata?.Header
                            };
                            parentMenuItem.Items.Add(menuItem);
                        }
                    }
                }
            }

            foreach (var item in parentMenuItems.Values.Where(i => i.Parent == null))
            {
                if (!mainMenu.Items.Contains(item))
                    mainMenu.Items.Add(item);
            }

            // On macOS, mirror the dynamically-built Avalonia Menu to a native menu bar
            if (OperatingSystem.IsMacOS())
            {
                try
                {
                    var visualRoot = TopLevel.GetTopLevel(mainMenu) ?? mainMenu.GetVisualRoot();
                    var rootObj = visualRoot as AvaloniaObject;
                    
                    var nativeRoot = new NativeMenu();

                    NativeMenuItem Convert(MenuItem m)
                    {
                        var header = m.Header?.ToString() ?? (m.Tag as string) ?? string.Empty;
                        var native = new NativeMenuItem { Header = header };

                        if (m.Command != null)
                            native.Command = m.Command;

                        if (m.Items != null && m.Items.Count > 0)
                        {
                            var sub = new NativeMenu();
                            foreach (var child in m.Items)
                            {
                                switch (child)
                                {
                                    case Separator:
                                        sub.Items.Add(new NativeMenuItemSeparator());
                                        break;
                                    case MenuItem childMi:
                                        sub.Items.Add(Convert(childMi));
                                        break;
                                }
                            }
                            native.Menu = sub;
                        }

                        if (m.Command is ICommand cmd)
                        {
                            try { native.IsEnabled = cmd.CanExecute(null); } catch { }
                            cmd.CanExecuteChanged += (_, __) => { try { native.IsEnabled = cmd.CanExecute(null); } catch { } };
                        }

                        return native;
                    }

                    foreach (var item in mainMenu.Items)
                    {
                        if (item is MenuItem mi)
                        {
                            nativeRoot.Items.Add(Convert(mi));
                        }
                    }

                    // If a NativeMenu already exists on the Window or Application (from XAML),
                    // merge our items into it rather than replacing it. Some XAML files
                    // declare a static NativeMenu and we should augment it.
                    NativeMenu? existing = null;
                    if (rootObj != null)
                    {
                        existing = NativeMenu.GetMenu(rootObj);
                    }

                    if (existing == null && Application.Current != null)
                    {
                        existing = NativeMenu.GetMenu(Application.Current);
                    }

                    if (existing != null)
                    {
                        // Append our generated items to the existing menu
                        foreach (var it in nativeRoot.Items)
                            existing.Items.Add(it);
                        
                        // Ensure application/visual root see the merged menu
                        if (rootObj != null)
                            NativeMenu.SetMenu(rootObj, existing);
                        
                        if (Application.Current != null)
                            NativeMenu.SetMenu(Application.Current, existing);
                    }
                    else
                    {
                        // No existing menu found — set ours on both visual root and Application
                        if (rootObj != null)
                            NativeMenu.SetMenu(rootObj, nativeRoot);
                        
                        if (Application.Current != null)
                            NativeMenu.SetMenu(Application.Current, nativeRoot);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"InitMainMenu: native menu integration failed: {ex}");
                }
            }
        }

        static MenuItem GetOrAddParentMenuItem(Menu mainMenu, Dictionary<string, MenuItem> parentMenuItems, string menuId)
        {
            if (menuId == null) menuId = string.Empty;
            if (!parentMenuItems.TryGetValue(menuId, out var parentMenuItem))
            {
                var topLevel = mainMenu.Items.OfType<MenuItem>().FirstOrDefault(m => (string?)m.Tag == menuId);
                if (topLevel == null)
                {
                    parentMenuItem = new MenuItem { Header = menuId, Tag = menuId };
                    parentMenuItems.Add(menuId, parentMenuItem);
                }
                else
                {
                    parentMenuItems.Add(menuId, topLevel);
                    parentMenuItem = topLevel;
                }
            }
            return parentMenuItem;
        }
    }
}
