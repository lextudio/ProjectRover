using System;
using System.IO;
using ICSharpCode.ILSpyX.Settings;

namespace ICSharpCode.ILSpy
{
    public class ILSpySettingsFilePathProvider : ISettingsFilePathProvider
    {
        public string GetSettingsFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // If running on Windows prefer ILSpy WPF's settings location when it exists
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var icsharpDir = Path.Combine(appData, "ICSharpCode");
                    var ilspyPath = Path.Combine(icsharpDir, "ILSpy.xml");
                    if (File.Exists(ilspyPath))
                        return ilspyPath;
                }
            }
            catch
            {
                // ignore platform detection errors and fall back to ProjectRover path
            }

            var roverDir = Path.Combine(appData, "ProjectRover");
            Directory.CreateDirectory(roverDir);
            return Path.Combine(roverDir, "ILSpy.xml");
        }
    }
}
