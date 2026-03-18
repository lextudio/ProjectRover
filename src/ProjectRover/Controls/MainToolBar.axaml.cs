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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

            // Initial CanExecute can be evaluated before all toolbar buttons are attached.
            // Queue a requery so startup enabled/disabled state matches the current model.
            Dispatcher.UIThread.Post(CommandManager.InvalidateRequerySuggested, DispatcherPriority.Background);
        }

        static Button CreateToolbarItem(IExport<ICommand, IToolbarCommandMetadata> commandExport)
        {
            var command = commandExport.Value;
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
    }
}
