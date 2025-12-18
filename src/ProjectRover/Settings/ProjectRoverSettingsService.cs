using System;
using System.IO;
using System.Text.Json;
using ICSharpCode.ILSpy.Util;

namespace ProjectRover.Settings
{
    public class ProjectRoverSettings
    {
        public bool ShowAvaloniaMainMenuOnMac { get; set; } = false;
    }

    public sealed class ProjectRoverSettingsService
    {
        private static readonly string SettingsFileName = "projectrover_settings.json";
        private readonly string _path;

        public ProjectRoverSettingsService()
        {
            var dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(dir, "ProjectRover");
            Directory.CreateDirectory(appDir);
            _path = Path.Combine(appDir, SettingsFileName);
        }

        public ProjectRoverSettings Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return new ProjectRoverSettings();
                var json = File.ReadAllText(_path);
                var s = JsonSerializer.Deserialize<ProjectRoverSettings>(json) ?? new ProjectRoverSettings();
                return s;
            }
            catch
            {
                var fallback = new ProjectRoverSettings();
                return fallback;
            }
        }

        public void Save(ProjectRoverSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
            }
            catch { }
        }
    }
}
