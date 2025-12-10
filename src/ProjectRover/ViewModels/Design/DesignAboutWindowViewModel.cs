/*
    Copyright 2024 CodeMerx
    Copyright 2025 LeXtudio Inc.
    This file is part of ProjectRover.

    ProjectRover is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ProjectRover is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with ProjectRover.  If not, see<https://www.gnu.org/licenses/>.
*/

using System;
using System.Threading.Tasks;
using ProjectRover.Providers;

namespace ProjectRover.ViewModels.Design;

public class DesignAboutWindowViewModel : AboutWindowViewModel
{
    public DesignAboutWindowViewModel()
        : base(new DesignAppInformationProvider())
    {
    }
}

file class DesignAppInformationProvider : IAppInformationProvider
{
    public string Name { get; } = "Project Rover";
    public string Version { get; } = "1.69.0";
    public string Copyright { get; } = $"Copyright {DateTime.Now.Year} LeXtudio Inc.";

    public AdditionalInfo AdditionalInfo { get; } = new()
    {
        Title = "Credits",
        Text = "Originally created by CodeMerx and now maintained by LeXtudio Inc."
    };

    public Task TryLoadRemoteAdditionalInfoAsync()
    {
        return Task.CompletedTask;
    }
}
