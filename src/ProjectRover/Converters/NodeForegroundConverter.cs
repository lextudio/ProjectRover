using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ICSharpCode.ILSpy.TreeNodes;

namespace ICSharpCode.ILSpy.Converters
{
    public class NodeForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ILSpyTreeNode node)
            {
                if (node.IsAutoLoaded)
                {
                    return Brushes.SteelBlue;
                }
                if (!node.IsPublicAPI)
                {
                    return Brushes.Gray; // SystemColors.GrayTextBrushKey equivalent
                }
            }
            return AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
