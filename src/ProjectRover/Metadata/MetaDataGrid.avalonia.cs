using Avalonia.Controls;

using ICSharpCode.ILSpy.TextViewControl;
using ICSharpCode.ILSpy.ViewModels;

namespace ICSharpCode.ILSpy.Metadata
{
	class MetaDataGrid : ListBox, IHaveState
	{
		public ViewState? GetState()
		{
			throw new System.NotImplementedException();
		}
	}
}
