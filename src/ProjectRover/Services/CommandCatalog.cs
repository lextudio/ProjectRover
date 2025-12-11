using System.Collections.Generic;

namespace ProjectRover.Services;

public record CommandDescriptor(
    string Id,
    string DisplayName,
    string IconKey,
    string Group,
    string? Shortcut = null,
    string? Description = null);

public interface ICommandCatalog
{
    IReadOnlyList<CommandDescriptor> Commands { get; }
}

public sealed class CommandCatalog : ICommandCatalog
{
    private static readonly IReadOnlyList<CommandDescriptor> defaultCommands = new[]
    {
        new CommandDescriptor("cmdidOpenILSpy", "Open in ILSpy", "ReferenceIcon", "Menu.Tools", "Ctrl+Alt+O", "Open the selected item in ILSpy."),
        new CommandDescriptor("cmdidOpenReferenceInILSpy", "Open Reference in ILSpy", "ReferenceIcon", "ContextMenu.Reference", "Ctrl+Alt+R", "Navigate to the reference target in ILSpy."),
        new CommandDescriptor("cmdidOpenProjectOutputInILSpy", "Open Project Output in ILSpy", "AssemblyIcon", "Menu.Project", null, "Decompile the project output assembly."),
        new CommandDescriptor("cmdidOpenCodeItemInILSpy", "Open Code Item in ILSpy", "ClassIcon", "ContextMenu.CodeItem", "Ctrl+Alt+C", "Open the selected code element in ILSpy."),
        new CommandDescriptor("cmdidCopyFullyQualifiedName", "Copy Fully Qualified Name", "CopyIcon", "ContextMenu.CodeItem", "Ctrl+Shift+C", "Copy the fully qualified name of the selected entity.")
    };

    public IReadOnlyList<CommandDescriptor> Commands => defaultCommands;
}
