using System.Runtime.Serialization;
using Dock.Model.Adapters;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace Dock.Model.TomsToolbox.Core;

[DataContract(IsReference = true)]
public class DockWindow : ReactiveBase, IDockWindow
{
    private IHostAdapter _hostAdapter;
    private string _id;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private bool _topmost;
    private string _title;
    private IDockable? _owner;
    private IFactory? _factory;
    private IRootDock? _layout;
    private IHostWindow? _host;

    public DockWindow()
    {
        _id = nameof(IDockWindow);
        _title = nameof(IDockWindow);
        _hostAdapter = new HostAdapter(this);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool Topmost
    {
        get => _topmost;
        set => SetProperty(ref _topmost, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    [IgnoreDataMember]
    public IDockable? Owner
    {
        get => _owner;
        set => SetProperty(ref _owner, value);
    }

    [IgnoreDataMember]
    public IFactory? Factory
    {
        get => _factory;
        set => SetProperty(ref _factory, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public IRootDock? Layout
    {
        get => _layout;
        set => SetProperty(ref _layout, value);
    }

    [IgnoreDataMember]
    public IHostWindow? Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public virtual bool OnClose() => true;
    public virtual bool OnMoveDragBegin() => true;
    public virtual void OnMoveDrag() { }
    public virtual void OnMoveDragEnd() { }

    public void Save() => _hostAdapter.Save();
    public void Present(bool isDialog) => _hostAdapter.Present(isDialog);
    public void Exit() => _hostAdapter.Exit();
    public void SetActive() => _hostAdapter.SetActive();

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        _hostAdapter = new HostAdapter(this);
    }
}
