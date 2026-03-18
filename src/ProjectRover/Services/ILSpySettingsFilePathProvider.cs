// Copyright (c) 2025-2026 LeXtudio Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

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
