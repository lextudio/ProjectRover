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

using Mono.Cecil.Cil;

namespace ILSpyRover.SearchResults;

public class VariableNameSearchResult : SearchResult
{
    public required VariableDefinition VariableDefinition { get; init; }
}
