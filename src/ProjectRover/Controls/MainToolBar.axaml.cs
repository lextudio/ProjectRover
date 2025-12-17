using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using TomsToolbox.Composition;
using ICSharpCode.ILSpy.Commands;
using System.Windows.Input;
using System.IO;

namespace ICSharpCode.ILSpy.Controls
{
    public partial class MainToolBar : UserControl
    {
        public MainToolBar()
        {
            InitializeComponent();
            
            this.AttachedToVisualTree += (_, _) => {
                var exportProvider = ProjectRover.App.ExportProvider;
                if (exportProvider != null)
                {
                     InitToolbar(exportProvider);
                }
            };
        }

        void InitToolbar(IExportProvider exportProvider)
        {
            var navToolbar = this.FindControl<ItemsControl>("NavToolbarItems");
            var openToolbar = this.FindControl<ItemsControl>("OpenToolbarItems");
            var otherToolbar = this.FindControl<ItemsControl>("OtherToolbarItems");
            
            if (navToolbar == null || openToolbar == null || otherToolbar == null) return;

            navToolbar.Items.Clear();
            openToolbar.Items.Clear();
            otherToolbar.Items.Clear();
            
            var toolbarCommands = exportProvider
                .GetExports<ICommand, IToolbarCommandMetadata>("ToolbarCommand")
                .ToList();

            Console.WriteLine($"InitToolbar: Found {toolbarCommands.Count} toolbar commands.");
            foreach (var tc in toolbarCommands)
            {
                Console.WriteLine($"InitToolbar: Command metadata: Category={tc.Metadata?.ToolbarCategory}, Icon={tc.Metadata?.ToolbarIcon}, ToolTip={tc.Metadata?.ToolTip}");
            }

            // 1. Navigation
            var navCommands = toolbarCommands
                .Where(c => c.Metadata?.ToolbarCategory == "Navigation")
                .OrderBy(c => c.Metadata?.ToolbarOrder);
                
            foreach (var cmd in navCommands)
            {
                navToolbar.Items.Add(CreateToolbarItem(cmd));
            }

            // 2. Open
            var openCommands = toolbarCommands
                .Where(c => c.Metadata?.ToolbarCategory == "Open")
                .OrderBy(c => c.Metadata?.ToolbarOrder);
                
            foreach (var cmd in openCommands)
            {
                openToolbar.Items.Add(CreateToolbarItem(cmd));
            }
            
            // 3. Others
            var otherGroups = toolbarCommands
                .Where(c => c.Metadata?.ToolbarCategory != "Navigation" && c.Metadata?.ToolbarCategory != "Open")
                .GroupBy(c => c.Metadata?.ToolbarCategory);
                
            foreach (var group in otherGroups)
            {
                otherToolbar.Items.Add(new Border { 
                    Width = 1, 
                    Height = 16, 
                    Background = Brushes.Gray, 
                    Margin = new Thickness(4, 0) 
                });
                
                foreach (var cmd in group.OrderBy(c => c.Metadata?.ToolbarOrder))
                {
                    otherToolbar.Items.Add(CreateToolbarItem(cmd));
                }
            }
        }

        static Button CreateToolbarItem(IExport<ICommand, IToolbarCommandMetadata> commandExport)
        {
            var command = commandExport.Value;
            var iconPath = commandExport.Metadata?.ToolbarIcon;
            
            var button = new Button
            {
                Command = command,
                Tag = commandExport.Metadata?.Tag,
                Padding = new Thickness(4),
                Margin = new Thickness(2),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            ToolTip.SetTip(button, ICSharpCode.ILSpy.Util.ResourceHelper.GetString(commandExport.Metadata?.ToolTip));

            IImage? image = null;
            if (!string.IsNullOrEmpty(iconPath))
            {
                 image = LoadImage(iconPath);
            }
            
            if (image != null)
            {
                button.Content = new Image { Source = image, Width = 16, Height = 16 };
            }
            else
            {
                // Fallback text if no icon
                button.Content = commandExport.Metadata?.ToolTip ?? "Cmd"; 
            }

            return button;
        }

        static IImage? LoadImage(string iconPath)
        {
            // iconPath is like "Images/Open"
            // We want to map it to "Assets/Open.svg"
            
            var name = System.IO.Path.GetFileName(iconPath); // "Open"
            
            // Handle known mismatches
            if (name == "Sort") name = "SortAssemblies";
            
            // Try loading SVG from Assets
            var uri = new Uri($"avares://ProjectRover/Assets/{name}.svg");
            if (AssetLoader.Exists(uri))
            {
                return new SvgImage { Source = SvgSource.Load(uri.ToString(), uri) };
            }
            
            return null;
        }
    }
}
