// Minimal shim for SharpTreeView used by ILSpy sources when building ILSpy.Shims.
using System;


using Avalonia.Controls;

using ICSharpCode.ILSpyX.TreeView;

namespace ICSharpCode.ILSpy.Controls.TreeView
{
    // This provides the missing SharpTreeView type used by linked ILSpy sources.
    // It's intentionally minimal: only what the linked sources require.
    public class SharpTreeView : ItemsControl
    {
        public SharpTreeNode[] GetTopLevelSelection()
        {
            return null!;
        }

		internal IDisposable LockUpdates()
		{
			throw new NotImplementedException();
		}
	}
}
