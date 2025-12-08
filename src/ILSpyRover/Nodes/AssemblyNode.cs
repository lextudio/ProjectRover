/*
    Copyright 2024 CodeMerx
    Copyright 2025 LeXtudio Inc.
    This file is part of ILSpyRover.

    ILSpyRover is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ILSpyRover is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with ILSpyRover.  If not, see<https://www.gnu.org/licenses/>.
*/

using System.Collections.ObjectModel;
using Mono.Cecil;

namespace ILSpyRover.Nodes;

public class AssemblyNode : Node
{
    public AssemblyNode()
    {
        References = new ReferencesNode
        {
            Name = "References",
            Parent = this
        };
        Children.Add(References);
    }
    
    public required AssemblyDefinition AssemblyDefinition { get; init; }
    public ReferencesNode References { get; }
    public ObservableCollection<Node> Children { get; } = new();
    public bool DependenciesLoaded { get; set; }

    public void AddNamespace(NamespaceNode @namespace) => Children.Add(@namespace);
}
