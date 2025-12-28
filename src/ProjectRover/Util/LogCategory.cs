using Serilog;

namespace ICSharpCode.ILSpy.Util
{
    public static class LogCategory
    {
        public static ILogger For(string category)
        {
            // Set both a friendly Category property and SourceContext so Serilog
            // minimum-level overrides can match on the SourceContext name.
            return RoverLog.Log
                .ForContext("Category", category)
                .ForContext("SourceContext", category);
        }
    }
}
