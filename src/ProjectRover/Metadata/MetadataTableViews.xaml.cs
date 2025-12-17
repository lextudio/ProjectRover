using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ICSharpCode.ILSpy.Metadata
{
    public partial class MetadataTableViews : ResourceDictionary
    {
        public MetadataTableViews()
        {
            // ResourceDictionary will be loaded by Avalonia XAML infrastructure when referenced.
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
