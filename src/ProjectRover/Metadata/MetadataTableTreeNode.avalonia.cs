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

using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Avalonia;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpy.ViewModels;

namespace ICSharpCode.ILSpy.Metadata
{
	partial class MetadataTableTreeNode
	{
		protected void ScrollRowIntoView(DataGrid view, int row)
		{
			if (!view.IsLoaded)
			{
				view.Loaded += View_Loaded;
			}
			else
			{
				View_Loaded(view, new Avalonia.Interactivity.RoutedEventArgs());
			}
			if (row >= 0)
			{
				var items = view.ItemsSource as System.Collections.IList ?? view.Items;
				if (items != null && items.Count > row)
				{
					Dispatcher.UIThread.Post(() =>
					{
						var item = items[row];
						view.SelectItem(item);
						view.ScrollIntoView(item);
					}, DispatcherPriority.Background);
				}
			}
		}

		private void View_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			DataGrid view = (DataGrid)sender;
			view.ScrollIntoView(scrollTarget - 1);
			view.Loaded -= View_Loaded;
			this.scrollTarget = default;
		}
	}
}