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
        // Attached property to remember the original icon key for each MenuItem so we can reload
        // the themed image when the application theme changes.
        public static readonly AttachedProperty<string?> MenuIconKeyProperty =
            Avalonia.AvaloniaProperty.RegisterAttached<MainMenu, MenuItem, string?>("MenuIconKey");

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
                        // Avoid inserting duplicate separators: only add a Separator when the last
                        // added item is not already a Separator.
                        var last = parentMenuItem.Items.LastOrDefault();
                        if (!(last is Separator))
                        {
                            parentMenuItem.Items.Add(new Separator());
                        }
                    }
                    foreach (var entry in category)
                    {
                        if (menuGroups.Any(g => g.Key == entry.Metadata?.MenuID))
                        {
                            var subParent = GetOrAddParentMenuItem(mainMenu, parentMenuItems, entry.Metadata?.MenuID);
                            subParent.Header = entry.Metadata?.Header ?? subParent.Header?.ToString();

                            // If the submenu item we found already has a visual parent, don't add the same
                            // instance again (that causes an InvalidOperationException at runtime).
                            // Instead, when it's already parented under a different menu, add a lightweight
                            // placeholder MenuItem (same header/tag) so the menu structure is represented
                            // without moving the original visual element.
                            if (subParent.Parent == null)
                            {
                                parentMenuItem.Items.Add(subParent);
                            }
                            else if (!object.ReferenceEquals(subParent.Parent, parentMenuItem))
                            {
                                var placeholder = new MenuItem { Header = subParent.Header, Tag = subParent.Tag };
                                parentMenuItem.Items.Add(placeholder);
                            }
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
                            if (!string.IsNullOrEmpty(entry.Metadata?.MenuIcon))
                            {
                                try
                                {
                                    var imgSource = Images.LoadImage(entry.Metadata.MenuIcon);
                                    if (imgSource != null)
                                    {
                                        var img = new Avalonia.Controls.Image
                                        {
                                            Width = 16,
                                            Height = 16,
                                            Source = imgSource
                                        };
                                        menuItem.Icon = img;
                                        // Remember the key so we can reload when theme changes
                                        menuItem.SetValue(MenuIconKeyProperty, entry.Metadata.MenuIcon);
                                    }
                                }
                                catch { }
                            }
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
                            // When the theme stored in SessionSettings changes, the application theme
                            // will be applied via ThemeManager and MessageBus. Also refresh menu icons
                            // to pick up themed variants (Assets/Dark/...)
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => RefreshMenuIcons(mainMenu));
                        }
                    };
                }
            }

            // Subscribe to theme change notifications and refresh icons when theme changes.
            try
            {
                MessageBus<ThemeChangedEventArgs>.Subscribers += (_, __) => {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => RefreshMenuIcons(mainMenu));
                };
            }
            catch
            {
                // Ignore if MessageBus is unavailable for any reason.
            }

            // On macOS, default behavior is to hide the Avalonia menu and mirror it to the native menu bar.
            // Respect the Rover-level preference if present: when the user opted to "Keep main menu visible on macOS",
            // do not perform the native mirroring and ensure the Avalonia menu stays visible.
            if (OperatingSystem.IsMacOS())
            {
                try
                {
                    var roverSettings = settingsService?.GetSettings<ProjectRoverSettingsSection>();
                    var keepAvaloniaMenu = roverSettings?.ShowAvaloniaMainMenuOnMac ?? false;

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

        static void RefreshMenuIcons(Menu mainMenu)
        {
            if (mainMenu == null) return;
            foreach (var item in mainMenu.Items.OfType<MenuItem>())
            {
                RefreshMenuItemIconsRecursive(item);
            }
        }

        static void RefreshMenuItemIconsRecursive(MenuItem mi)
        {
            if (mi == null) return;

            try
            {
                var iconKey = mi.GetValue(MenuIconKeyProperty);
                if (!string.IsNullOrEmpty(iconKey))
                {
                    var imgSource = Images.LoadImage(iconKey);
                    if (imgSource != null)
                    {
                        mi.Icon = new Avalonia.Controls.Image { Width = 16, Height = 16, Source = imgSource };
                    }
                    else
                    {
                        mi.Icon = null;
                    }
                }
            }
            catch
            {
                // Ignore failures while reloading icons
            }

            if (mi.Items != null)
            {
                foreach (var child in mi.Items.OfType<MenuItem>())
                {
                    RefreshMenuItemIconsRecursive(child);
                }
            }
        }

        static MenuItem GetOrAddParentMenuItem(Menu mainMenu, Dictionary<string, MenuItem> parentMenuItems, string menuId)
        {
            if (menuId == null) menuId = string.Empty;
            if (!parentMenuItems.TryGetValue(menuId, out var parentMenuItem))
            {
                // Search entire menu tree (not only top-level) for an existing MenuItem with matching Tag.
                MenuItem? FindInChildren(MenuItem item)
                {
                    if (string.Equals((item.Tag as string) ?? string.Empty, menuId, StringComparison.Ordinal))
                        return item;
                    if (item.Items != null)
                    {
                        foreach (var child in item.Items.OfType<MenuItem>())
                        {
                            var found = FindInChildren(child);
                            if (found != null)
                                return found;
                        }
                    }
                    return null;
                }

                MenuItem? topLevel = null;
                foreach (var mi in mainMenu.Items.OfType<MenuItem>())
                {
                    topLevel = FindInChildren(mi);
                    if (topLevel != null)
                        break;
                }

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
