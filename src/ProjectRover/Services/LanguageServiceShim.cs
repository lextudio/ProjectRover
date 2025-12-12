using System;
using System.Composition;

namespace ICSharpCode.ILSpy.Languages
{
    // Minimal shim of ILSpy's LanguageService expected by other parts.
    [Export]
    [Shared]
    public class LanguageService
    {
        public ICSharpCode.ILSpyX.LanguageVersion LanguageVersion { get; private set; }

        [ImportingConstructor]
        public LanguageService()
        {
            // default to latest
            LanguageVersion = new ICSharpCode.ILSpyX.LanguageVersion("Latest");
        }

        // Optional: allow setting via DI in Rover if needed later
        public void SetLanguageVersion(ICSharpCode.ILSpyX.LanguageVersion v)
        {
            LanguageVersion = v ?? new ICSharpCode.ILSpyX.LanguageVersion("Latest");
        }
    }
}
