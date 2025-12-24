using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Commands;
using System.Windows.Input;
using TomsToolbox.Composition;

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
                if (!group.Any())
                    continue;

                otherToolbar.Items.Add(new Separator());
                
                foreach (var cmd in group.OrderBy(c => c.Metadata?.ToolbarOrder))
                {
                    otherToolbar.Items.Add(CreateToolbarItem(cmd));
                }
            }
        }

        static Button CreateToolbarItem(IExport<ICommand, IToolbarCommandMetadata> commandExport)
        {
            var command = CommandWrapper.Unwrap(commandExport.Value);
            var iconPath = commandExport.Metadata?.ToolbarIcon;

            var tooltip = ICSharpCode.ILSpy.Util.ResourceHelper.GetString(commandExport.Metadata?.ToolTip)
                ?? commandExport.Metadata?.ToolTip;

            var button = new Button
            {
                Command = command,
                Tag = commandExport.Metadata?.ToolbarIcon ?? commandExport.Metadata?.Tag
            };

            if (commandExport.Value is IProvideParameterBinding parameterBinding)
            {
                button.Bind(Button.CommandParameterProperty, parameterBinding.ParameterBinding);
            }

            ToolTip.SetTip(button, tooltip);

            if (!string.IsNullOrEmpty(iconPath))
            {
                // Create an Image control and bind its Source using IconKeyThemeToImageConverter
                var img = new Image();

                var multi = new Avalonia.Data.MultiBinding
                {
                    Converter = new ICSharpCode.ILSpy.Converters.IconKeyThemeToImageConverter(),
                    Bindings = new System.Collections.Generic.List<Avalonia.Data.IBinding>
                    {
                        new Avalonia.Data.Binding("Tag") { Source = button },
                        new Avalonia.Data.Binding("ActualThemeVariant") { Source = Application.Current }
                    }
                };

                img.Bind(Image.SourceProperty, multi);
                button.Content = img;
            }
            else
            {
                // Fallback text if no icon
                button.Content = tooltip ?? "Cmd";
            }

            return button;
        }

        // Note: image loading is delegated to Images.LoadImage to keep behavior consistent
    }
}
