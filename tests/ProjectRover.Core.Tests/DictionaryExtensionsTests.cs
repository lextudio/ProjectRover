using System.Collections.Generic;
using ProjectRover.Extensions;
using Xunit;

namespace ProjectRover.Core.Tests;

public class DictionaryExtensionsTests
{
    [Fact]
    public void AddRange_OverwritesExistingKeysAndAddsNewOnes()
    {
        var target = new Dictionary<string, int>
        {
            ["one"] = 1,
            ["two"] = 2
        };

        var source = new Dictionary<string, int>
        {
            ["two"] = 20,
            ["three"] = 3
        };

        target.AddRange(source);

        Assert.Equal(3, target.Count);
        Assert.Equal(1, target["one"]);
        Assert.Equal(20, target["two"]);
        Assert.Equal(3, target["three"]);
    }
}
