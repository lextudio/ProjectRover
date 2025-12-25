using System;
using System.Globalization;

namespace ICSharpCode.ILSpy.Metadata;

class HexFormatConverter : IValueConverter
{
	public static readonly HexFormatConverter Instance = new HexFormatConverter();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is int intValue)
			return intValue.ToString("X8");
		if (value is long longValue)
			return longValue.ToString("X16");
		if (value is uint uintValue)
			return uintValue.ToString("X8");
		if (value is ulong ulongValue)
			return ulongValue.ToString("X16");
		return value?.ToString() ?? "";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}