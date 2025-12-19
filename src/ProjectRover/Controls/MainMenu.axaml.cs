using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia;
using TomsToolbox.Composition;
using ProjectRover.Settings;
using ICSharpCode.ILSpy.Util;
using System.Collections.Generic;
using ICSharpCode.ILSpy.Commands;
using System.Windows.Input;
using ICSharpCode.ILSpy.Themes;
using System.ComponentModel;

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
            var settingsService = exportProvider.GetExportedValue<SettingsService>();

            var parentMenuItems = new Dictionary<string, MenuItem>();
            var menuGroups = mainMenuCommands.OrderBy(c => c.Metadata?.MenuOrder).GroupBy(c => c.Metadata?.ParentMenuID).ToArray();
            var themeMenuItems = new List<MenuItem>();
            var nativeThemeItems = new List<NativeMenuItem>();

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
                            if (string.Equals(entry.Metadata?.ParentMenuID, "_Theme", StringComparison.OrdinalIgnoreCase))
                            {
                                menuItem.ToggleType = MenuItemToggleType.Radio;
                                menuItem.IsChecked = string.Equals(ThemeManager.Current.Theme, headerText ?? entry.Metadata?.Header, StringComparison.OrdinalIgnoreCase);
                                menuItem.Click += (_, _) => MainMenuThemeHelpers.ApplyThemeFromHeader(menuItem.Header?.ToString(), settingsService);
                                themeMenuItems.Add(menuItem);
                            }
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

            if (themeMenuItems.Count > 0)
            {
                MainMenuThemeHelpers.UpdateThemeChecks(themeMenuItems, settingsService?.SessionSettings?.Theme ?? ThemeManager.Current.Theme);
                MainMenuThemeHelpers.UpdateNativeThemeChecks(nativeThemeItems, settingsService?.SessionSettings?.Theme ?? ThemeManager.Current.Theme);

                var sessionSettings = settingsService?.SessionSettings;
                if (sessionSettings != null)
                {
                    sessionSettings.PropertyChanged += (_, e) => {
                        if (e.PropertyName == nameof(sessionSettings.Theme))
                        {
                            MainMenuThemeHelpers.UpdateThemeChecks(themeMenuItems, sessionSettings.Theme);
                            MainMenuThemeHelpers.UpdateNativeThemeChecks(nativeThemeItems, sessionSettings.Theme);
                        }
                    };
                }
            }

            // On macOS, default behavior is to hide the Avalonia menu and mirror it to the native menu bar.
            // Respect the Rover-level preference if present: when the user opted to "Keep main menu visible on macOS",
            // do not perform the native mirroring and ensure the Avalonia menu stays visible.
            if (OperatingSystem.IsMacOS())
            {
                try
                {
                    var roverSettings = new ProjectRoverSettingsService().Load();
                    var keepAvaloniaMenu = roverSettings.ShowAvaloniaMainMenuOnMac;

                    var visualRoot = TopLevel.GetTopLevel(mainMenu) ?? mainMenu.GetVisualRoot();
                    var rootObj = visualRoot as AvaloniaObject;

                    mainMenu.IsVisible = keepAvaloniaMenu;
                    
                    var nativeRoot = new NativeMenu();

                    NativeMenuItem Convert(MenuItem m)
                    {
                        var header = m.Header?.ToString() ?? (m.Tag as string) ?? string.Empty;
                        var native = new NativeMenuItem { Header = header };

                    if (m.Command != null)
                    {
                        native.Command = m.Command;
                        native.CommandParameter = m.CommandParameter;
                    }

                        if (m.ToggleType != MenuItemToggleType.None)
                        {
                            native.ToggleType = (NativeMenuItemToggleType)m.ToggleType;
                            native.IsChecked = m.IsChecked == true;
                        }

                        if (string.Equals(header, "Light", StringComparison.OrdinalIgnoreCase) || string.Equals(header, "Dark", StringComparison.OrdinalIgnoreCase))
                        {
                            nativeThemeItems.Add(native);
                        }

                        native.Click += (_, _) => {
                            MainMenuThemeHelpers.ApplyThemeFromHeader(header, settingsService);
                            MainMenuThemeHelpers.UpdateThemeChecks(themeMenuItems, ThemeManager.Current.Theme);
                            MainMenuThemeHelpers.UpdateNativeThemeChecks(nativeThemeItems, ThemeManager.Current.Theme);
                        };

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

                    // Always set a fresh native menu; avoid reusing existing items to prevent parent conflicts.
                    if (rootObj != null)
                        NativeMenu.SetMenu(rootObj, nativeRoot);
                    
                    if (Application.Current != null)
                        NativeMenu.SetMenu(Application.Current, nativeRoot);
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

    static class MainMenuThemeHelpers
    {
        public static void UpdateThemeChecks(IEnumerable<MenuItem> themeMenuItems, string? currentTheme)
        {
            if (string.IsNullOrEmpty(currentTheme))
                return;

            foreach (var mi in themeMenuItems)
            {
                mi.IsChecked = string.Equals(mi.Header?.ToString(), currentTheme, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void UpdateNativeThemeChecks(IEnumerable<NativeMenuItem> nativeThemeItems, string? currentTheme)
        {
            if (string.IsNullOrEmpty(currentTheme))
                return;

            foreach (var mi in nativeThemeItems)
            {
                mi.IsChecked = string.Equals(mi.Header, currentTheme, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void ApplyThemeFromHeader(string? header, SettingsService? settingsService)
        {
            if (string.IsNullOrWhiteSpace(header))
                return;

            Console.WriteLine($"[MainMenu] Theme menu click: {header}");
            ThemeManager.Current.ApplyTheme(header);
            Console.WriteLine($"[MainMenu] Application.ActualThemeVariant now {Application.Current?.ActualThemeVariant}");
            if (settingsService != null)
            {
                settingsService.SessionSettings.Theme = header;
                Console.WriteLine($"[MainMenu] SessionSettings.Theme set to {header}");
            }
        }
    }
}
