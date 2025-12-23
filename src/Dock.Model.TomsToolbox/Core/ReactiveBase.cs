using System.Runtime.Serialization;
using TomsToolbox.Wpf;

namespace Dock.Model.TomsToolbox.Core;

/// <summary>
/// Reactive base class.
/// </summary>
[DataContract(IsReference = true)]
public abstract class ReactiveBase : ObservableObject
{
}
