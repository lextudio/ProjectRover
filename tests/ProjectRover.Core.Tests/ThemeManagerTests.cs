using System;
using ICSharpCode.ILSpy.Themes;
using Xunit;

namespace ProjectRover.Core.Tests;

public class ThemeManagerTests : IDisposable
{
    private readonly string previousTheme;

    public ThemeManagerTests()
    {
        previousTheme = ThemeManager.Current.Theme;
    }

    public void Dispose()
    {
        ThemeManager.Current.ApplyTheme(previousTheme);
    }

    [Fact]
    public void ApplyTheme_TogglesDarkFlagAndThemeName()
    {
        ThemeManager.Current.ApplyTheme("Dark");

        Assert.Equal("Dark", ThemeManager.Current.Theme);
        Assert.True(ThemeManager.Current.IsDarkTheme);

        ThemeManager.Current.ApplyTheme("Light");

        Assert.Equal("Light", ThemeManager.Current.Theme);
        Assert.False(ThemeManager.Current.IsDarkTheme);
    }

    [Fact]
    public void ApplyTheme_NormalizesUnknownNameToLight()
    {
        ThemeManager.Current.ApplyTheme("Sunrise");

        Assert.Equal("Light", ThemeManager.Current.Theme);
        Assert.False(ThemeManager.Current.IsDarkTheme);
    }
}
