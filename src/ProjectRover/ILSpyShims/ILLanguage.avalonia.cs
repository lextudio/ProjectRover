using System.Linq;
using System.Reflection.Metadata;
using AvaloniaEdit.Highlighting;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Docking;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.ViewModels;

namespace ICSharpCode.ILSpy
{
    public partial class ILLanguage
    {
        
		public override RichText GetRichTextTooltip(IEntity entity)
		{
            return null;
            // TODO: porting 
            /*
			var output = new AvaloniaEditTextOutput() { IgnoreNewLineAndIndent = true };

			var disasm = CreateDisassembler(output, ((TabPageModel)dockWorkspace.ActiveTabPage).CreateDecompilationOptions());
			MetadataFile module = entity.ParentModule?.MetadataFile;
			if (module == null)
			{
				return null;
			}

			switch (entity.SymbolKind)
			{
				case SymbolKind.TypeDefinition:
					disasm.DisassembleTypeHeader(module, (TypeDefinitionHandle)entity.MetadataToken);
					break;
				case SymbolKind.Field:
					disasm.DisassembleFieldHeader(module, (FieldDefinitionHandle)entity.MetadataToken);
					break;
				case SymbolKind.Property:
				case SymbolKind.Indexer:
					disasm.DisassemblePropertyHeader(module, (PropertyDefinitionHandle)entity.MetadataToken);
					break;
				case SymbolKind.Event:
					disasm.DisassembleEventHeader(module, (EventDefinitionHandle)entity.MetadataToken);
					break;
				case SymbolKind.Method:
				case SymbolKind.Operator:
				case SymbolKind.Constructor:
				case SymbolKind.Destructor:
				case SymbolKind.Accessor:
					disasm.DisassembleMethodHeader(module, (MethodDefinitionHandle)entity.MetadataToken);
					break;
				default:
					output.Write(GetDisplayName(entity, true, true, true));
					break;
			}

			return new DocumentHighlighter(output.GetDocument(), base.SyntaxHighlighting).HighlightLine(1).ToRichText();
            */
		}
    }
}
