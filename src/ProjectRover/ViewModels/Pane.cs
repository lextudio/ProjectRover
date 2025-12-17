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

using Avalonia;

namespace ICSharpCode.ILSpy.ViewModels
{
	public class Pane
	{
		// Helper properties to enable binding state properties from the model to the view.

		public static readonly AttachedProperty<bool> IsActiveProperty =
			AvaloniaProperty.RegisterAttached<Pane, AvaloniaObject, bool>(
				"IsActive",
				defaultValue: false);

		public static void SetIsActive(AvaloniaObject element, bool value)
		{
			element.SetValue(IsActiveProperty, value);
		}

		public static bool GetIsActive(AvaloniaObject element)
		{
			return element.GetValue(IsActiveProperty);
		}

		public static readonly AttachedProperty<bool> IsVisibleProperty =
			AvaloniaProperty.RegisterAttached<Pane, AvaloniaObject, bool>(
				"IsVisible",
				defaultValue: false);

		public static void SetIsVisible(AvaloniaObject element, bool value)
		{
			element.SetValue(IsVisibleProperty, value);
		}

		public static bool GetIsVisible(AvaloniaObject element)
		{
			return element.GetValue(IsVisibleProperty);
		}
	}
}