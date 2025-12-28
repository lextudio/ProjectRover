using Serilog;

namespace ICSharpCode.ILSpy.Util
{
    public static class LogCategory
    {
        public static ILogger For(string category)
        {
            return RoverLog.Log.ForContext("Category", category);
        }
    }
}
