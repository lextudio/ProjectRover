using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using ICSharpCode.ILSpy.Util;
using ProjectRover.Settings;

namespace ICSharpCode.ILSpy.Options
{
    public partial class DisplaySettingsPanel : UserControl
    {
        private bool isUpdatingFontList;
        private string? lastSelectedFont;

        public DisplaySettingsPanel()
        {
            InitializeComponent();
            this.AttachedToVisualTree += OnAttachedToVisualTree;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            try
            {
                var combo = this.FindControl<ComboBox>("FontComboBox");
                if (combo == null)
                    return;

                if (DataContext is not DisplaySettingsViewModel viewModel)
                    return;

                var displaySettings = viewModel.Settings;
                var settingsService = ProjectRover.App.ExportProvider?.GetExportedValue<SettingsService>();
                var roverSettings = settingsService?.GetSettings<ProjectRoverSettingsSection>();
                var recentFontsEnabled = roverSettings?.RecentFontsEnabled ?? true;

                // Enumerate system fonts via shim
                var systemFonts = System.Windows.Media.Fonts.SystemFontFamilies.Select(ff =>
                {
                    // try to get Source or Name
                    try { return ff.Name?.ToString() ?? ff.ToString(); } catch { return ff.ToString(); }
                }).ToList();
                var systemFontSet = new HashSet<string>(systemFonts, StringComparer.OrdinalIgnoreCase);

                var items = new List<string>();
                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void AddItem(string value)
                {
                    if (added.Add(value))
                        items.Add(value);
                }

                var recentFonts = recentFontsEnabled ? RecentFontsCache.Load() : Array.Empty<string>();
                var hasRecentFonts = false;
                if (recentFonts.Count > 0)
                {
                    foreach (var recent in recentFonts)
                    {
                        if (systemFontSet.Contains(recent))
                        {
                            AddItem(recent);
                            hasRecentFonts = true;
                        }
                    }
                    if (hasRecentFonts)
                        items.Add("──────────");
                }

                foreach (var f in systemFonts)
                {
                    AddItem(f);
                }

                combo.ItemsSource = items;

                // initialize selection - try Source or Name property, else ToString
                string? current = null;
                if (displaySettings.SelectedFont != null)
                {
                    current = displaySettings.SelectedFont.Name ?? displaySettings.SelectedFont.ToString();
                }
                if (!string.IsNullOrEmpty(current))
                {
                    var matched = items.FirstOrDefault(item =>
                        string.Equals(item, current, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(matched))
                        combo.SelectedItem = matched;
                }
                lastSelectedFont = combo.SelectedItem as string ?? current;

                combo.SelectionChanged += (s, ev) =>
                {
                    if (isUpdatingFontList)
                        return;

                    if (combo.SelectedItem is string sel)
                    {
                        if (sel == "──────────")
                        {
                            // ignore selecting the separator - restore previous selection
                            combo.SelectedItem = lastSelectedFont;
                            return;
                        }

                        displaySettings.SelectedFont = new FontFamily(sel);
                        lastSelectedFont = sel;

                        // update recent cache and refresh items immediately so changes show up while dialog is open
                        if (recentFontsEnabled)
                        {
                            isUpdatingFontList = true;
                            try
                            {
                                var updatedRecentFonts = RecentFontsCache.Update(sel);

                                // rebuild items to reflect new recent list
                                var newItems = new List<string>();
                                var newAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                var hasNewRecentFonts = false;

                                void AddNewItem(string value)
                                {
                                    if (newAdded.Add(value))
                                        newItems.Add(value);
                                }

                                foreach (var recent in updatedRecentFonts)
                                {
                                    if (systemFontSet.Contains(recent))
                                    {
                                        AddNewItem(recent);
                                        hasNewRecentFonts = true;
                                    }
                                }
                                if (hasNewRecentFonts)
                                    newItems.Add("──────────");
                                foreach (var f in systemFonts)
                                {
                                    AddNewItem(f);
                                }

                                // replace ItemsSource and reselect the chosen font
                                combo.ItemsSource = newItems;
                                combo.SelectedItem = sel;
                            }
                            finally
                            {
                                isUpdatingFontList = false;
                            }
                        }
                    }
                };
            }
            catch (Exception)
            {
                // ignore UI init errors
            }
        }
    }
}
