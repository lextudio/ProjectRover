namespace ICSharpCode.ILSpy.Metadata
{
    public interface IContentFilter
    {
        bool IsMatch(object? value);
    }
}
