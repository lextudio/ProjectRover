// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;

using Avalonia.Controls;

using AvaloniaEdit;

using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpyX.Search;
using ICSharpCode.ILSpyX.TreeView;

using TomsToolbox.Composition;
using TomsToolbox.Essentials;
using TomsToolbox.Wpf.Composition;
using Avalonia;

namespace ICSharpCode.ILSpy
{
	public interface IContextMenuEntry
	{
		bool IsVisible(TextViewContext context);
		bool IsEnabled(TextViewContext context);
		void Execute(TextViewContext context);
	}

	public class TextViewContext
	{
		/// <summary>
		/// Returns the selected nodes in the tree view.
		/// Returns null, if context menu does not belong to a tree view.
		/// </summary>
		public SharpTreeNode[] SelectedTreeNodes { get; private set; }

		/// <summary>
		/// Returns the tree view the context menu is assigned to.
		/// Returns null, if context menu is not assigned to a tree view.
		/// </summary>
		public TreeView TreeView { get; private set; }

		/// <summary>
		/// Returns the text view the context menu is assigned to.
		/// Returns null, if context menu is not assigned to a text view.
		/// </summary>
		public DecompilerTextView TextView { get; private set; }

		/// <summary>
		/// Returns the list box the context menu is assigned to.
		/// Returns null, if context menu is not assigned to a list box.
		/// </summary>
		public ListBox ListBox { get; private set; }

		/// <summary>
		/// Returns the data grid the context menu is assigned to.
		/// Returns null, if context menu is not assigned to a data grid.
		/// </summary>
		public DataGrid DataGrid { get; private set; }

		/// <summary>
		/// Returns the reference the mouse cursor is currently hovering above.
		/// Returns null, if there was no reference found.
		/// </summary>
		public ReferenceSegment Reference { get; private set; }

		/// <summary>
		/// Returns the position in TextView the mouse cursor is currently hovering above.
		/// Returns null, if TextView returns null;
		/// </summary>
		public TextViewPosition? Position { get; private set; }

		/// <summary>
		/// Returns the original source of the context menu event.
		/// </summary>
		public AvaloniaObject OriginalSource { get; private set; }

		public static TextViewContext Create(EventArgs eventArgs, TreeView treeView = null, DecompilerTextView textView = null, ListBox listBox = null, DataGrid dataGrid = null)
		{
			ReferenceSegment reference;
			Point? textViewPoint = null;

			if (textView is not null)
			{
				if (eventArgs is ContextRequestedEventArgs contextRequestedEventArgs
					&& contextRequestedEventArgs.TryGetPosition(textView, out var point))
				{
					textViewPoint = point;
				}
				reference = textViewPoint.HasValue
					? textView.GetReferenceSegmentAtMousePosition(textViewPoint.Value)
					: null;
			}
			else
			{
				reference = (listBox?.SelectedItem ?? dataGrid?.SelectedItem) switch {
					SearchResult searchResult => new() { Reference = searchResult.Reference },
					TreeNodes.IMemberTreeNode treeNode => new() { Reference = treeNode.Member }, { } value => new() { Reference = value },
					_ => null
				};
			}

			object originalSource = eventArgs switch
			{
				ContextRequestedEventArgs contextRequestedEventArgs => contextRequestedEventArgs.Source,
				DataGridContextMenuEventArgs dataGridContextMenuEventArgs => dataGridContextMenuEventArgs.OriginalSource,
				_ => null
			};

			var position = textViewPoint.HasValue ? textView?.GetPositionFromMousePosition(textViewPoint.Value) : null;
			var selectedTreeNodes = treeView?.GetTopLevelSelection().ToArray();

			return new() {
				ListBox = listBox,
				DataGrid = dataGrid,
				TreeView = treeView,
				TextView = textView,
				SelectedTreeNodes = selectedTreeNodes,
				Reference = reference,
				Position = position,
				OriginalSource = originalSource as DependencyObject
            };
		}
	}

	public interface IContextMenuEntryMetadata
	{
		string MenuID { get; }
		string ParentMenuID { get; }
		string Icon { get; }
		string Header { get; }
		string Category { get; }
		string InputGestureText { get; }

		double Order { get; }
	}

	[MetadataAttribute]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class ExportContextMenuEntryAttribute : ExportAttribute, IContextMenuEntryMetadata
	{
		public ExportContextMenuEntryAttribute()
			: base(typeof(IContextMenuEntry))
		{
			// entries default to end of menu unless given specific order position
			Order = double.MaxValue;
		}
		/// <summary>
		/// Gets/Sets the ID of this menu item. Menu entries are not required to have an ID,
		/// however, setting it allows to declare nested menu structures.
		/// Plugin authors are advised to use GUIDs as identifiers to prevent conflicts.
		/// <para/>
		/// NOTE: Defining cycles (for example by accidentally setting <see cref="MenuID"/> equal to <see cref="ParentMenuID"/>)
		/// will lead to a stack-overflow and crash of ILSpy at startup.
		/// </summary>
		public string MenuID { get; set; }
		/// <summary>
		/// Gets/Sets the parent of this menu item. All menu items sharing the same parent will be displayed as sub-menu items.
		/// If this property is set to <see langword="null"/>, the menu item is displayed in the top-level menu.
		/// <para/>
		/// NOTE: Defining cycles (for example by accidentally setting <see cref="MenuID"/> equal to <see cref="ParentMenuID"/>)
		/// will lead to a stack-overflow and crash of ILSpy at startup.
		/// </summary>
		public string ParentMenuID { get; set; }
		public string Icon { get; set; }
		public string Header { get; set; }
		public string Category { get; set; }
		public string InputGestureText { get; set; }
		public double Order { get; set; }
	}

	internal class ContextMenuProvider
	{
		private static readonly WeakEventSource<EventArgs> ContextMenuClosedEventSource = new();

		public static event EventHandler<EventArgs> ContextMenuClosed {
			add => ContextMenuClosedEventSource.Subscribe(value);
			remove => ContextMenuClosedEventSource.Unsubscribe(value);
		}

		/// <summary>
		/// Enables extensible context menu support for the specified tree view.
		/// </summary>
		public static void Add(TreeView treeView)
		{
			var provider = new ContextMenuProvider(treeView);
			treeView.ContextRequested += provider.treeView_ContextMenuOpening;
			// Context menu is shown only when the ContextMenu property is not null before the
			// ContextMenuOpening event handler is called.
			treeView.ContextMenu = new ContextMenu();
		}

		public static void Add(DecompilerTextView textView)
		{
			var provider = new ContextMenuProvider(textView);
			textView.ContextRequested += provider.textView_ContextMenuOpening;
			// Context menu is shown only when the ContextMenu property is not null before the
			// ContextMenuOpening event handler is called.
			textView.ContextMenu = new ContextMenu();
		}

		public static void Add(ListBox listBox)
		{
			var provider = new ContextMenuProvider(listBox);
			listBox.ContextRequested += provider.listBox_ContextMenuOpening;
			listBox.ContextMenu = new ContextMenu();
		}

		public static void Add(DataGrid dataGrid)
		{
			var provider = new ContextMenuProvider(dataGrid);
			dataGrid.ContextMenuOpening += provider.dataGrid_ContextMenuOpening;
			dataGrid.ContextMenu = new ContextMenu();
		}

		readonly Control control;
		readonly TreeView treeView;
		readonly DecompilerTextView textView;
		readonly ListBox listBox;
		readonly DataGrid dataGrid;
		readonly IExport<IContextMenuEntry, IContextMenuEntryMetadata>[] entries;

		private ContextMenuProvider(Control control)
		{
			entries = control.GetExportProvider().GetExports<IContextMenuEntry, IContextMenuEntryMetadata>().ToArray();

			this.control = control;
		}

		ContextMenuProvider(DecompilerTextView textView)
			: this((Control)textView)
		{
			this.textView = textView ?? throw new ArgumentNullException(nameof(textView));
		}

		ContextMenuProvider(TreeView treeView)
			: this((Control)treeView)
		{
			this.treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
		}

		ContextMenuProvider(ListBox listBox)
			: this((Control)listBox)
		{
    		this.listBox = listBox ?? throw new ArgumentNullException(nameof(listBox));
		}

		ContextMenuProvider(DataGrid dataGrid)
			: this((Control)dataGrid)
		{
			this.dataGrid = dataGrid ?? throw new ArgumentNullException(nameof(dataGrid));
		}

		void treeView_ContextMenuOpening(object sender, ContextRequestedEventArgs e)
		{
			var context = TextViewContext.Create(e, treeView: treeView);
			if (context.SelectedTreeNodes.Length == 0)
			{
				e.Handled = true; // don't show the menu
				return;
			}

			if (ShowContextMenu(context, out var menu))
				treeView.ContextMenu = menu;
			else
				// hide the context menu.
				e.Handled = true;
		}

		void textView_ContextMenuOpening(object sender, ContextRequestedEventArgs e)
		{
			var context = TextViewContext.Create(e, textView: textView);
			if (ShowContextMenu(context, out var menu))
				textView.ContextMenu = menu;
			else
				// hide the context menu.
				e.Handled = true;
		}

		void listBox_ContextMenuOpening(object sender, ContextRequestedEventArgs e)
		{
			var context = TextViewContext.Create(e, listBox: listBox);
			if (ShowContextMenu(context, out var menu))
				listBox.ContextMenu = menu;
			else
				// hide the context menu.
				e.Handled = true;
		}

		void dataGrid_ContextMenuOpening(object sender, DataGridContextMenuEventArgs e)
		{
			var context = TextViewContext.Create(e, dataGrid: dataGrid);
			if (ShowContextMenu(context, out var menu))
				dataGrid.ContextMenu = menu;
			else
				// hide the context menu.
				e.Handled = true;
		}

		bool ShowContextMenu(TextViewContext context, out ContextMenu menu)
		{
			menu = new ContextMenu();

			var menuGroups = new Dictionary<string, IExport<IContextMenuEntry, IContextMenuEntryMetadata>[]>();
			IExport<IContextMenuEntry, IContextMenuEntryMetadata>[] topLevelGroup = null;
			foreach (var group in entries.OrderBy(c => c.Metadata.Order).GroupBy(c => c.Metadata.ParentMenuID))
			{
				if (group.Key == null)
				{
					topLevelGroup = group.ToArray();
				}
				else
				{
					menuGroups.Add(group.Key, group.ToArray());
				}
			}
			BuildMenu(topLevelGroup ?? Array.Empty<IExport<IContextMenuEntry, IContextMenuEntryMetadata>>(), menu.Items);
			menu.Closed += ContextMenu_Closing;

			void ContextMenu_Closing(object sender, EventArgs e)
			{
				((ContextMenu)sender).Closed -= ContextMenu_Closing;
				ContextMenuClosedEventSource.Raise(this, EventArgs.Empty);
			}

			return menu.Items.Count > 0;

			void BuildMenu(IExport<IContextMenuEntry, IContextMenuEntryMetadata>[] menuGroup, ItemCollection parent)
			{
				foreach (var category in menuGroup.GroupBy(c => c.Metadata.Category))
				{
					var needSeparatorForCategory = parent.Count > 0;
					foreach (var entryPair in category)
					{
						var entry = entryPair.Value;
						if (entry.IsVisible(context))
						{
							if (needSeparatorForCategory)
							{
								parent.Add(new Separator());
								needSeparatorForCategory = false;
							}
							var menuItem = new MenuItem();
							menuItem.Header = ResourceHelper.GetString(entryPair.Metadata.Header);
							ApplyGesture(menuItem, entryPair.Metadata.InputGestureText);
							if (!string.IsNullOrEmpty(entryPair.Metadata.Icon))
							{
								menuItem.Icon = new Image {
									Width = 16,
									Height = 16,
									Source = Images.LoadImage(Images.Load(entryPair.Value, entryPair.Metadata.Icon))
								};
							}
							if (entryPair.Value.IsEnabled(context))
							{
								menuItem.Click += delegate { entry.Execute(context); };
							}
							else
								menuItem.IsEnabled = false;
							parent.Add(menuItem);

							if (entryPair.Metadata.MenuID != null && menuGroups.TryGetValue(entryPair.Metadata.MenuID, out var group))
							{
								BuildMenu(group, menuItem.Items);
							}
						}
					}
				}
			}

			void ApplyGesture(MenuItem menuItem, string? gestureText)
			{
				if (string.IsNullOrWhiteSpace(gestureText) || gestureText == "MMB") // IMPORTANT: "MMB" is a special case we use to indicate middle-mouse-button actions. We don't want to show that in the menu.
					return;

				try
				{
					var parsed = KeyGesture.Parse(gestureText);
					menuItem.HotKey = parsed;
					menuItem.InputGesture = parsed;
				}
				catch
				{
					if (menuItem.Header is string headerText)
					{
						menuItem.Header = $"{headerText}\t{gestureText}";
					}
				}
			}
		}
	}
}
