using System;
using System.IO;
using ICSharpCode.ILSpyX.Settings;

namespace ICSharpCode.ILSpy
{
    public class ILSpySettingsFilePathProvider : ISettingsFilePathProvider
    {
        public string GetSettingsFilePath()
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var roverDir = Path.Combine(appData, "ProjectRover");
            Directory.CreateDirectory(roverDir);
            return Path.Combine(roverDir, "ILSpy.xml");
        }
    }
}
