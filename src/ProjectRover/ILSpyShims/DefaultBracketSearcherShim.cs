
using AvaloniaEdit.Document;

namespace ICSharpCode.ILSpy.TextView
{
    // Lightweight shim for IBracketSearcher used by ProjectRover when language-specific
    // bracket searchers are not available. Returns null (no match) to avoid bringing
    // heavy language parsing into the Avalonia host.
    public class DefaultBracketSearcher : IBracketSearcher
    {
        public static readonly DefaultBracketSearcher Instance = new DefaultBracketSearcher();

        public BracketSearchResult SearchBracket(IDocument document, int offset)
        {
            // No-op: indicate nothing to highlight.
            return null;
        }
    }

    public interface IBracketSearcher
    {
        BracketSearchResult SearchBracket(IDocument document, int offset);
    }
}
