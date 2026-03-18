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

using System.Collections.Generic;
using Avalonia.Controls;
using MouseHoverLogic = AvaloniaEdit.Rendering.PointerHoverLogic;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpy.ViewModels;
using Avalonia.Input;
using System.Linq;

namespace ICSharpCode.ILSpy.Metadata
{
	class MetaDataGrid : DataGrid, IHaveState
	{
		private readonly MouseHoverLogic hoverLogic;
		private ToolTip toolTip;

		public ILSpyTreeNode SelectedTreeNode { get; set; }

		public MetaDataGrid()
		{
			this.hoverLogic = new MouseHoverLogic(this);
			this.hoverLogic.PointerHover += HoverLogic_MouseHover;
			this.hoverLogic.PointerHoverStopped += HoverLogic_MouseHoverStopped;
		}

		private void HoverLogic_MouseHoverStopped(object sender, PointerEventArgs e)
		{
			// Non-popup tooltips get closed as soon as the mouse starts moving again
			if (toolTip != null)
			{
				//toolTip.IsOpen = false;
				e.Handled = true;
			}
		}
		private void HoverLogic_MouseHover(object sender, PointerEventArgs e)
		{
			var position = e.GetPosition(this);
			var hit =this.InputHitTest(position);
			if (hit == null)
			{
				return;
			}

			var cell = this.GetInputElementsAt(position).OfType<DataGridCell>().FirstOrDefault();
			if (cell == null)
				return;
			var data = cell.DataContext;
			//var name = (string)cell.Column.Header;
			if (toolTip == null)
			{
				toolTip = new ToolTip();
				//toolTip.Closed += ToolTipClosed;
			}
			//toolTip.PlacementTarget = this; // required for property inheritance

			// var pi = data?.GetType().GetProperty(name + "Tooltip");
			// if (pi == null)
			// 	return;
			// object tooltip = pi.GetValue(data);
			// if (tooltip is string s)
			// {
			// 	if (string.IsNullOrWhiteSpace(s))
			// 		return;
			// 	toolTip.Content = new TextBlock {
			// 		Text = s,
			// 		TextWrapping = TextWrapping.Wrap
			// 	};
			// }
			// else if (tooltip != null)
			// {
			// 	toolTip.Content = tooltip;
			// }
			// else
			// {
			// 	return;
			// }

			//e.Handled = true;
			// TODO: toolTip.IsOpen = true;
		}

		private void ToolTipClosed(object sender, RoutedEventArgs e)
		{
			if (toolTip == sender)
			{
				toolTip = null;
			}
		}

		public ViewState GetState()
		{
			return new ViewState {
				DecompiledNodes = SelectedTreeNode == null
					? null
					: new HashSet<ILSpyTreeNode>(new[] { SelectedTreeNode })
			};
		}
	}
}
