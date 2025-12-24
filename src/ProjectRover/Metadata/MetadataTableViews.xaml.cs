using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ICSharpCode.ILSpy.Metadata
{
    public partial class MetadataTableViews : ResourceDictionary
    {
        public MetadataTableViews()
        {
            AvaloniaXamlLoader.Load(this);
        }

        static MetadataTableViews instance;

        public static MetadataTableViews Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MetadataTableViews();
                }
                return instance;
            }
        }
    }
}
