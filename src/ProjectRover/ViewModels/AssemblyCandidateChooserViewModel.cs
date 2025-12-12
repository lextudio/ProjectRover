using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectRover.ViewModels;

public class AssemblyCandidateChooserViewModel
{
    public AssemblyCandidateChooserViewModel(IEnumerable<string> candidates)
    {
        Candidates = new ObservableCollection<string>(candidates);
        Selected = Candidates.FirstOrDefault();
    }

    public ObservableCollection<string> Candidates { get; }
    public string? Selected { get; set; }
}
