using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Input;
using Dock.Model.Adapters;
using Dock.Model.Core;

namespace Dock.Model.TomsToolbox.Core;

[DataContract(IsReference = true)]
public abstract class DockBase : DockableBase, IDock
{
    internal INavigateAdapter _navigateAdapter;
    private IList<IDockable>? _visibleDockables;
    private IDockable? _activeDockable;
    private IDockable? _defaultDockable;
    private IDockable? _focusedDockable;
    private int _openedDockablesCount = 0;
    private bool _canCloseLastDockable = true;
    private bool _enableGlobalDocking = true;

    protected DockBase()
    {
        _navigateAdapter = new NavigateAdapter(this);
        GoBack = new RelayCommand(() => _navigateAdapter.GoBack());
        GoForward = new RelayCommand(() => _navigateAdapter.GoForward());
        Navigate = new RelayCommand<object>(root => _navigateAdapter.Navigate(root, true));
        Close = new RelayCommand(() => _navigateAdapter.Close());
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public IList<IDockable>? VisibleDockables
    {
        get => _visibleDockables;
        set => SetProperty(ref _visibleDockables, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public IDockable? ActiveDockable
    {
        get => _activeDockable;
        set
        {
            SetProperty(ref _activeDockable, value);
            Factory?.InitActiveDockable(value, this);
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public IDockable? DefaultDockable
    {
        get => _defaultDockable;
        set => SetProperty(ref _defaultDockable, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public IDockable? FocusedDockable
    {
        get => _focusedDockable;
        set
        {
            SetProperty(ref _focusedDockable, value);
            Factory?.OnFocusedDockableChanged(value);
        }
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public int OpenedDockablesCount
    {
        get => _openedDockablesCount;
        set => SetProperty(ref _openedDockablesCount, value);
    }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool CanCloseLastDockable
    {
        get => _canCloseLastDockable;
        set => SetProperty(ref _canCloseLastDockable, value);
    }

    [IgnoreDataMember]
    public bool CanGoBack => _navigateAdapter.CanGoBack;

    [IgnoreDataMember]
    public bool CanGoForward => _navigateAdapter.CanGoForward;

    [IgnoreDataMember]
    public ICommand GoBack { get; private set; }

    [IgnoreDataMember]
    public ICommand GoForward { get; private set; }

    [IgnoreDataMember]
    public ICommand Navigate { get; private set; }

    [IgnoreDataMember]
    public ICommand Close { get; private set; }

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool EnableGlobalDocking
    {
        get => _enableGlobalDocking;
        set => SetProperty(ref _enableGlobalDocking, value);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        _navigateAdapter = new NavigateAdapter(this);
        GoBack = new RelayCommand(() => _navigateAdapter.GoBack());
        GoForward = new RelayCommand(() => _navigateAdapter.GoForward());
        Navigate = new RelayCommand<object>(root => _navigateAdapter.Navigate(root, true));
        Close = new RelayCommand(() => _navigateAdapter.Close());
    }
}
