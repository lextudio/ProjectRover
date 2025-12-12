using System;
using System.Composition;

namespace ICSharpCode.ILSpy.Util
{
    [Export]
    [Shared]
    public class SettingsService
    {
        public ICSharpCode.ILSpyX.Settings.DecompilerSettings DecompilerSettings { get; }
        public ICSharpCode.ILSpy.Options.DisplaySettings DisplaySettings { get; }

        [ImportingConstructor]
        public SettingsService()
        {
            DecompilerSettings = new ICSharpCode.ILSpyX.Settings.DecompilerSettings();
            DisplaySettings = new ICSharpCode.ILSpy.Options.DisplaySettings();
        }
    }
}
