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
using System.ComponentModel;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;

namespace ICSharpCode.ILSpy.Metadata
{
	static partial class Helpers
	{
		private static readonly Dictionary<string, IDataTemplate> _linkCellTemplatesAvalonia = new Dictionary<string, IDataTemplate>();

		private static IDataTemplate GetOrCreateLinkCellTemplate(string name, PropertyDescriptor descriptor, Binding binding)
		{
			if (_linkCellTemplatesAvalonia.TryGetValue(name, out var template))
			{
				return template;
			}

			var dataTemplate = new FuncDataTemplate<object>((data, _) =>
			{
				var hyper = new HyperlinkButton();
				hyper.Classes.Add("LinkButton");
				hyper.Bind(ContentControl.ContentProperty, binding);
				hyper.Click += (sender, e) =>
				{
					var hyperlink = (HyperlinkButton)sender;
					var onClickMethod = descriptor.ComponentType.GetMethod("On" + name + "Click", BindingFlags.Instance | BindingFlags.Public);
					if (onClickMethod != null)
					{
						onClickMethod.Invoke(hyperlink.DataContext, Array.Empty<object>());
					}
				};
				return hyper;
			}, true);

			_linkCellTemplatesAvalonia.Add(name, dataTemplate);
			return dataTemplate;
		}
	}
}
