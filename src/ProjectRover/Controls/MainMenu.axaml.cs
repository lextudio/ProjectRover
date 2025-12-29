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
using ICSharpCode.ILSpy.ViewModels;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TomsToolbox.Essentials;
using System.Reflection;
using Avalonia.Svg.Skia;

namespace ICSharpCode.ILSpy.Controls
{
    public partial class MainMenu : UserControl
    {
        private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("MainMenu");
        // Attached property to remember the original icon key for each MenuItem so we can reload
        // the themed image when the application theme changes.
        public static readonly AttachedProperty<string?> MenuIconKeyProperty =
            Avalonia.AvaloniaProperty.RegisterAttached<MainMenu, MenuItem, string?>("MenuIconKey");

        private EventHandler<TabPagesCollectionChangedEventArgs>? _windowMenuTabPagesChangedHandler;
        private readonly Dictionary<MenuItem, Action> _windowMenuTabPageUpdaters = new();
        private EventHandler<ThemeChangedEventArgs>? _windowMenuThemeChangedHandler;
        private readonly List<MenuItem> _themeMenuItems = new();
        private List<NativeMenuItem> _nativeThemeMenuItems = new();
        private SettingsService? _settingsService;
        private const string TabPageMenuIconPath = "/Assets/Checkmark.svg";
        private NativeMenu? _nativeWindowSubmenu; // Track the Window submenu for dynamic updates
        private readonly Dictionary<ICSharpCode.ILSpy.ViewModels.TabPageModel, NativeMenuItem> _nativeTabPageItems = new();
        private NativeMenuItemSeparator? _nativeTabPageSeparator; // Separator before tab pages
        private ICSharpCode.ILSpy.Docking.DockWorkspace? _dockWorkspace; // Track for active tab updates

        public MainMenu()
        {
            InitializeComponent();

            this.AttachedToVisualTree += HandleAttachedToVisualTree;
        }

        private void HandleAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            var exportProvider = ProjectRover.App.ExportProvider;
            if (exportProvider == null)
                return;

            // Delay to ensure visual tree is fully constructed and attached to a TopLevel
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                InitMainMenu(Menu, exportProvider);

                // Initialize Window menu with tool panes and tab pages
                var windowMenu = Menu.Items.OfType<MenuItem>().FirstOrDefault(m => (m.Tag as string) == "_Window");
                if (windowMenu != null)
                {
                    var dockWorkspace = exportProvider.GetExportedValue<ICSharpCode.ILSpy.Docking.DockWorkspace>();
                    if (dockWorkspace != null)
                    {
                        InitWindowMenu(windowMenu, dockWorkspace);
                        if (OperatingSystem.IsMacOS())
                        {
                            BuildNativeMenu(Menu);
                        }
                    }
                }
            });
        }

        void InitMainMenu(Menu mainMenu, IExportProvider exportProvider)
        {
            // Get all main menu commands exported with contract "MainMenuCommand"
            var mainMenuCommands = exportProvider.GetExports<ICommand, IMainMenuCommandMetadata>("MainMenuCommand").ToArray();
            var settingsService = exportProvider.GetExportedValue<SettingsService>();
            _settingsService = settingsService;

            var parentMenuItems = new Dictionary<string, MenuItem>();
            var menuGroups = mainMenuCommands.OrderBy(c => c.Metadata?.MenuOrder).GroupBy(c => c.Metadata?.ParentMenuID).ToArray();
            _themeMenuItems.Clear();

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
                            ApplyGesture(menuItem, entry.Metadata?.InputGestureText, cmd);
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
                                menuItem.IsChecked = string.Equals(ThemeManager.Current.Theme, headerText ?? entry.Metadata?.Header, StringComparison.OrdinalIgnoreCase);
                                menuItem.Click += (_, _) => MainMenuThemeHelpers.ApplyThemeFromHeader(menuItem.Header?.ToString(), settingsService);
                                _themeMenuItems.Add(menuItem);
                            }
                        if (string.Equals(entry.Metadata?.ParentMenuID, "_Help", StringComparison.OrdinalIgnoreCase) && menuItem.Icon == null)
                        {
                            // Reserve icon column width so Help menu width matches other menus
                            menuItem.Icon = new Border { Width = 16, Height = 16, Opacity = 0 };
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

            if (_themeMenuItems.Count > 0)
            {
                MainMenuThemeHelpers.UpdateThemeChecks(_themeMenuItems, settingsService?.SessionSettings?.Theme ?? ThemeManager.Current.Theme);
                MainMenuThemeHelpers.UpdateNativeThemeChecks(_nativeThemeMenuItems, settingsService?.SessionSettings?.Theme ?? ThemeManager.Current.Theme);
                SetupThemeMenuCheckIcons(_themeMenuItems);

                var sessionSettings = settingsService?.SessionSettings;
                if (sessionSettings != null)
                {
                    sessionSettings.PropertyChanged += (_, e) => {
                        if (e.PropertyName == nameof(sessionSettings.Theme))
                        {
                            MainMenuThemeHelpers.UpdateThemeChecks(_themeMenuItems, sessionSettings.Theme);
                            MainMenuThemeHelpers.UpdateNativeThemeChecks(_nativeThemeMenuItems, sessionSettings.Theme);
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

            SetupVisibilityMenuCheckIcons(mainMenu, settingsService);
            SetupLanguageMenuCheckIcons(mainMenu, settingsService);

            // Note: Native menu will be built after InitWindowMenu completes (when all dynamic items are added)
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

        void InitWindowMenu(MenuItem windowMenuItem, ICSharpCode.ILSpy.Docking.DockWorkspace dockWorkspace)
        {
            _dockWorkspace = dockWorkspace;
            
            // Store default items (MEF-exported items like Close All Documents, Reset Layout)
            var defaultItems = windowMenuItem.Items.OfType<Control>().ToList();
            windowMenuItem.Items.Clear();
            _windowMenuTabPageUpdaters.Clear();
            if (_windowMenuThemeChangedHandler == null)
            {
                _windowMenuThemeChangedHandler = (_, __) => {
                    var updaters = _windowMenuTabPageUpdaters.Values.ToArray();
                    foreach (var updater in updaters)
                    {
                        updater();
                    }
                };
                MessageBus<ThemeChangedEventArgs>.Subscribers += _windowMenuThemeChangedHandler;
            }

            // Create menu items for tool panes (Assemblies, Analyze, Search, etc.)
            var toolItems = dockWorkspace.ToolPanes.Select(toolPane => CreateToolPaneMenuItem(toolPane, dockWorkspace)).ToArray();
            
            // Create list for tab pages (open documents) with live updates
            var tabPageMenuItems = new List<MenuItem>();
            var separatorBeforeTabPages = new Separator();
            var initialTabPageCount = dockWorkspace.TabPages.Count;
            
            foreach (var tabPage in dockWorkspace.TabPages)
            {
                tabPageMenuItems.Add(CreateTabPageMenuItem(tabPage, dockWorkspace, _windowMenuTabPageUpdaters));
            }

            // Listen to tab page collection changes via MessageBus to keep menu in sync
            _windowMenuTabPagesChangedHandler = (_, e) => {
                // Convert wrapped event args to NotifyCollectionChangedEventArgs
                System.Collections.Specialized.NotifyCollectionChangedEventArgs args = e;
                
                if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && args.NewItems != null)
                {
                    log.Debug("TabPages.Add event fired, count={Count}", args.NewItems.Count);
                    foreach (var newItem in args.NewItems.OfType<ICSharpCode.ILSpy.ViewModels.TabPageModel>())
                    {
                        log.Debug("Creating menu item for tab: {Title}", newItem.Title);
                        var menuItem = CreateTabPageMenuItem(newItem, dockWorkspace, _windowMenuTabPageUpdaters);
                        tabPageMenuItems.Add(menuItem);
                        
                        // Add separator before first tab page if not already present
                        if (tabPageMenuItems.Count == 1 && separatorBeforeTabPages.Parent == null)
                        {
                            windowMenuItem.Items.Add(separatorBeforeTabPages);
                        }
                        windowMenuItem.Items.Add(menuItem);
                        log.Debug("Added menu item to Window menu, total items={Count}", windowMenuItem.Items.Count);
                    }
                    if (OperatingSystem.IsMacOS())
                    {
                        log.Debug("Scheduling native menu tab add");
                        // Instead of rebuilding the entire native menu, directly add items to the Window submenu
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => {
                                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                    log.Debug("Executing native tab add");
                                    foreach (var newItem in args.NewItems.OfType<ICSharpCode.ILSpy.ViewModels.TabPageModel>())
                                    {
                                        if (_nativeWindowSubmenu != null && !_nativeTabPageItems.ContainsKey(newItem))
                                        {
                                            // Add separator before first tab page
                                            if (_nativeTabPageItems.Count == 0 && _nativeTabPageSeparator == null)
                                            {
                                                _nativeTabPageSeparator = new NativeMenuItemSeparator();
                                                _nativeWindowSubmenu.Items.Add(_nativeTabPageSeparator);
                                                log.Debug("Added separator before tab pages");
                                            }
                                            
                                            var nativeItem = CreateNativeTabPageMenuItem(newItem, dockWorkspace);
                                            _nativeTabPageItems[newItem] = nativeItem;
                                            _nativeWindowSubmenu.Items.Add(nativeItem);
                                            log.Debug("Added native tab item: {Title} (dictionary now has {Count} items)", newItem.Title, _nativeTabPageItems.Count);
                                        }
                                    }
                                    // Update states immediately after adding to show checkmark on active tab
                                    UpdateNativeTabPageStates();
                                });
                            });
                        });
                    }
                }
                else if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && args.OldItems != null)
                {
                    log.Debug("TabPages.Remove event fired, count={Count}", args.OldItems.Count);
                    var toRemove = windowMenuItem.Items.OfType<MenuItem>().Where(mi => {
                        var tabPage = (ICSharpCode.ILSpy.ViewModels.TabPageModel?)mi.Tag;
                        return args.OldItems.OfType<ICSharpCode.ILSpy.ViewModels.TabPageModel>().Any(tp => tp == tabPage);
                    }).ToArray();
                    
                    foreach (var item in toRemove)
                    {
                        var tabPage = (ICSharpCode.ILSpy.ViewModels.TabPageModel?)item.Tag;
                        log.Debug("Removing menu item for tab: {Title}", tabPage?.Title ?? "unknown");
                        windowMenuItem.Items.Remove(item);
                        tabPageMenuItems.Remove(item);
                        _windowMenuTabPageUpdaters.Remove(item);
                    }
                    
                    // Remove separator if no tab pages left
                    if (tabPageMenuItems.Count == 0 && windowMenuItem.Items.Contains(separatorBeforeTabPages))
                    {
                        windowMenuItem.Items.Remove(separatorBeforeTabPages);
                    }

                    if (OperatingSystem.IsMacOS())
                    {
                        // Instead of rebuilding the entire native menu, directly remove items from the Window submenu
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => {
                                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                    foreach (var oldItem in args.OldItems.OfType<ICSharpCode.ILSpy.ViewModels.TabPageModel>())
                                    {
                                        if (_nativeWindowSubmenu != null && _nativeTabPageItems.TryGetValue(oldItem, out var nativeItem))
                                        {
                                            _nativeWindowSubmenu.Items.Remove(nativeItem);
                                            _nativeTabPageItems.Remove(oldItem);
                                            log.Debug("Removed native tab item: {Title}", oldItem.Title);
                                        }
                                    }
                                    
                                    // Remove separator if no tab pages left
                                    if (_nativeTabPageItems.Count == 0 && _nativeTabPageSeparator != null && _nativeWindowSubmenu != null)
                                    {
                                        _nativeWindowSubmenu.Items.Remove(_nativeTabPageSeparator);
                                        _nativeTabPageSeparator = null;
                                        log.Debug("Removed tab page separator");
                                    }
                                });
                            });
                        });
                    }
                }
            };
            MessageBus<TabPagesCollectionChangedEventArgs>.Subscribers += _windowMenuTabPagesChangedHandler;

            // Add default items (Close All Documents, Reset Layout)
            foreach (var item in defaultItems)
            {
                windowMenuItem.Items.Add(item);
            }

            // Add separator before tool panes if there are any
            if (toolItems.Length > 0 && windowMenuItem.Items.Count > 0)
            {
                windowMenuItem.Items.Add(new Separator());
            }

            // Add tool pane items (Assemblies, Analyze, Search, etc.)
            foreach (var item in toolItems)
            {
                windowMenuItem.Items.Add(item);
            }

            // Add separator before tab pages and initial tab page items
            if (initialTabPageCount > 0 && windowMenuItem.Items.Count > 0)
            {
                windowMenuItem.Items.Add(separatorBeforeTabPages);
            }

            // Add initial tab page items (open documents)
            foreach (var item in tabPageMenuItems)
            {
                windowMenuItem.Items.Add(item);
            }

            // Note: Native menu will be built after this method completes in the AttachedToVisualTree handler
            
            // Listen for active tab changes to update native menu items
            if (OperatingSystem.IsMacOS())
            {
                dockWorkspace.PropertyChanged += (_, e) => {
                    log.Debug("DockWorkspace.PropertyChanged: {Property}", e.PropertyName);
                    if (e.PropertyName == nameof(dockWorkspace.ActiveTabPage))
                    {
                        log.Debug("ActiveTabPage changed to: {Title}", dockWorkspace.ActiveTabPage?.Title ?? "null");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateNativeTabPageStates());
                    }
                };
                
                // Also listen for property changes on individual TabPageModel objects
                // This handles cases where the same tab is reused but its Title property changes
                void HookTabPagePropertyChanged(ICSharpCode.ILSpy.ViewModels.TabPageModel tabPage)
                {
                    tabPage.PropertyChanged += (_, e) => {
                        log.Debug("TabPageModel '{Title}' PropertyChanged: {Property}", tabPage.Title, e.PropertyName);
                        if (e.PropertyName == nameof(tabPage.Title))
                        {
                            log.Debug("Tab title changed, updating native menu");
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                // Update the native menu item's header
                                if (_nativeTabPageItems.TryGetValue(tabPage, out var nativeItem))
                                {
                                    TrySetNativeMenuItemHeader(nativeItem, tabPage.Title);
                                }
                            });
                        }
                    };
                }

                static void TrySetNativeMenuItemHeader(NativeMenuItem nativeItem, string? header)
                {
                    try
                    {
                        var pi = nativeItem.GetType().GetProperty("Header", BindingFlags.Public | BindingFlags.Instance);
                        if (pi != null && pi.CanWrite)
                        {
                            pi.SetValue(nativeItem, header);
                            log.Debug("Updated native menu item header to: {Header}", header);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Failed to set native menu header");
                    }
                }
                
                // Hook existing tabs
                foreach (var tabPage in dockWorkspace.TabPages)
                {
                    HookTabPagePropertyChanged(tabPage);
                }
                
                // Hook future tabs when they're added
                MessageBus<TabPagesCollectionChangedEventArgs>.Subscribers += (_, wrappedArgs) => {
                    if (wrappedArgs is null) return;
                    var args = (System.Collections.Specialized.NotifyCollectionChangedEventArgs)wrappedArgs;
                    if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && args.NewItems != null)
                    {
                        foreach (var newItem in args.NewItems.OfType<ICSharpCode.ILSpy.ViewModels.TabPageModel>())
                        {
                            HookTabPagePropertyChanged(newItem);
                        }
                    }
                };
            }
        }

        static MenuItem CreateToolPaneMenuItem(ICSharpCode.ILSpy.ViewModels.ToolPaneModel toolPane, ICSharpCode.ILSpy.Docking.DockWorkspace dockWorkspace)
        {
            var menuItem = new MenuItem
            {
                Header = toolPane.Title,
                Tag = toolPane.ContentId,
                Command = toolPane.AssociatedCommand ?? new ICSharpCode.ILSpy.Commands.ToolPaneCommand(toolPane.ContentId, dockWorkspace)
            };
            if (toolPane.ShortcutKey != null)
            {
                menuItem.HotKey = toolPane.ShortcutKey;
                menuItem.InputGesture = toolPane.ShortcutKey;
            }

            // Add icon if available
            if (!string.IsNullOrEmpty(toolPane.Icon))
            {
                try
                {
                    var imgSource = Images.LoadImage(toolPane.Icon);
                    if (imgSource != null)
                    {
                        menuItem.Icon = new Avalonia.Controls.Image
                        {
                            Width = 16,
                            Height = 16,
                            Source = imgSource
                        };
                        // Remember icon key for theme reload
                        menuItem.SetValue(MenuIconKeyProperty, toolPane.Icon);
                    }
                }
                catch
                {
                    // Ignore icon loading errors
                }
            }

            return menuItem;
        }

        static MenuItem CreateTabPageMenuItem(ICSharpCode.ILSpy.ViewModels.TabPageModel tabPage, ICSharpCode.ILSpy.Docking.DockWorkspace dockWorkspace, IDictionary<MenuItem, Action> tabPageMenuItemUpdaters)
        {
            var header = new TextBlock
            {
                MaxWidth = 200,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            
            // Bind header to tab page title
            header.SetBinding(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(tabPage.Title))
            {
                Source = tabPage
            });

            var menuItem = new MenuItem
            {
                Header = header,
                Tag = tabPage,
                Command = new ICSharpCode.ILSpy.Commands.TabPageCommand(tabPage, dockWorkspace),

            };
            // remember the checkmark icon key so RefreshMenuIcons can reload it when theme changes
            menuItem.SetValue(MenuIconKeyProperty, TabPageMenuIconPath);

            // Update menu item when active tab changes
            dockWorkspace.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(dockWorkspace.ActiveTabPage))
                {
                    UpdateState();
                }
            };

            // Set initial state
            UpdateState();

            void UpdateState()
            {
                var isActive = dockWorkspace.ActiveTabPage == tabPage;
                header.FontWeight = isActive ? FontWeight.Bold : FontWeight.Normal;
                menuItem.IsChecked = isActive;
                if (isActive)
                {
                    var iconSource = Images.LoadImage(TabPageMenuIconPath);
                    if (iconSource != null)
                    {
                        menuItem.Icon = new Avalonia.Controls.Image
                        {
                            Width = 16,
                            Height = 16,
                            Source = iconSource
                        };
                    }
                    else
                    {
                        menuItem.Icon = null;
                    }
                }
                else
                {
                    menuItem.Icon = null;
                }
            }

            tabPageMenuItemUpdaters[menuItem] = UpdateState;
            return menuItem;
        }

        static void SetupThemeMenuCheckIcons(IEnumerable<MenuItem> themeMenuItems)
        {
            foreach (var item in themeMenuItems)
            {
                void UpdateIcon()
                {
                    if (item.IsChecked == true)
                    {
                        var iconSource = Images.LoadImage(TabPageMenuIconPath);
                        item.SetValue(MenuIconKeyProperty, TabPageMenuIconPath);
                        item.Icon = iconSource == null ? null : new Avalonia.Controls.Image { Width = 16, Height = 16, Source = iconSource };
                    }
                    else
                    {
                        item.Icon = null;
                    }
                }

                item.PropertyChanged += (_, e) => {
                    if (e.Property == MenuItem.IsCheckedProperty)
                    {
                        UpdateIcon();
                    }
                };

                UpdateIcon();
            }
        }

        static void SetupVisibilityMenuCheckIcons(Menu mainMenu, SettingsService? settingsService)
        {
            if (settingsService == null)
                return;

            var viewMenu = mainMenu.Items.OfType<MenuItem>().FirstOrDefault(mi => string.Equals(mi.Tag as string, "_View", StringComparison.Ordinal));
            if (viewMenu == null)
                return;

            var langSettings = settingsService.SessionSettings?.LanguageSettings;
            if (langSettings == null)
                return;

            foreach (var item in viewMenu.Items.OfType<MenuItem>())
            {
                var tag = item.Tag as string;
                if (!IsApiVisibilityMenuItem(item))
                    continue;

                void UpdateIcon()
                {
                    if (item.IsChecked == true)
                    {
                        var iconSource = Images.LoadImage(TabPageMenuIconPath);
                        item.SetValue(MenuIconKeyProperty, TabPageMenuIconPath);
                        item.Icon = iconSource == null ? null : new Avalonia.Controls.Image { Width = 16, Height = 16, Source = iconSource };
                    }
                    else
                    {
                        item.Icon = null;
                    }
                }

                item.PropertyChanged += (_, e) => {
                    if (e.Property == MenuItem.IsCheckedProperty)
                    {
                        UpdateIcon();
                    }
                };

                item.Click += (_, _) => ApplyApiVisibilitySelection(settingsService, tag);
                UpdateIcon();
            }
        }

        static bool IsApiVisibilityMenuItem(MenuItem menuItem)
        {
            return string.Equals(menuItem.Tag as string, "ApiVisPublicOnly", StringComparison.Ordinal)
                || string.Equals(menuItem.Tag as string, "ApiVisPublicAndInternal", StringComparison.Ordinal)
                || string.Equals(menuItem.Tag as string, "ApiVisAll", StringComparison.Ordinal);
        }

        static bool IsLanguageMenuItem(MenuItem menuItem)
        {
            var tag = menuItem.Tag as string;
            return tag != null && tag.StartsWith("Language:", StringComparison.Ordinal);
        }

        static void ApplyApiVisibilitySelection(SettingsService? settingsService, string? tag)
        {
            if (settingsService?.SessionSettings?.LanguageSettings == null)
                return;

            var lang = settingsService.SessionSettings.LanguageSettings;
            switch (tag)
            {
                case "ApiVisPublicOnly":
                    lang.ApiVisPublicOnly = true;
                    break;
                case "ApiVisPublicAndInternal":
                    lang.ApiVisPublicAndInternal = true;
                    break;
                case "ApiVisAll":
                    lang.ApiVisAll = true;
                    break;
            }
        }

        static void TryApplyNativeMenuIcon(NativeMenuItem native, MenuItem menuItem)
        {
            var src = LoadNativeMenuImage(menuItem);

            if (src == null)
                return;

            try
            {
                var iconProp = native.GetType().GetProperty("Icon", BindingFlags.Public | BindingFlags.Instance);
                if (iconProp == null || !iconProp.CanWrite)
                    return;

                var targetType = iconProp.PropertyType;
                if (!targetType.IsInstanceOfType(src))
                {
                    // Native menus usually expect a bitmap; rasterize vector images (e.g., SvgImage).
                    src = RasterizeImage(src, 16, 16) ?? src;
                }

                if (targetType.IsInstanceOfType(src))
                {
                    iconProp.SetValue(native, src);
                }
                else
                {
                    log.Debug("Native menu icon skipped: source={SourceType}, target={TargetType}", src.GetType().FullName, targetType.FullName);
                }
            }
                catch (Exception ex)
            {
                log.Error(ex, "Applying native menu icon failed");
            }
        }

        static IImage? RasterizeImage(IImage image, int width, int height)
        {
            try
            {
                var pixelSize = new PixelSize(Math.Max(1, width), Math.Max(1, height));
                var rtb = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
                using (var ctx = rtb.CreateDrawingContext())
                {
                    ctx.DrawImage(image, new Rect(image.Size), new Rect(new Point(0, 0), new Size(width, height)));
                }
                return rtb;
            }
                catch (Exception ex)
            {
                log.Error(ex, "RasterizeImage failed");
                return null;
            }
        }

        static void ApplyLanguageSelection(SettingsService? settingsService, string? tag)
        {
            if (settingsService?.SessionSettings == null)
                return;

            if (string.IsNullOrWhiteSpace(tag) || !tag.StartsWith("Language:", StringComparison.Ordinal))
                return;

            var culture = tag.Substring("Language:".Length);
            if (culture == "null")
                culture = null;

            settingsService.SessionSettings.CurrentCulture = culture;
        }

        static IImage? LoadNativeMenuImage(MenuItem menuItem)
        {
            // Prefer a theme-neutral load using platform theme (system) rather than app theme.
            string? iconKey = menuItem.GetValue(MenuIconKeyProperty);
            if (string.IsNullOrEmpty(iconKey))
            {
                if (menuItem.Icon is Avalonia.Controls.Image { Source: { } imgSrc })
                    return imgSrc;
                return null;
            }

            var platformTheme = Application.Current?.PlatformSettings?.GetColorValues()?.ThemeVariant;
            var preferDark = string.Equals(platformTheme?.ToString(), "Dark", StringComparison.OrdinalIgnoreCase);

            string? path = Images.ResolveIcon(iconKey);
            if (path == null)
                return null;

            // Normalize to avares://
            if (path.StartsWith("/"))
                path = $"avares://ProjectRover{path}";
            else if (!path.Contains("://"))
                path = $"avares://ProjectRover/Assets/{path}";

            if (!path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return Images.LoadImage(iconKey); // fallback

            var candidates = new List<string>();
            if (preferDark && path.Contains("/Assets/") && !path.Contains("/Assets/Dark/"))
            {
                var darkPath = path.Replace("/Assets/", "/Assets/Dark/", StringComparison.Ordinal);
                candidates.Add(darkPath);
            }
            candidates.Add(path);

            foreach (var candidate in candidates)
            {
                try
                {
                    var svg = SvgSource.Load(candidate, null);
                    if (svg != null)
                        return new SvgImage { Source = svg };
                }
                catch
                {
                    // try next
                }
            }

            return null;
        }

        static void SetupLanguageMenuCheckIcons(Menu mainMenu, SettingsService? settingsService)
        {
            if (settingsService == null)
                return;

            var viewMenu = mainMenu.Items.OfType<MenuItem>().FirstOrDefault(mi => string.Equals(mi.Tag as string, "_View", StringComparison.Ordinal));
            if (viewMenu == null)
                return;

            foreach (var item in viewMenu.Items.OfType<MenuItem>().SelectMany(i => i.Items.OfType<MenuItem>()))
            {
                if (!IsLanguageMenuItem(item))
                    continue;

                void UpdateIcon()
                {
                    if (item.IsChecked == true)
                    {
                        var iconSource = Images.LoadImage(TabPageMenuIconPath);
                        item.SetValue(MenuIconKeyProperty, TabPageMenuIconPath);
                        item.Icon = iconSource == null ? null : new Avalonia.Controls.Image { Width = 16, Height = 16, Source = iconSource };
                        item.SetValue(MenuIconKeyProperty, TabPageMenuIconPath);
                    }
                    else
                    {
                        item.Icon = null;
                    }
                }

                item.PropertyChanged += (_, e) => {
                    if (e.Property == MenuItem.IsCheckedProperty)
                    {
                        UpdateIcon();
                    }
                };

                item.Click += (_, _) => ApplyLanguageSelection(settingsService, item.Tag as string);
                UpdateIcon();
            }
        }

        static void ApplyGesture(MenuItem menuItem, string? gestureText, ICommand cmd)
        {
            // Prefer explicit metadata text; otherwise fall back to routed command gestures.
            var resolvedCommand = ICSharpCode.ILSpy.CommandWrapper.Unwrap(cmd);
            if (string.IsNullOrWhiteSpace(gestureText) && resolvedCommand is System.Windows.Input.RoutedCommand routed)
            {
                var routedGesture = routed.InputGestures?.OfType<KeyGesture>().FirstOrDefault();
                gestureText = routedGesture?.ToString();
            }
            else if (string.IsNullOrWhiteSpace(gestureText) && resolvedCommand is System.Windows.Input.RoutedUICommand routedUi)
            {
                var routedGesture = routedUi.InputGestures?.OfType<KeyGesture>().FirstOrDefault();
                gestureText = routedGesture?.ToString();
            }

            if (string.IsNullOrWhiteSpace(gestureText))
                return;

            try
            {
                // HotKey drives visual hint rendering in Avalonia menus
                var parsed = KeyGesture.Parse(gestureText);
                menuItem.HotKey = parsed;
                menuItem.InputGesture = parsed; // ensures hint is rendered even if HotKey isn't
            }
            catch
            {
                // Fallback: append gesture text so it is still visible
                if (menuItem.Header is string headerText)
                {
                    menuItem.Header = $"{headerText}\t{gestureText}";
                }
            }
        }

        void BuildNativeMenu(Menu mainMenu)
        {
            if (!OperatingSystem.IsMacOS())
                return;

            log.Debug("BuildNativeMenu: Starting native menu build");
            try
            {
                var settingsService = _settingsService;
                var roverSettings = settingsService?.GetSettings<ProjectRoverSettingsSection>();
                var keepAvaloniaMenu = roverSettings?.ShowAvaloniaMainMenuOnMac ?? false;

                var visualRoot = TopLevel.GetTopLevel(mainMenu) ?? mainMenu.GetVisualRoot();
                var rootObj = visualRoot as AvaloniaObject;

                mainMenu.IsVisible = keepAvaloniaMenu;

                _nativeThemeMenuItems = new List<NativeMenuItem>();

                var nativeRoot = new NativeMenu();
                var itemCount = 0;

                NativeMenuItem Convert(MenuItem m)
                {
                    var header = ResolveMenuHeader(m);
                    var isThemeMenuItem = _themeMenuItems.Contains(m);
                    var native = new NativeMenuItem { Header = header };
                    var isApiVisibilityItem = IsApiVisibilityMenuItem(m);
                    var isLanguageMenuItem = IsLanguageMenuItem(m);
                    var suppressNativeToggle = isThemeMenuItem || isApiVisibilityItem || isLanguageMenuItem;

                    itemCount++;
                    if (m.Tag is ICSharpCode.ILSpy.ViewModels.TabPageModel tp)
                    {
                        log.Debug("Convert: Converting tab page menu item: header={Header}, title={Title}", header, tp.Title);
                    }

                    if (m.Command != null)
                    {
                        native.Command = m.Command;
                        native.CommandParameter = m.CommandParameter;
                    }

                    TryApplyNativeMenuIcon(native, m);

                    if (!suppressNativeToggle && m.ToggleType != MenuItemToggleType.None)
                    {
                        native.ToggleType = (NativeMenuItemToggleType)m.ToggleType;
                        native.IsChecked = m.IsChecked == true;

                        m.PropertyChanged += (_, e) => {
                            if (e.Property == MenuItem.IsCheckedProperty)
                            {
                                native.IsChecked = m.IsChecked == true;
                            }
                        };
                    }

                    if (string.Equals(header, "Light", StringComparison.OrdinalIgnoreCase) || string.Equals(header, "Dark", StringComparison.OrdinalIgnoreCase))
                    {
                        _nativeThemeMenuItems.Add(native);
                    }

                    if (isThemeMenuItem)
                    {
                        native.Click += (_, _) => {
                            MainMenuThemeHelpers.ApplyThemeFromHeader(header, settingsService);
                            MainMenuThemeHelpers.UpdateThemeChecks(_themeMenuItems, ThemeManager.Current.Theme);
                            MainMenuThemeHelpers.UpdateNativeThemeChecks(_nativeThemeMenuItems, ThemeManager.Current.Theme);
                        };
                    }
                    else if (isApiVisibilityItem)
                    {
                        native.Click += (_, _) => ApplyApiVisibilitySelection(settingsService, m.Tag as string);
                    }
                    else if (isLanguageMenuItem)
                    {
                        native.Click += (_, _) => ApplyLanguageSelection(settingsService, m.Tag as string);
                    }
                    else if (m.ToggleType != MenuItemToggleType.None && m.Command == null)
                    {
                        native.Click += (_, _) => {
                            m.IsChecked = m.ToggleType == MenuItemToggleType.Radio ? true : !m.IsChecked;
                        };
                    }

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
                                    var convertedChild = Convert(childMi);
                                    sub.Items.Add(convertedChild);
                                    // Track tab page items for dynamic updates
                                    if (childMi.Tag is ICSharpCode.ILSpy.ViewModels.TabPageModel tabPageModel)
                                    {
                                        _nativeTabPageItems[tabPageModel] = convertedChild;
                                    }
                                    break;
                            }
                        }
                        native.Menu = sub;
                        
                        // Store the Window submenu for dynamic tab updates
                        if (string.Equals(header, "Window", StringComparison.OrdinalIgnoreCase))
                        {
                            _nativeWindowSubmenu = sub;
                            log.Debug("BuildNativeMenu: Stored Window submenu reference with {Count} items", sub.Items.Count);
                        }
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

                log.Debug("BuildNativeMenu: Converted {ItemCount} menu items total", itemCount);

                // Insert an explicit application menu to avoid the platform/Avalonia
                // falling back to a default menu (which can show "About Avalonia").
                try
                {
                    var appName = Application.Current?.Name
                                  ?? Assembly.GetEntryAssembly()?.GetName().Name
                                  ?? "Project Rover";
                    var appSub = new NativeMenu();
                    // Optionally populate appSub with About/Preferences/Quit entries.
                    var appMenuItem = new NativeMenuItem { Header = appName, Menu = appSub };
                    nativeRoot.Items.Insert(0, appMenuItem);
                    log.Debug("BuildNativeMenu: Inserted application menu '{AppName}' to override default app menu", appName);
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "BuildNativeMenu: Failed to insert application menu");
                }

                // Always set a fresh native menu; avoid reusing existing items to prevent parent conflicts.
                // Some native backends may throw when attempting to update menus that are still
                // referenced by the platform. Always clear existing menu first before setting new one.
                void TrySetNativeMenu(AvaloniaObject target)
                {
                    try
                    {
                        // Check if there's already a menu set, and clear it first to avoid update conflicts
                        var existingMenu = NativeMenu.GetMenu(target);
                        if (existingMenu != null)
                        {
                            log.Debug("BuildNativeMenu: Clearing existing native menu before setting new one");
                            try
                            {
                                NativeMenu.SetMenu(target, null);
                            }
                            catch (Exception clearEx)
                            {
                                log.Warning(clearEx, "BuildNativeMenu: Clear failed (will try setting new menu anyway)");
                            }
                        }
                        
                        NativeMenu.SetMenu(target, nativeRoot);
                        log.Debug("BuildNativeMenu: Successfully set native menu on target");
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "BuildNativeMenu: SetMenu failed");
                        // Last resort: try setting on Application.Current as fallback
                        if (!ReferenceEquals(target, Application.Current))
                        {
                            try
                            {
                                log.Debug("BuildNativeMenu: Trying Application.Current as fallback");
                                NativeMenu.SetMenu(Application.Current, nativeRoot);
                                log.Debug("BuildNativeMenu: Fallback succeeded");
                            }
                            catch (Exception fallbackEx)
                            {
                                log.Warning(fallbackEx, "BuildNativeMenu: Fallback also failed");
                            }
                        }
                    }
                }

                // Set native menu only on the window (rootObj); setting on both window and
                // Application.Current causes "menu does not match" errors on macOS
                if (rootObj != null)
                    TrySetNativeMenu(rootObj);

                log.Debug("BuildNativeMenu: Native menu set successfully");
                MainMenuThemeHelpers.UpdateNativeThemeChecks(_nativeThemeMenuItems, settingsService?.SessionSettings?.Theme ?? ThemeManager.Current.Theme);
            }
            catch (Exception ex)
            {
                log.Error(ex, "BuildNativeMenu: ERROR");
            }
        }
        
        static string ResolveMenuHeader(MenuItem m)
        {
            // For dynamic items (tab pages, tool panes), prefer getting the title from the Tag model
            // since TextBlock bindings may not be evaluated yet when native menu is built
            if (m.Tag is ICSharpCode.ILSpy.ViewModels.TabPageModel tp && !string.IsNullOrWhiteSpace(tp.Title))
            {
                log.Debug("ResolveMenuHeader: Resolved from TabPageModel.Title: {Title}", tp.Title);
                return tp.Title;
            }
            
            // For tool panes, the Header is already a string (toolPane.Title)
            if (m.Header is string hs)
            {
                log.Debug("ResolveMenuHeader: Resolved from string Header: {Header}", hs);
                return hs;
            }
                
            // For TextBlock headers, try to get the text
            if (m.Header is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
            {
                log.Debug("ResolveMenuHeader: Resolved from TextBlock.Text: {Text}", tb.Text);
                return tb.Text;
            }
            
            // Fallback to Tag if it's a string
            if (m.Tag is string ts)
            {
                log.Debug("ResolveMenuHeader: Resolved from string Tag: {Tag}", ts);
                return ts;
            }
            
            var fallback = m.Header?.ToString() ?? string.Empty;
            log.Debug("ResolveMenuHeader: Fallback to ToString: {Fallback}", fallback);
            return fallback;
        }

        NativeMenuItem CreateNativeTabPageMenuItem(ICSharpCode.ILSpy.ViewModels.TabPageModel tabPage, ICSharpCode.ILSpy.Docking.DockWorkspace dockWorkspace)
        {
            var nativeItem = new NativeMenuItem 
            { 
                Header = tabPage.Title ?? "Untitled",
                Command = new ICSharpCode.ILSpy.Commands.TabPageCommand(tabPage, dockWorkspace)
            };
            
            log.Debug("CreateNativeTabPageMenuItem: Created native item for: {Title}", tabPage.Title);
            return nativeItem;
        }

        void UpdateNativeTabPageStates()
        {
            if (_dockWorkspace == null) return;
            
            var activeTab = _dockWorkspace.ActiveTabPage;
            log.Debug("UpdateNativeTabPageStates: Updating for active tab: {Active}", activeTab?.Title ?? "none");
            log.Debug("UpdateNativeTabPageStates: Total native tab items: {Count}", _nativeTabPageItems.Count);
            
            foreach (var kvp in _nativeTabPageItems)
            {
                var tabPage = kvp.Key;
                var nativeItem = kvp.Value;
                var isActive = tabPage == activeTab;
                
                log.Debug("UpdateNativeTabPageStates: Checking tab {Title} (active={IsActive}, sameObject={SameObject})", tabPage.Title, isActive, ReferenceEquals(tabPage, activeTab));
                
                // Update header to reflect active state
                nativeItem.Header = tabPage.Title ?? "Untitled";
                
                // Add/remove checkmark icon based on active state
                if (isActive)
                {
                    try
                    {
                        // Create a temporary menu item to load the icon
                        var tempMenuItem = new MenuItem();
                        tempMenuItem.SetValue(MenuIconKeyProperty, TabPageMenuIconPath);
                        var iconSource = LoadNativeMenuImage(tempMenuItem);
                        if (iconSource != null)
                        {
                            TryApplyNativeMenuIcon(nativeItem, tempMenuItem);
                            log.Debug("UpdateNativeTabPageStates: Added checkmark to active tab {Title}", tabPage.Title);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warning(ex, "UpdateNativeTabPageStates: Failed to add icon");
                    }
                }
                else
                {
                    // Remove icon for inactive tabs
                    try
                    {
                        var iconProp = nativeItem.GetType().GetProperty("Icon", BindingFlags.Public | BindingFlags.Instance);
                        if (iconProp != null && iconProp.CanWrite)
                        {
                            iconProp.SetValue(nativeItem, null);
                            log.Debug("UpdateNativeTabPageStates: Removed checkmark from inactive tab {Title}", tabPage.Title);
                        }
                    }
                    catch { }
                }
                
                log.Debug("UpdateNativeTabPageStates: Tab {Title} active={IsActive}", tabPage.Title, isActive);
            }
        }
    }

    static class MainMenuThemeHelpers
    {
        private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("MainMenu");

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

            log.Debug("Theme menu click: {Header}", header);
            ThemeManager.Current.ApplyTheme(header);
            log.Debug("Application.ActualThemeVariant now {Variant}", Application.Current?.ActualThemeVariant);
            if (settingsService != null)
            {
                settingsService.SessionSettings.Theme = header;
                log.Debug("SessionSettings.Theme set to {Header}", header);
            }
        }
    }
}
