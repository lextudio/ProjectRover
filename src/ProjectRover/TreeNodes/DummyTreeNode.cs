using System;
using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.TreeNodes;

namespace ICSharpCode.ILSpy.TreeNodes
{
    internal class DummyTreeNode : ILSpyTreeNode
    {
        public override object Text => "Loading...";

        public override object Icon => null;

        public override FilterResult Filter(LanguageSettings settings)
        {
            return FilterResult.Match;
        }

        public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
        {
        }
    }
}
