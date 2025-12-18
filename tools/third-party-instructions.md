# Instructions for Updating Third-Party Licenses

## Requirements of `check_third_party.py`

Note that you must verify THIRD-PARTY-NOTICES.md against these rules to find violations:

### Sections

A section should have a clear title, and the license header, and then the actual license text. Keep the right indentation.

``` md
## Avalonia.Desktop

    The MIT License (MIT)

    Copyright (c) .NET Foundation and Contributors All Rights Reserved

    Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

- Duplicated sections are errors.
- Indentation problems are errors.
- Missing header or license text are errors.
- Copyright year/holder missing or show typical placeholders like `<YEAR>` or `<COPYRIGHT HOLDER>` are errors.

### Other Rules

- Sections must be ordered alphabetically by their titles. Mis-ordered sections are errors.
- Typical families of packages must be grouped together if their names share the same prefix and their license texts are identical. Examples are `Microsoft.Extensions.*`, `Serilog.*`, `Avalonia.*`, `ICSharpCode.*`, `Dock.*`, `TomsToolbox.*`. Not grouping them is a warning. A family of packages usually come from repos under the same GitHub organization/person.
- If a package is indirect dependency and showed up in the notices, it is an error. Only direct dependencies should be included.

## Steps to Update THIRD-PARTY-NOTICES.md for `update_third_party.py`

### Acquiring Licenses for NuGet Packages

1. You should learn which is the .csproj file that contains the dependencies, usually it is `src/ProjectRover/ProjectRover.csproj` for this repo.
2. Build this project to restore all NuGet packages.
3. Analyze the project file to find all direct dependencies, and record their names and versions. Note that if global package management is used (e.g. `Directory.Packages.props`), you need to read that file too.
4. Reach NuGet local cache to see if you can find typical license files there for each packages. The default location is `%USERPROFILE%\.nuget\packages` on Windows, and `~/.nuget/packages` on macOS/Linux. Copy the license files if found to a working folder.
5. If not found, try to learn the GitHub repo of the package, and see if you can find license files there. Use typical paths like `LICENSE`, `LICENSE.txt`, `LICENSE.md`, etc.
6. If still not found, try to reach NuGet.org API to see if license text.

### Acquiring Licenses for Non-NuGet Dependencies

Some dependencies are not from NuGet, for example, ILSpy source code is included as a git submodule. You need to manually find their license texts from their repos.

### Merging and Preparing the THIRD-PARTY-NOTICES.md File

With all package names and their license texts collected, next build up the section list. A family of packages may share the same license text, so you can group them together to a single section. Examples are,

- `Microsoft.Extensions.*` packages can be grouped to a single section if their license texts are identical.
- `Serilog.*` packages can be grouped to a single section if their license texts are identical.
- `Avalonia.*` packages can be grouped to a single section if their license texts are identical.
- `ICSharpCode.*` packages can be grouped to a single section if their license texts are identical.
- `Dock.*` packages can be grouped to a single section if their license texts are identical.
- `TomsToolbox.*` packages can be grouped to a single section if their license texts are identical.

You should prefer to group, and only not group when the license texts are different.

Finally, prepare the THIRD-PARTY-NOTICES.md file. You can keep existing sections if they are still valid. For new sections, append them to the right location of the file. The sections are ordered alphabetically by their titles.

## Tips for Automation in Both Scripts

The checkout script `check_third_party.py` is provided to help you validate the above.

You must Steps must be automated using the `tools/update_third_party.py` script and validate the results in each round. You can run it using:

``` bash
python3 tools/update_third_party.py
python3 tools/check_third_party.py
```

You can keep improving the update script until more edge cases are covered. Iterate and ask for feedbacks when facing difficulties.
