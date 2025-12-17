using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy
{
    internal static class Images
    {
        private static string GetUri(string path) => $"avares://ProjectRover/Assets/{path}";

        public static object OK => GetUri("StatusOKOutline.svg");
        
        public static object Load(object owner, string path)
        {
             if (string.IsNullOrEmpty(path)) return null;
             if (path == "Images/Warning") return GetUri("StatusWarningOutline.svg");
             if (path.EndsWith(".svg")) return GetUri(path);
             return path;
        }

        public static string? ResolveIcon(object? icon)
        {
            if (icon is string s)
            {
                if (s.StartsWith("avares://") || s.StartsWith("/")) return s;
                if (App.Current.TryGetResource(s, App.Current.ActualThemeVariant, out var res) && res is string p)
                {
                    return p;
                }
            }
            return null;
        }

        public static IImage? LoadImage(object? icon)
        {
            string? path = ResolveIcon(icon);
            if (path == null) return null;
            
            if (path.StartsWith("/"))
            {
                path = $"avares://ProjectRover{path}";
            }

            if (path.EndsWith(".svg"))
            {
                try
                {
                    // Ensure we have a valid URI
                    if (!path.Contains("://"))
                    {
                         path = $"avares://ProjectRover/Assets/{path}";
                    }
                    return new SvgImage { Source = SvgSource.Load(path, null) };
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

		internal static object GetIcon(object icon, object overlay, bool isStatic)
		{
			string name = null;
			string access = "Public";

			if (overlay is AccessOverlayIcon accessOverlay)
			{
				switch (accessOverlay)
				{
					case AccessOverlayIcon.Public: access = "Public"; break;
					case AccessOverlayIcon.Internal: access = "Internal"; break;
					case AccessOverlayIcon.Protected: access = "Protected"; break;
					case AccessOverlayIcon.Private: access = "Private"; break;
					case AccessOverlayIcon.ProtectedInternal: access = "Protected"; break;
					case AccessOverlayIcon.PrivateProtected: access = "Private"; break;
					case AccessOverlayIcon.CompilerControlled: access = "Private"; break;
				}
			}

			if (icon is TypeIcon typeIcon)
			{
				switch (typeIcon)
				{
					case TypeIcon.Class: name = "Class"; break;
					case TypeIcon.Struct: name = "Struct"; break;
					case TypeIcon.Interface: name = "Interface"; break;
					case TypeIcon.Delegate: name = "Delegate"; break;
					case TypeIcon.Enum: name = "Enum"; break;
					default: name = "Class"; break;
				}
			}
			else if (icon is MemberIcon memberIcon)
			{
				switch (memberIcon)
				{
					case MemberIcon.Literal: name = "Constant"; break;
					case MemberIcon.FieldReadOnly: name = "Field"; break;
					case MemberIcon.Field: name = "Field"; break;
					case MemberIcon.Property: name = "Property"; break;
					case MemberIcon.Method: name = "Method"; break;
					case MemberIcon.Event: name = "Event"; break;
					case MemberIcon.EnumValue: name = "Enum"; break;
					case MemberIcon.Constructor: name = "Constructor"; break;
					case MemberIcon.VirtualMethod: name = "Method"; break;
					case MemberIcon.Operator: name = "Method"; break;
					case MemberIcon.ExtensionMethod: name = "Method"; break;
					case MemberIcon.PInvokeMethod: name = "Method"; break;
					case MemberIcon.Indexer: name = "Property"; break;
					default: name = "Method"; break;
				}
			}

			if (name != null)
			{
                // Construct key like "ClassPublicIcon" or "MethodIconPublic"
                // App.axaml keys are like "ClassIcon", "ClassInternalIcon" or "MethodIconPublic"
                // Let's try to match the pattern in App.axaml
                
                // Pattern 1: {Name}Icon{Access} (e.g. PropertyIconPublic)
                // Pattern 2: {Name}{Access}Icon (e.g. ClassInternalIcon)
                
                // Let's check App.axaml again.
                // ClassIcon (Public), ClassInternalIcon, ClassProtectedIcon...
                // PropertyIconPublic, PropertyIconProtected...
                // MethodIconPublic...
                // FieldIconPublic...
                // EventIconPublic...
                
                if (name == "Class" || name == "Struct" || name == "Interface" || name == "Enum" || name == "Delegate")
                {
                    if (access == "Public") return $"{name}Icon";
                    return $"{name}{access}Icon";
                }
                else
                {
                    return $"{name}Icon{access}";
                }
			}

			return "ClassIcon";
		}

		internal static object GetOverlayIcon(Accessibility accessibility)
		{
			// Return null for now (overlays not implemented in this shim)
			return null;
		}

		public static object SubTypes => "SubTypesIcon"; // Missing in App.axaml?

		public static object ListFolder => "ResourcesIconClosed";
		public static object ListFolderOpen => "ResourcesIconOpen";

		public static object Header => "HeaderIcon";
		public static object MetadataTableGroup => "TablesIcon";
		public static object Library => "AssemblyIcon";
		public static object Namespace => "NamespaceIcon";
		public static object FolderClosed => "ResourcesIconClosed";
		public static object FolderOpen => "ResourcesIconOpen";
		public static object MetadataTable => "DataTableIcon";
		public static object ExportedType => "ClassIcon";
		public static object TypeReference => "ClassIcon";
		public static object MethodReference => "MethodIcon";
		public static object FieldReference => "FieldIcon";
		public static object Interface => "InterfaceIcon";
		public static object Class => "ClassIcon";
		public static object Field => "FieldIcon";
		public static object Method => "MethodIcon";
		public static object Property => "PropertyIcon";
		public static object Event => "EventIcon";
		public static object Literal => "ConstantIcon";
		public static object Save => "SaveIcon"; // Missing?
		public static object Assembly => "AssemblyIcon";
		public static object ViewCode => "ViewCodeIcon"; // Missing?
		public static object AssemblyWarning => "ReferenceWarningIcon";
		public static object MetadataFile => "MetadataIcon";
		public static object FindAssembly => "OpenIcon";
		public static object SuperTypes => "SuperTypesIcon"; // Missing?
		public static object ReferenceFolder => "ReferenceGroupIcon";
		public static object ResourceImage => "ResourceIcon"; // Missing?
		public static object Resource => "ResourcesIcon";
		public static object ResourceResourcesFile => "ResourceFileIcon";
		public static object ResourceXml => "ResourceFileIcon";
		public static object ResourceXsd => "ResourceFileIcon";
		public static object ResourceXslt => "ResourceFileIcon";
		public static object Heap => "HeapIcon";
		public static object Metadata => "MetadataIcon";

		public static object Search => "SearchIcon";
	}
}
