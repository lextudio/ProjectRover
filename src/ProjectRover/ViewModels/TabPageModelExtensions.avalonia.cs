// Copyright (c) 2019 AlphaSierraPapa for the SharpDevelop Team
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
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using Avalonia;

using Avalonia.Input;

using Avalonia.VisualTree;

using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.TextViewControl;

using TomsToolbox.Wpf;

#nullable enable


namespace ICSharpCode.ILSpy.ViewModels
{
	public static partial class TabPageModelExtensions
	{
		public static void Focus(this TabPageModel tabPage)
		{
			if (tabPage.Content is not FrameworkElement content)
				return;

			var focusable = content
				.GetSelfAndVisualDescendants()          // Avalonia.VisualTree.VisualExtensions
				.OfType<IInputElement>()                // or OfType<Control>()
				.FirstOrDefault(item =>
					item.Focusable
					&& item is Visual v
					&& v.IsEffectivelyVisible);

			focusable?.Focus();
		}
    }
}
