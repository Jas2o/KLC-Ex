using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KLC_Ex {

    public class ViewModel : INotifyPropertyChanged {
        public ObservableCollection<VSAView> VSAViews { get; set; }
        public ObservableCollection<MachineGroup> MachineGroups { get; set; }
        public ObservableCollection<Machine> ListMachine { get; set; }

        public ObservableCollection<AgentProc> ListAgentProc { get; set; }
        public ObservableCollection<AgentProcHistory> ListAgentProcHistory { get; set; }
        public ObservableCollection<AgentProcScheduled> ListAgentProcScheduled { get; set; }
        public ObservableCollection<AgentProcLog> ListAgentProcLog { get; set; }

        public ObservableCollection<AgentProcMHS> ListAgentProcMHS { get; set; } //Machine/History/Scheduled
        public ObservableCollection<MachineRM> ListMachineRM { get; set; } //Remote Maintenance

        private Machine _selectedAgent;
        public Machine SelectedAgent {
            get { return _selectedAgent; }
            set {
                _selectedAgent = value;
                OnPropertyChange("SelectedAgent");
            }
        }

        private AgentProc _selectedAgentProc;
        public AgentProc SelectedAgentProc {
            get { return _selectedAgentProc; }
            set {
                _selectedAgentProc = value;
                OnPropertyChange("SelectedAgentProc");
            }
        }

        public ViewModel() {
            VSAViews = new ObservableCollection<VSAView>();
            MachineGroups = new ObservableCollection<MachineGroup>();
            ListMachine = new ObservableCollection<Machine>();

            ListAgentProc = new ObservableCollection<AgentProc>();
            ListAgentProcHistory = new ObservableCollection<AgentProcHistory>();
            ListAgentProcScheduled = new ObservableCollection<AgentProcScheduled>();
            ListAgentProcLog = new ObservableCollection<AgentProcLog>();

            ListAgentProcMHS = new ObservableCollection<AgentProcMHS>();
            ListMachineRM = new ObservableCollection<MachineRM>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChange(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool showColumnNetwork;
        public bool ShowColumnNetwork
        {
            get { return showColumnNetwork; }
            set
            {
                showColumnNetwork = value;
                OnPropertyChange("ShowColumnNetwork");
            }
        }

        private bool showColumnExtras;
        public bool ShowColumnExtras
        {
            get { return showColumnExtras; }
            set
            {
                showColumnExtras = value;
                OnPropertyChange("ShowColumnExtras");
            }
        }
    }
}