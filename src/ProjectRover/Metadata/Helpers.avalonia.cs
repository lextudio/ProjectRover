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
using System.ComponentModel;
using System.Reflection;

using Avalonia.Controls;

namespace ICSharpCode.ILSpy.Metadata
{
	static partial class Helpers
	{
		private static DataTemplate GetOrCreateLinkCellTemplate(string name, PropertyDescriptor descriptor, Binding binding)
		{
			if (linkCellTemplates.TryGetValue(name, out var template))
			{
				return template;
			}

			var tb = new HyperlinkButton();
			tb.Click += Hyperlink_Click;

			DataTemplate dataTemplate = new DataTemplate() { VisualTree = tb };
			linkCellTemplates.Add(name, dataTemplate);
			return dataTemplate;

			void Hyperlink_Click(object sender, RoutedEventArgs e)
			{
				var hyperlink = (HyperlinkButton)sender;
				var onClickMethod = descriptor.ComponentType.GetMethod("On" + name + "Click", BindingFlags.Instance | BindingFlags.Public);
				if (onClickMethod != null)
				{
					onClickMethod.Invoke(hyperlink.DataContext, Array.Empty<object>());
				}
			}
		}
	}

	static class DataTemplateExtensions
	{
		extension(DataTemplate dataTemplate)
		{
			public object VisualTree {
				get => dataTemplate.Content;
				set => dataTemplate.Content = value;
			}
		}
	}
}
