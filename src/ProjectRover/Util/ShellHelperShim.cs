using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ICSharpCode.ILSpy.Util
{
    public static class ShellHelper
    {
        public static void OpenFolderAndSelectItem(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return;
                if (File.Exists(path) || Directory.Exists(path))
                {
                    // macOS: use 'open' to reveal in Finder, Windows would use explorer.exe /select
                    Process.Start(new ProcessStartInfo("open", $"-R \"{path}\"") { CreateNoWindow = true, UseShellExecute = false });
                }
            }
            catch
            {
                // ignore failures in the shim
            }
        }

        public static void OpenFolderAndSelectItems(IEnumerable<string> paths)
        {
            foreach (var p in paths)
            {
                OpenFolderAndSelectItem(p);
            }
        }

		internal static void OpenFolder(string targetFolder)
		{
			throw new NotImplementedException();
		}
	}
}
