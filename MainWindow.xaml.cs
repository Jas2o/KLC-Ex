using LibKaseya;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KLCEx {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private ViewModel model;
        private string authToken;
        private bool useMITM = false;

        public MainWindow() {
            model = new ViewModel();
            this.DataContext = model;
            InitializeComponent();
            Kaseya.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            string savedAuthToken = KaseyaAuth.GetStoredAuth();
            if (savedAuthToken != null) {
                authToken = savedAuthToken;
                ConnectLMS();
            }

            if (!File.Exists(@"C:\Program Files\Kaseya Live Connect-MITM\KaseyaLiveConnect.exe"))
                chkUseMITM.Visibility = Visibility.Hidden;
        }

        private void menuLoadToken_Click(object sender, RoutedEventArgs e) {
            string savedAuthToken = KaseyaAuth.GetStoredAuth();

            WindowAuthToken dialog = new WindowAuthToken();
            dialog.Owner = this;
            if (savedAuthToken != null)
                dialog.ResponseText = savedAuthToken;
            bool accept = (bool)dialog.ShowDialog();

            if (accept && dialog.ResponseText.Length > 0) {
                authToken = dialog.ResponseText;

                ConnectLMS();
            }
        }

        private void ConnectLMS() {
            //api/v1.0/system/machinegroups?$top=5&$filter=(substringof(%27ramvek%27,tolower(MachineGroupName))%20eq%20true)&$orderby=MachineGroupName%20asc

            model.MachineGroups.Clear();
            model.MachineGroups.Add(new MachineGroup(null, "< All Groups >", null, null));

            int records = 0;
            int num = 0;
            do {
                IRestResponse response = Kaseya.GetRequest(authToken, "api/v1.0/system/machinegroups?$orderby=MachineGroupName%20asc&$skip=" + num);
                dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                records = (int)result["TotalRecords"];
                foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                    model.MachineGroups.Add(new MachineGroup(child));
                }
                num += 100;
            } while (num < records);

            stackFilter.Opacity = 1.0;
        }

        private void txtFilterMachineId_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Enter) {
                searchRefresh();
            }
        }

        private void cmbOrg_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                searchRefresh();
            }
        }

        private void cmbOrg_DropDownClosed(object sender, EventArgs e) {
            searchRefresh();
        }

        private void btnFilterRefresh_Click(object sender, RoutedEventArgs e) {
            searchRefresh();
        }

        private void searchRefresh() {
            model.ListMachine.Clear();
            if (authToken == null)
                return;

            MachineGroup group = (MachineGroup)cmbOrg.SelectedItem;
            string root = (group != null ? group.GroupName.Replace(".root", "") : "");

            //https://vsa-web.company.com.au/api/v1.0/assetmgmt/agents?$top=15&$filter=(MachineGroupId%20eq%2056591821512413912341567131M)%20and%20(ShowToolTip%20lt%20100%20or%20ShowToolTip%20eq%20null)&$orderby=AgentName%20asc

            string filterName = Regex.Replace(txtFilterMachineId.Text, "[^a-zA-Z0-9_.-]+", "", RegexOptions.Compiled);

            int records = 0;
            int num = 0;
            do {
                IRestResponse response;
                if (group == null || group.GroupId == null) {
                    response = Kaseya.GetRequest(authToken, "api/v1.0/assetmgmt/agents?$top=50&$filter=substringof('" + filterName + "',%20ComputerName)&$orderby=AgentName%20asc&$skip=" + num);
                } else {
                    if (group.GroupName.EndsWith(".root") && chkFilterGroupSub.IsChecked == true) {
                        response = Kaseya.GetRequest(authToken, "api/v1.0/assetmgmt/agents?$top=50&$filter=endswith(MachineGroup,%20'" + root + "')%20and%20substringof('" + filterName + "',%20ComputerName)&$orderby=AgentName%20asc&$skip=" + num);
                    } else {
                        response = Kaseya.GetRequest(authToken, "api/v1.0/assetmgmt/agents?$top=50&$filter=(MachineGroupId%20eq%20" + group.GroupId + "M)%20and%20substringof('" + filterName + "',%20ComputerName)&$orderby=AgentName%20asc&$skip=" + num);
                    }
                }

                dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                records = (int)result["TotalRecords"];
                foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                    model.ListMachine.Add(new Machine(child));
                }
                num += 50;

                if (num > 150)
                    break;
            } while (num < records);
        }

        private void btnConnectAltLaunch_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            command.Launch(true, false);
        }

        private void btnConnectAltShared_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            command.SetForRemoteControl(false, true);

            if (ConnectPromptWithAdminBypass(agent))
                command.Launch(true, false);
        }

        private void btnConnectAltPrivate_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            command.SetForRemoteControl(true, true);
            command.Launch(true, false);
        }

        private void btnConnectOriginalLiveConnect_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            command.SetForLiveConnect();
            command.Launch(false, false);
        }

        private void btnConnectOriginalShared_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            command.SetForRemoteControl(false, true);

            if (ConnectPromptWithAdminBypass(agent))
                command.Launch(false, useMITM);
        }

        private void btnConnectOriginalPrivate_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            command.SetForRemoteControl(true, true);
            command.Launch(false, useMITM);
        }

        private bool ConnectPromptWithAdminBypass(Machine agent) {
            string agentName = agent.ComputerName;
            string agentDWG = agent.DomainWorkgroup;
            string agentUserLast = agent.UserLast;
            string agentUserCurrent = agent.UserCurrent;

            //string displayGroup = agentApi["Result"]["MachineGroup"];
            string displayUser = (agentUserCurrent != "" ? agentUserCurrent : agentUserLast);
            string displayGWG = "";

            if (agent.OS != "Mac OS X")
                displayGWG = (agentDWG.Contains("(d") ? "Domain: " : "Workgroup: ") + agentDWG.Substring(0, agentDWG.IndexOf(" ("));

            MessageBoxResult result;
            string[] arrAdmins = new string[] { "administrator", "brandadmin", "adminc", "company" };
            if (arrAdmins.Contains(displayUser.ToLower())) {
                result = MessageBoxResult.Yes;
            } else {
                string textConfirm = string.Format("Agent: {0}\r\nUser: {1}\r\n{2}", agentName, displayUser, displayGWG);
                result = MessageBox.Show("Connect to:\r\n\r\n" + textConfirm, "Connecting to " + agentName, MessageBoxButton.YesNo, MessageBoxImage.Question);
            }

            return (result == MessageBoxResult.Yes);
        }

        private void chkUseMITM_Checked(object sender, RoutedEventArgs e) {
            useMITM = true;
        }

        private void chkUseMITM_Unchecked(object sender, RoutedEventArgs e) {
            useMITM = false;
        }
    }
}
