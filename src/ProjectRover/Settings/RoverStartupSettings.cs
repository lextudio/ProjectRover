using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ICSharpCode.ILSpyX.Settings;

namespace ProjectRover.Settings;

public sealed class RoverStartupSettings : ISettingsSection, INotifyPropertyChanged
{
    private bool restoreAssemblies = true;
    private bool useDebugSymbols = true;
    private bool applyWinRtProjections;
    private bool showCompilerGeneratedMembers;
    private bool showInternalApi;
    private bool autoLoadReferencedAssemblies;

    public XName SectionName => "RoverStartupSettings";

    public bool RestoreAssemblies
    {
        get => restoreAssemblies;
        set => SetProperty(ref restoreAssemblies, value);
    }

    public bool UseDebugSymbols
    {
        get => useDebugSymbols;
        set => SetProperty(ref useDebugSymbols, value);
    }

    public bool ApplyWinRtProjections
    {
        get => applyWinRtProjections;
        set => SetProperty(ref applyWinRtProjections, value);
    }

    public bool ShowCompilerGeneratedMembers
    {
        get => showCompilerGeneratedMembers;
        set => SetProperty(ref showCompilerGeneratedMembers, value);
    }

    public bool ShowInternalApi
    {
        get => showInternalApi;
        set => SetProperty(ref showInternalApi, value);
    }

    public bool AutoLoadReferencedAssemblies
    {
        get => autoLoadReferencedAssemblies;
        set => SetProperty(ref autoLoadReferencedAssemblies, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadFromXml(XElement section)
    {
        RestoreAssemblies = (bool?)section.Attribute(nameof(RestoreAssemblies)) ?? true;
        UseDebugSymbols = (bool?)section.Attribute(nameof(UseDebugSymbols)) ?? true;
        ApplyWinRtProjections = (bool?)section.Attribute(nameof(ApplyWinRtProjections)) ?? false;
        ShowCompilerGeneratedMembers = (bool?)section.Attribute(nameof(ShowCompilerGeneratedMembers)) ?? false;
        ShowInternalApi = (bool?)section.Attribute(nameof(ShowInternalApi)) ?? false;
        AutoLoadReferencedAssemblies = (bool?)section.Attribute(nameof(AutoLoadReferencedAssemblies)) ?? false;
    }

    public XElement SaveToXml()
    {
        var section = new XElement(SectionName);

        section.SetAttributeValue(nameof(RestoreAssemblies), RestoreAssemblies);
        section.SetAttributeValue(nameof(UseDebugSymbols), UseDebugSymbols);
        section.SetAttributeValue(nameof(ApplyWinRtProjections), ApplyWinRtProjections);
        section.SetAttributeValue(nameof(ShowCompilerGeneratedMembers), ShowCompilerGeneratedMembers);
        section.SetAttributeValue(nameof(ShowInternalApi), ShowInternalApi);
        section.SetAttributeValue(nameof(AutoLoadReferencedAssemblies), AutoLoadReferencedAssemblies);

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
