using System.Runtime.Serialization;
using Dock.Model.Controls;
using Dock.Model.TomsToolbox.Core;

namespace Dock.Model.TomsToolbox.Controls;

[DataContract(IsReference = true)]
public class Tool : DockableBase, ITool, IDocument
{
}
