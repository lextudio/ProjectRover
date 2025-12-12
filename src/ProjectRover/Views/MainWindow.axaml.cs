/*
    Copyright 2024 CodeMerx
    Copyright 2025 LeXtudio Inc.
    This file is part of ProjectRover.

    ProjectRover is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ProjectRover is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with ProjectRover.  If not, see<https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using ProjectRover.Extensions;
using ProjectRover.Providers;
using ProjectRover.Services;
using ProjectRover.ViewModels;
using ProjectRover.Nodes;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using Microsoft.Extensions.Logging;
using TextMateSharp.Grammars;
using System.IO;

using static AvaloniaEdit.Utils.ExtensionMethods;

namespace ProjectRover.Views;

public partial class MainWindow : Window
{
    internal static readonly TextSegmentCollection<ReferenceTextSegment> references = new();
    private MainWindowViewModel? viewModel;
    private readonly RegistryOptions? registryOptions;
    private readonly TextMate.Installation? textMateInstallation;
    private AssemblyListPane leftDockView = null!;
    private DecompilerPane centerDockView = null!;
    private SearchPane searchDockView = null!;
    private Document? documentHost;
    private ToolDock? searchDock;
    private ProportionalDockSplitter? searchSplitter;
    private ProportionalDock? verticalLayout;
    private ProportionalDock? rightDock;
    private Factory dockFactory = null!;
    private InlineResourceElementGenerator? resourceInlineGenerator;

    private AvaloniaEdit.TextEditor TextEditor => centerDockView.Editor;
    private TreeView TreeView => leftDockView.ExplorerTreeView;
    public TextBox SearchTextBox => searchDockView.SearchTextBoxControl;
    
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(ILogger<MainWindow> logger, MainWindowViewModel mainWindowViewModel,
        IAnalyticsService analyticsService, IAutoUpdateService autoUpdateService, IAppInformationProvider appInformationProvider)
    {
        InitializeComponent();

        Program.UnhandledException += (_, e) =>
        {
            logger.LogCritical(e, "Unhandled exception occured");
        };
        
        viewModel = mainWindowViewModel;
        DataContext = viewModel;

        ConfigureDockLayout();

        _ = analyticsService.TrackEventAsync(AnalyticsEvents.Startup);
        // Swallowing the exceptions on purpose to avoid problems in the auto-update or remote additional info taking down the entire app
        _ = autoUpdateService.CheckForNewerVersionAsync();
        _ = appInformationProvider.TryLoadRemoteAdditionalInfoAsync();
        
        resourceInlineGenerator = new InlineResourceElementGenerator(
            () => viewModel?.InlineObjects,
            entry => viewModel?.SaveImageEntryAsync(entry),
            entry => viewModel?.SaveObjectEntryAsync(entry));
        TextEditor.TextArea.TextView.ElementGenerators.Insert(0, resourceInlineGenerator);
        TextEditor.TextArea.TextView.ElementGenerators.Add(new ReferenceElementGenerator());
        
        registryOptions = new RegistryOptions(ThemeName.LightPlus);
        textMateInstallation = TextEditor.InstallTextMate(registryOptions);
        textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".cs").Id));
        ApplyTheme(viewModel.SelectedTheme);
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        viewModel.PropertyChanged += ViewModelPropertyChanged;
        UpdateDocumentTitle();

        TextEditor.KeyDown += (_, args) =>
        {
            if (args.Handled)
                return;

            if (args.Key != Key.F12)
                return;

            var reference = references.FindSegmentsContaining(TextEditor.CaretOffset).FirstOrDefault();
            if (reference == null)
                return;

            if (reference.Resolved)
            {
                if (reference.MemberReference is System.Reflection.Metadata.EntityHandle handle)
                {
                    viewModel.SelectNodeByMemberReference(handle);
                }
                else if (reference.MemberReference is string fullName)
                {
                    viewModel.NavigateByFullName(fullName);
                }
            }
            else
            {
                viewModel.TryLoadUnresolvedReference();
            }

            args.Handled = true;
        };
        
        AddHandler(DragDrop.DragOverEvent, (_, args) =>
        {
            if (args.Handled)
                return;
            
#pragma warning disable CS0618 // Type or member is obsolete
            var data = args.Data;
            if (data?.Contains(DataFormats.FileNames) == true)
            {
                args.DragEffects &= DragDropEffects.Copy;
            }
            else
            {
                args.DragEffects &= DragDropEffects.None;
            }
#pragma warning restore CS0618 // Type or member is obsolete
        });
        
        AddHandler(DragDrop.DragEnterEvent, (_, args) =>
        {
            if (args.Handled)
                return;
            
#pragma warning disable CS0618 // Type or member is obsolete
            var data = args.Data;
            if (data?.Contains(DataFormats.FileNames) == true)
            {
                DragDropLabel.IsVisible = true;
            }
#pragma warning restore CS0618 // Type or member is obsolete
        });
        
        AddHandler(DragDrop.DragLeaveEvent, (_, args) =>
        {
            if (args.Handled)
                return;
            
            DragDropLabel.IsVisible = false;
        });
        
        AddHandler(DragDrop.DropEvent, (_, args) =>
        {
            if (args.Handled)
                return;
            
            DragDropLabel.IsVisible = false;
            
#pragma warning disable CS0618 // Type or member is obsolete
            var data = args.Data;
            if (data?.Contains(DataFormats.FileNames) != true)
                return;

            analyticsService.TrackEventAsync(AnalyticsEvents.OpenViaDragDrop);

            var files = data.GetFiles();
            if (files != null)
            {
                viewModel?.LoadAssemblies(files.Select(file => file.Path.LocalPath));
            }
#pragma warning restore CS0618 // Type or member is obsolete

            args.Handled = true;
        });

        TreeView.DoubleTapped += OnTreeViewDoubleTapped;
        TreeView.KeyDown += OnTreeViewKeyDown;
        
        PointerReleased += (_, args) =>
        {
            if (args.Handled)
                return;
            
            if (args.InitialPressMouseButton == MouseButton.XButton1 && viewModel.BackCommand.CanExecute(null))
            {
                viewModel.BackCommand.Execute(null);

                args.Handled = true;
            }
            else if (args.InitialPressMouseButton == MouseButton.XButton2 && viewModel.ForwardCommand.CanExecute(null))
            {
                viewModel.ForwardCommand.Execute(null);
                
                args.Handled = true;
            }
        };
    }

    private void ConfigureDockLayout()
    {
        leftDockView = new AssemblyListPane { DataContext = viewModel };
        centerDockView = new DecompilerPane { DataContext = viewModel };
        searchDockView = new SearchPane { DataContext = viewModel };

        documentHost = new Document
        {
            Id = "DocumentPane",
            Title = GetDocumentTitle(),
            Content = centerDockView,
            Context = viewModel,
            CanClose = false,
            CanFloat = false,
            CanPin = false
        };

        var documentDock = new DocumentDock
        {
            Id = "DocumentDock",
            Title = "DocumentDock",
            VisibleDockables = new ObservableCollection<IDockable> { documentHost },
            ActiveDockable = documentHost
        };

        var tool = new Tool
        {
            Id = "Explorer",
            Title = "Explorer",
            Content = leftDockView,
            Context = viewModel,
            CanClose = false,
            CanFloat = false,
            CanPin = false
        };

        var toolDock = new ToolDock
        {
            Id = "LeftDock",
            Title = "LeftDock",
            Alignment = Alignment.Left,
            VisibleDockables = new ObservableCollection<IDockable> { tool },
            ActiveDockable = tool
        };

        toolDock.Proportion = 0.3;
        documentDock.Proportion = 0.7;

        rightDock = new ProportionalDock
        {
            Id = "RightDock",
            Orientation = Dock.Model.Core.Orientation.Vertical,
            VisibleDockables = new ObservableCollection<IDockable>
            {
                documentDock
            },
            ActiveDockable = documentDock
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Dock.Model.Core.Orientation.Horizontal,
            VisibleDockables = new ObservableCollection<IDockable>
            {
                toolDock,
                new ProportionalDockSplitter { CanResize = true },
                rightDock
            },
            ActiveDockable = rightDock
        };

        var searchTool = new Tool
        {
            Id = "SearchTool",
            Title = "Search",
            Content = searchDockView,
            Context = viewModel,
            CanClose = false,
            CanFloat = false,
            CanPin = false
        };

        searchDock = new ToolDock
        {
            Id = "SearchDock",
            Title = "Search",
            Alignment = Alignment.Top,
            VisibleDockables = new ObservableCollection<IDockable> { searchTool },
            ActiveDockable = searchTool
        };
        searchDock.Proportion = 0.25;
        searchSplitter = new ProportionalDockSplitter { CanResize = true };

        verticalLayout = new ProportionalDock
        {
            Id = "VerticalLayout",
            Orientation = Dock.Model.Core.Orientation.Vertical,
            VisibleDockables = new ObservableCollection<IDockable>
            {
                mainLayout
            },
            ActiveDockable = mainLayout
        };

        var rootDock = new RootDock
        {
            Id = "Root",
            Title = "Root",
            VisibleDockables = new ObservableCollection<IDockable> { verticalLayout },
            ActiveDockable = verticalLayout
        };

        dockFactory = new Factory();
        DockHost.Factory = dockFactory;
        DockHost.Layout = rootDock;
        DockHost.InitializeFactory = true;
        DockHost.InitializeLayout = true;

        UpdateDocumentTitle();

        if (viewModel?.IsSearchDockVisible == true)
        {
            ShowSearchDock();
        }
    }

    private string GetDocumentTitle() =>
        viewModel?.AssemblyNodes.Count == 0 ? "New Tab" : "Document";

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedNode))
        {
            UpdateDocumentTitle();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.Document)
            || e.PropertyName == nameof(MainWindowViewModel.InlineObjects))
        {
            TextEditor.TextArea.TextView.Redraw();
        }
    }

    private void UpdateDocumentTitle()
    {
        if (documentHost == null)
            return;

        var name = viewModel?.SelectedNode?.Name;
        documentHost.Title = string.IsNullOrWhiteSpace(name) ? "New Tab" : name;
    }

    private void ShowSearchDock()
    {
        if (rightDock == null || searchDock == null || searchSplitter == null)
            return;

        var docks = rightDock.VisibleDockables;

        if (docks != null && !docks.Contains(searchDock))
        {
            dockFactory.InsertDockable(rightDock, searchDock, 0);
            dockFactory.InsertDockable(rightDock, searchSplitter, 1);
        }

        dockFactory.SetActiveDockable(searchDock);
        dockFactory.SetFocusedDockable(rightDock, searchDock);

        if (searchDock.ActiveDockable == null && searchDock.VisibleDockables?.Count > 0)
        {
            dockFactory.SetActiveDockable(searchDock.VisibleDockables[0]);
        }

        Dispatcher.UIThread.Post(() => SearchTextBox.Focus());

        if (viewModel != null)
        {
            viewModel.IsSearchDockVisible = true;
        }
    }

    private void OnSearchButtonClick(object? sender, RoutedEventArgs e)
    {
        ShowSearchDock();
    }

    private void OnTreeViewDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled)
            return;

        if (TreeView.SelectedItem is ResolvedReferenceNode resolved
            && !string.IsNullOrEmpty(resolved.FilePath)
            && File.Exists(resolved.FilePath))
        {
            var existing = viewModel?.FindAssemblyNodeByFilePath(resolved.FilePath);
            if (existing is not null)
            {
                if (viewModel != null)
                {
                    viewModel.SelectedNode = existing;
                }
                TreeView.Focus();
                e.Handled = true;
                return;
            }

            var added = viewModel?.LoadAssemblies(new[] { resolved.FilePath }, loadDependencies: true);
            var newAssembly = added?.FirstOrDefault();
            if (newAssembly is not null)
            {
                if (viewModel != null)
                {
                    viewModel.SelectedNode = newAssembly;
                }
                TreeView.Focus();
            }
            e.Handled = true;
        }
    }

    private void OnTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.Delete && TreeView.SelectedItem is AssemblyNode assemblyNode)
        {
            viewModel?.RemoveAssembly(assemblyNode);
            e.Handled = true;
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTheme))
        {
            ApplyTheme(viewModel?.SelectedTheme);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.Document)
            || e.PropertyName == nameof(MainWindowViewModel.InlineObjects))
        {
            TextEditor.TextArea.TextView.Redraw();
        }
    }

    private void ApplyTheme(MainWindowViewModel.ThemeOption? themeOption)
    {
        var variant = themeOption?.Variant ?? ThemeVariant.Light;
        Application.Current!.RequestedThemeVariant = variant;

        var textMateTheme = variant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
        if (textMateInstallation != null && registryOptions != null)
        {
            textMateInstallation.SetTheme(registryOptions.LoadTheme(textMateTheme));
        }
    }
    
    private void TreeView_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
    }

    private class InlineResourceElementGenerator : VisualLineElementGenerator
    {
        private readonly Func<IReadOnlyList<MainWindowViewModel.InlineObjectSpec>?> inlineSource;
        private readonly Action<MainWindowViewModel.ResourceImageEntry>? saveImage;
        private readonly Action<MainWindowViewModel.ResourceObjectEntry>? saveObject;

        public InlineResourceElementGenerator(Func<IReadOnlyList<MainWindowViewModel.InlineObjectSpec>?> inlineSource,
            Action<MainWindowViewModel.ResourceImageEntry>? saveImage = null,
            Action<MainWindowViewModel.ResourceObjectEntry>? saveObject = null)
        {
            this.inlineSource = inlineSource;
            this.saveImage = saveImage;
            this.saveObject = saveObject;
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            var inlines = inlineSource() ?? Array.Empty<MainWindowViewModel.InlineObjectSpec>();
            var next = inlines.Select(i => i.Offset).Where(o => o >= startOffset).DefaultIfEmpty(-1).Min();
            return next;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            var inlines = inlineSource() ?? Array.Empty<MainWindowViewModel.InlineObjectSpec>();
            var match = inlines.FirstOrDefault(i => i.Offset == offset);
            if (match == null)
                return null!;

            Control control = match.Kind switch
            {
                MainWindowViewModel.InlineObjectKind.Image when match.Image != null => BuildImageControl(match),
                MainWindowViewModel.InlineObjectKind.Table when match.Entries != null => BuildTableControl(match.Caption, match.Entries),
                MainWindowViewModel.InlineObjectKind.ObjectTable when match.ObjectEntries != null => BuildObjectTable(match.Caption, match.ObjectEntries),
                MainWindowViewModel.InlineObjectKind.Image when match.ImageEntries != null => BuildImageList(match.Caption, match.ImageEntries),
                _ => null!
            };

            if (control == null)
                return null!;

            return new InlineObjectElement(1, control);
        }

        private static Control BuildTableControl(string? caption, IReadOnlyList<KeyValuePair<string, string>> entries)
        {
            var stack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 4
            };

            if (!string.IsNullOrWhiteSpace(caption))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = caption,
                    FontWeight = FontWeight.SemiBold
                });
            }

            var list = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 2
            };

            foreach (var entry in entries)
            {
                list.Children.Add(new TextBlock
                {
                    Text = $"{entry.Key} = {entry.Value}"
                });
            }

            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Child = new ScrollViewer
                {
                    Content = list,
                    MaxHeight = 320,
                    MaxWidth = 640,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                }
            };

            stack.Children.Add(border);
            return stack;
        }

        private Control BuildImageControl(MainWindowViewModel.InlineObjectSpec spec)
        {
            var stack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 2
            };

            if (!string.IsNullOrWhiteSpace(spec.Caption))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = spec.Caption,
                    FontWeight = FontWeight.SemiBold
                });
            }

            stack.Children.Add(new Image
            {
                Source = spec.Image,
                Stretch = Stretch.None
            });

            if (saveImage != null && spec.ImageEntries == null)
            {
                var btn = new Button
                {
                    Content = "Save",
                    Padding = new Thickness(4)
                };
                btn.Click += (_, __) => saveImage(new MainWindowViewModel.ResourceImageEntry(spec.Caption ?? "image", spec.Image!, spec.Caption ?? string.Empty));
                stack.Children.Add(btn);
            }

            return stack;
        }

        private Control BuildObjectTable(string? caption, IReadOnlyList<MainWindowViewModel.ResourceObjectEntry> entries)
        {
            var stack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 4
            };

            if (!string.IsNullOrWhiteSpace(caption))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = caption,
                    FontWeight = FontWeight.SemiBold
                });
            }

            var list = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 2
            };

            foreach (var entry in entries)
            {
                list.Children.Add(new TextBlock
                {
                    Text = $"{entry.Name} [{entry.Type}] = {entry.Display}"
                });
                if (saveObject != null && entry.Raw != null)
                {
                    var btn = new Button
                    {
                        Content = "Save entry",
                        Padding = new Thickness(2)
                    };
                    btn.Click += (_, __) => saveObject(entry);
                    list.Children.Add(btn);
                }
            }

            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Child = new ScrollViewer
                {
                    Content = list,
                    MaxHeight = 320,
                    MaxWidth = 640,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                }
            };

            stack.Children.Add(border);
            return stack;
        }

        private Control BuildImageList(string? caption, IReadOnlyList<MainWindowViewModel.ResourceImageEntry> entries)
        {
            var stack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 4
            };

            if (!string.IsNullOrWhiteSpace(caption))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = caption,
                    FontWeight = FontWeight.SemiBold
                });
            }

            var list = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 6
            };

            foreach (var entry in entries)
            {
                var itemStack = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Vertical,
                    Spacing = 2
                };
                itemStack.Children.Add(new TextBlock { Text = $"{entry.Name} ({entry.Label})" });
                itemStack.Children.Add(new Image
                {
                    Source = entry.Image,
                    Stretch = Stretch.None
                });
                if (saveImage != null)
                {
                    var btn = new Button
                    {
                        Content = "Save entry",
                        Padding = new Thickness(2)
                    };
                    btn.Click += (_, __) => saveImage(entry);
                    itemStack.Children.Add(btn);
                }
                list.Children.Add(itemStack);
            }

            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Child = new ScrollViewer
                {
                    Content = list,
                    MaxHeight = 320,
                    MaxWidth = 640,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                }
            };

            stack.Children.Add(border);
            return stack;
        }
    }

    private class ReferenceElementGenerator : VisualLineElementGenerator
    {
        public override int GetFirstInterestedOffset(int startOffset)
        {
            return references.FindFirstSegmentWithStartAfter(startOffset)?.StartOffset ?? -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            var segment = references.FindSegmentsContaining(offset).First();
            return new ReferenceVisualLineText(CurrentContext.VisualLine, segment.Length, segment.MemberReference, segment.Resolved);
        }
    }
    
    private class ReferenceVisualLineText : VisualLineText
    {
        private static readonly KeyModifiers NavigateToKeyModifier =
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? KeyModifiers.Meta : KeyModifiers.Control;
        private static ReferenceVisualLineText? pressed;
        private readonly object memberReference;
        private readonly bool resolved;
        
        public ReferenceVisualLineText(VisualLine parentVisualLine, int length, object memberReference, bool resolved)
            : base(parentVisualLine, length)
        {
            this.memberReference = memberReference;
            this.resolved = resolved;
        }
        
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            if (!resolved)
            {
                TextRunProperties.SetForegroundBrush(Brushes.Red);
            }
            
            return base.CreateTextRun(startVisualColumn, context);
        }
        
        protected override void OnQueryCursor(PointerEventArgs e)
        {
            if (e.Handled)
            {
                base.OnQueryCursor(e);
                return;
            }

            if (e.Source is Control control)
            {
                if (e.KeyModifiers.HasFlag(NavigateToKeyModifier))
                {
                    control.Cursor = new Cursor(StandardCursorType.Hand);
                }
            }
            
            base.OnQueryCursor(e);
        }

        // protected override void OnPointerEntered(PointerEventArgs e)
        // {
        //     if (e.Handled)
        //     {
        //         base.OnQueryCursor(e);
        //         return;
        //     }
            
        //     if (e.Source is Control control)
        //     {
        //         if (!resolved)
        //         {
        //             var assemblyName = (memberReference.GetTopDeclaringTypeOrSelf().Scope as AssemblyNameReference)?.FullName;
        //             var message = memberReference switch
        //             {
        //                 TypeReference => $"Ambiguous type reference. Generic parameters might be present. Please, locate the assembly where the type is defined.{System.Environment.NewLine}{System.Environment.NewLine}Assembly name: {assemblyName}",
        //                 _ => $"Ambiguous reference. Please, locate the assembly where the member is defined.{System.Environment.NewLine}{System.Environment.NewLine}Assembly name: {assemblyName}"
        //             };
                    
        //             ToolTip.SetPlacement(control, PlacementMode.Pointer);
        //             ToolTip.SetTip(control, message);
        //             ToolTip.SetIsOpen(control, true);
        //         }
        //     }
            
        //     base.OnPointerEntered(e);
        // }

        // protected override void OnPointerExited(PointerEventArgs e)
        // {
        //     if (e.Handled)
        //     {
        //         base.OnPointerExited(e);
        //         return;
        //     }

        //     if (!resolved)
        //     {
        //         if (e.Source is Control control)
        //         {
        //             ToolTip.SetTip(control, null);
        //             ToolTip.SetIsOpen(control, false);
        //         }
        //     }
            
        //     base.OnPointerExited(e);
        // }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.Handled)
            {
                base.OnPointerPressed(e);
                return;
            }

            if (e.KeyModifiers.HasFlag(NavigateToKeyModifier))
            {
                var mainWindow = ((App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow as MainWindow)!;
                var textEditor = mainWindow.TextEditor;
                if (textEditor.TextArea.TextView.CapturePointer(e.Pointer))
                {
                    pressed = this;
                    e.Handled = true;
                }
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerEventArgs e)
        {
            if (e.Handled)
            {
                base.OnPointerReleased(e);
                return;
            }

            if (pressed == this)
            {
                var mainWindow = ((App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow as MainWindow)!;
                var textEditor = mainWindow.TextEditor;
                textEditor.TextArea.TextView.ReleasePointerCapture(e.Pointer);

                if (resolved)
                {
                    if (memberReference is System.Reflection.Metadata.EntityHandle handle)
                    {
                        mainWindow.viewModel?.SelectNodeByMemberReference(handle);
                    }
                }
                else
                {
                    mainWindow.viewModel?.TryLoadUnresolvedReference();
                }
                
                pressed = null;
                e.Handled = true;
            }

            base.OnPointerReleased(e);
        }
    }
}

public class ReferenceTextSegment : TextSegment
{
    public required object MemberReference { get; init; }
    public required bool Resolved { get; init; }
}
