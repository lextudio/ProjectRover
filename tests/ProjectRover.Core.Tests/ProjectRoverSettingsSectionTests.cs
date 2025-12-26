using System.Xml.Linq;
using ProjectRover.Settings;
using Xunit;

namespace ProjectRover.Core.Tests;

public class ProjectRoverSettingsSectionTests
{
    [Fact]
    public void DefaultsAreCorrect()
    {
        var section = new ProjectRoverSettingsSection();

        Assert.False(section.ShowAvaloniaMainMenuOnMac);
        Assert.True(section.UseDefaultDockLayoutOnly);
        Assert.Null(section.DockLayout);
    }

    [Fact]
    public void LoadFromXml_PopulatesProperties()
    {
        var section = new ProjectRoverSettingsSection();
        var xml = new XElement("ProjectRover",
            new XAttribute(nameof(section.ShowAvaloniaMainMenuOnMac), true),
            new XAttribute(nameof(section.UseDefaultDockLayoutOnly), false),
            new XElement(nameof(section.DockLayout), new XElement("Root")));

        section.LoadFromXml(xml);

        Assert.True(section.ShowAvaloniaMainMenuOnMac);
        Assert.False(section.UseDefaultDockLayoutOnly);
        Assert.Equal("<Root />", section.DockLayout);
    }

    [Fact]
    public void SaveToXml_IncludesDockLayoutElementWhenXmlIsValid()
    {
        var section = new ProjectRoverSettingsSection
        {
            DockLayout = "<Root><Child /></Root>"
        };

        var xml = section.SaveToXml();
        var dockLayoutElement = xml.Element(nameof(section.DockLayout));

        Assert.NotNull(dockLayoutElement);
        Assert.NotNull(dockLayoutElement.Element("Root"));
    }

    [Fact]
    public void SaveToXml_WritesRawDockLayoutWhenXmlIsInvalid()
    {
        var section = new ProjectRoverSettingsSection
        {
            DockLayout = "not xml"
        };

        var xml = section.SaveToXml();
        var dockLayoutElement = xml.Element(nameof(section.DockLayout));

        Assert.NotNull(dockLayoutElement);
        Assert.Equal("not xml", dockLayoutElement.Value);
    }
}
