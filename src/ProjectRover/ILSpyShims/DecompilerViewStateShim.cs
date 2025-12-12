using System;
using System.Collections.Generic;

namespace ICSharpCode.ILSpy.TextView
{
    // Small shim to bridge ILSpy TextView.ViewState types used by TabPageModel.
    // The real implementation lives in ILSpy; this minimal type provides compatibility
    // so ProjectRover compiles while we rely on the ILSpy Decompiler/TextView runtime.
    public class ViewState
    {
        public HashSet<object>? DecompiledNodes { get; set; }
        public Uri? ViewedUri { get; set; }
    }
}
