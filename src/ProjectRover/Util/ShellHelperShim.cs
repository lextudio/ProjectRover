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
