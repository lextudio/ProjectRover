using ICSharpCode.Decompiler.IL;

namespace ICSharpCode.ILSpy
{
    public static class DebugSteps
    {
        public static ILAstWritingOptions Options { get; } = new ILAstWritingOptions();
    }

    public class DebugStepsPaneModel { public const string PaneContentId = "debugStepsPane"; }
}
