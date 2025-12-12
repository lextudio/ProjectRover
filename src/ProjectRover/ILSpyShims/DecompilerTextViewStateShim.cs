namespace ICSharpCode.ILSpy.TextView
{
    // Minimal shim of DecompilerTextViewState used by DecompilationOptions.
    public class DecompilerTextViewState : ViewState
    {
        // keep minimal shape; actual state is used only at runtime by ILSpy, ProjectRover doesn't rely on internals
    }
}
