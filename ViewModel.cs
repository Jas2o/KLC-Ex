using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLCEx {
    public class ViewModel {

        public ObservableCollection<VSAView> VSAViews { get; set; }
        public ObservableCollection<MachineGroup> MachineGroups { get; set; }
        public ObservableCollection<Machine> ListMachine { get; set; }

        public ViewModel() {
            VSAViews = new ObservableCollection<VSAView>();
            MachineGroups = new ObservableCollection<MachineGroup>();
            ListMachine = new ObservableCollection<Machine>();
        }

    }
}
