using System.Xml.Linq;
using Avalonia.Media;

namespace ICSharpCode.ILSpy.Options
{
	public partial class DisplaySettings
	{
		FontFamily selectedFont;
		public FontFamily SelectedFont {
			get => selectedFont;
			set => SetProperty(ref selectedFont, value);
		}

		private void LoadSelectedFont(XElement section)
		{
			this.SelectedFont = new FontFamily((string)section.Attribute("Font") ?? "Consolas");
		}

		private void SaveSelectedFont(XElement section)
		{
			section.SetAttributeValue("Font", this.SelectedFont.Name);
		}
   	}
}
