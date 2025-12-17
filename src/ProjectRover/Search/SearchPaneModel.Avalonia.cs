using System.Linq;
using ICSharpCode.ILSpyX.Search;

namespace ICSharpCode.ILSpy.Search
{
    public partial class SearchPaneModel
    {
        public SearchModeModel SelectedSearchMode
        {
            get => SearchModes.FirstOrDefault(m => m.Mode == SessionSettings.SelectedSearchMode) ?? SearchModes[0];
            set
            {
                if (value != null)
                {
                    SessionSettings.SelectedSearchMode = value.Mode;
                    OnPropertyChanged(nameof(SelectedSearchMode));
                }
            }
        }
    }
}
