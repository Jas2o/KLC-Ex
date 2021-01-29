using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLCEx {
    public class ViewModel {

        public ObservableCollection<MachineGroup> MachineGroups { get; set; }
        public ObservableCollection<Machine> ListMachine { get; set; }

        public ViewModel() {
            MachineGroups = new ObservableCollection<MachineGroup>();
            ListMachine = new ObservableCollection<Machine>();
        }

    }
}
