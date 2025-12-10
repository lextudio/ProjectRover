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

using System.ComponentModel;

namespace ProjectRover.Nodes;

public abstract class Node : INotifyPropertyChanged
{
    public required string Name { get; init; }
    public required Node? Parent { get; init; }

    private bool isPublicAPI = true;
    public bool IsPublicAPI
    {
        get => isPublicAPI;
        set
        {
            if (isPublicAPI == value)
                return;
            isPublicAPI = value;
            OnPropertyChanged(nameof(IsPublicAPI));
        }
    }

    private bool isExpanded;
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
                return;
            isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
