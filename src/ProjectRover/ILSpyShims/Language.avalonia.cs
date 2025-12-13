using AvaloniaEdit.Highlighting;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy
{
    public abstract partial class Language
    {
        /// <summary>
        /// Gets the syntax highlighting used for this language.
        /// </summary>
        public virtual IHighlightingDefinition SyntaxHighlighting {
            get {
                return HighlightingManager.Instance.GetDefinitionByExtension(FileExtension);
            }
        }

        public virtual TextView.IBracketSearcher BracketSearcher {
            get {
                return new TextView.DefaultBracketSearcher(); // TODO: .Instance;
            }
        }

        /// <summary>
		/// Converts a member signature to a string.
		/// This is used for displaying the tooltip on a member reference.
		/// </summary>
		public virtual RichText GetRichTextTooltip(IEntity entity)
		{
			return GetTooltip(entity);
		}
    }
}
