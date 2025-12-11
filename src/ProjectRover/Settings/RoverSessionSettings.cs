using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ICSharpCode.ILSpyX.Settings;

namespace ProjectRover.Settings;

public sealed class RoverSessionSettings : ISettingsSection, INotifyPropertyChanged
{
    private string? selectedSearchMode;
    private bool isSearchDockVisible;

    public XName SectionName => "RoverSessionSettings";

    public string? SelectedSearchMode
    {
        get => selectedSearchMode;
        set => SetProperty(ref selectedSearchMode, value);
    }

    public bool IsSearchDockVisible
    {
        get => isSearchDockVisible;
        set => SetProperty(ref isSearchDockVisible, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadFromXml(XElement section)
    {
        SelectedSearchMode = (string?)section.Attribute(nameof(SelectedSearchMode));
        IsSearchDockVisible = (bool?)section.Attribute(nameof(IsSearchDockVisible)) ?? false;
    }

    public XElement SaveToXml()
    {
        var section = new XElement(SectionName);
        if (!string.IsNullOrEmpty(SelectedSearchMode))
        {
            section.SetAttributeValue(nameof(SelectedSearchMode), SelectedSearchMode);
        }
        section.SetAttributeValue(nameof(IsSearchDockVisible), IsSearchDockVisible);
        return section;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new(propertyName));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
