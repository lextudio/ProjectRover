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

using Microsoft.Extensions.Configuration;

namespace ProjectRover.Extensions;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddEmbeddedResource(this IConfigurationBuilder builder, string resource)
    {
        var assembly = AssemblyProvider.Assembly;
        var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{resource}");
        if (stream != null)
            // AddJsonStream will dispose the stream. Unfortunately, this is not documented.
            // Source code: https://github.com/dotnet/runtime/blob/210a7a9feec64b7fac5147656cddb199ec90cf75/src/libraries/Microsoft.Extensions.Configuration.Json/src/JsonConfigurationFileParser.cs#L30
            builder.AddJsonStream(stream);

        return builder;
    }
}
