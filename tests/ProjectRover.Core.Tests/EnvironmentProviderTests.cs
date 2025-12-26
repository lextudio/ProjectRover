using ProjectRover;
using Xunit;

namespace ProjectRover.Core.Tests;

public class EnvironmentProviderTests
{
    [Fact]
    public void EnvironmentProvider_UsesDevelopmentWhenCompiledWithDebug()
    {
        Assert.Equal(Environment.Development, EnvironmentProvider.Environment);
    }
}
