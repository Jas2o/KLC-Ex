using LibKaseya;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1].Contains("klcex:"))
                savedAuthToken = string.Join("", args[1].ToCharArray().Where(Char.IsDigit));
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

        #region Launch
        public enum LaunchMethod {
            System,
            DirectKaseya,
            DirectKaseyaMITM,
            DirectAlternative
        }

        public enum LaunchAction {
            LiveConnect,
            RemoteControlShared,
            RemoteControlPrivate,
            Terminal
        }

        public void Launch(Machine agent, LaunchMethod method, LaunchAction action) {
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            //LiveConnect is default
            if (action == LaunchAction.RemoteControlShared) {
                command.SetForRemoteControl(false, true);
                if (!ConnectPromptWithAdminBypass(agent))
                    return;
            } else if (action == LaunchAction.RemoteControlPrivate)
                command.SetForRemoteControl(true, true);
            else if (action == LaunchAction.Terminal)
                command.SetForTerminal(agent.OS == "Mac OS X");

            if (method == LaunchMethod.System)
                Process.Start("kaseyaliveconnect:///" + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(command))));
            else if (method == LaunchMethod.DirectKaseya)
                command.Launch(false, LaunchExtra.None);
            else if (method == LaunchMethod.DirectKaseyaMITM)
                command.Launch(false, LaunchExtra.Hawk);
            else if (method == LaunchMethod.DirectAlternative)
                command.Launch(true, LaunchExtra.None);
        }

        private bool ConnectPromptWithAdminBypass(Machine agent) {
            string agentName = agent.ComputerName;
            string agentDWG = (agent.DomainWorkgroup == null ? "" : agent.DomainWorkgroup);
            string agentUserLast = (agent.UserLast == null ? "" : agent.UserLast);
            string agentUserCurrent = (agent.UserCurrent == null ? "" : agent.UserCurrent);

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
        #endregion

        public delegate void HasConnected();
        public void UpdateGroupsAndViews() {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                progressRefresh.IsIndeterminate = true;

                model.MachineGroups.Clear();
                model.MachineGroups.Add(new MachineGroup(null, "< All Groups >", null, null));
                cmbOrg.SelectedIndex = 0;

                model.VSAViews.Clear();
                model.VSAViews.Add(new VSAView("", "< No View >"));
                cmbView.SelectedIndex = 0;
            });

            int records = 0;
            int num = 0;
            do {
                IRestResponse response = Kaseya.GetRequest(authToken, "api/v1.0/system/machinegroups?$orderby=MachineGroupName%20asc&$skip=" + num);
                dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                records = (int)result["TotalRecords"];
                foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                    Application.Current.Dispatcher.Invoke((Action)delegate {
                        model.MachineGroups.Add(new MachineGroup(child));
                    });
                }
                num += 100;
            } while (num < records);

            IRestResponse response2;
            response2 = Kaseya.GetRequest(authToken, "api/v1.0/system/views");
            dynamic result2 = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response2.Content);
            //records = (int)result["TotalRecords"];
            foreach (Newtonsoft.Json.Linq.JObject child in result2["Result"].Children()) {
                Application.Current.Dispatcher.Invoke((Action)delegate {
                    model.VSAViews.Add(new VSAView(child));
                });
            }

            Application.Current.Dispatcher.Invoke((Action)delegate {
                stackFilter.Opacity = 1.0;
                if(model.MachineGroups.Count > 1)
                    menuLoadToken.Header = "Token Loaded";
                else
                    menuLoadToken.Header = "Load Token";

                progressRefresh.IsIndeterminate = false;
            });
        }

        private void ConnectLMS() {
            //api/v1.0/system/machinegroups?$top=5&$filter=(substringof(%27ramvek%27,tolower(MachineGroupName))%20eq%20true)&$orderby=MachineGroupName%20asc
            menuLoadToken.Header = "Loading...";
            SetConnectButtons(false);

            Task.Run(() =>
            {
                HasConnected callback = new HasConnected(UpdateGroupsAndViews);
                KaseyaAuth auth = KaseyaAuth.ApiAuthX(authToken);
                callback();
            });
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
            SetConnectButtons(false);
            if (authToken == null)
                return;

            progressRefresh.IsIndeterminate = true;
            MachineGroup group = (MachineGroup)cmbOrg.SelectedItem;
            VSAView vsaView = (VSAView)cmbView.SelectedItem;
            string filterName = Regex.Replace(txtFilterMachineId.Text, "[^a-zA-Z0-9_.-]+", "", RegexOptions.Compiled);

            BackgroundWorker bw = new BackgroundWorker();
            //bw.WorkerReportsProgress = true;
            bw.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args) {
                string root = (group != null ? group.GroupName.Replace(".root", "") : "");

                //https://vsa-web.company.com.au/api/v1.0/assetmgmt/agents?$top=15&$filter=(MachineGroupId%20eq%2056591821512413912341567131M)%20and%20(ShowToolTip%20lt%20100%20or%20ShowToolTip%20eq%20null)&$orderby=AgentName%20asc

                int records = 0;
                int num = 0;
                do {
                    string query = "api/v1.0/assetmgmt/agents";
                    if (vsaView != null && vsaView.ViewId != "") {
                        //GET /assetmgmt/agentsinview/{viewId}
                        query += "inview/" + vsaView.ViewId;
                    }

                    if (group == null || group.GroupId == null) {
                        query += "?$top=50&$filter=substringof('" + filterName + "',%20ComputerName)&$orderby=AgentName%20asc&$skip=" + num;
                    } else {
                        if (group.GroupName.EndsWith(".root")) { //&& chkFilterGroupSub.IsChecked == true
                            query += "?$top=50&$filter=endswith(MachineGroup,%20'" + root + "')%20and%20substringof('" + filterName + "',%20ComputerName)&$orderby=AgentName%20asc&$skip=" + num;
                        } else {
                            query += "?$top=50&$filter=(MachineGroupId%20eq%20" + group.GroupId + "M)%20and%20substringof('" + filterName + "',%20ComputerName)&$orderby=AgentName%20asc&$skip=" + num;
                        }
                    }

                    IRestResponse response = Kaseya.GetRequest(authToken, query);

                    dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                    if (result["Status"] != "OK")
                        break;
                    records = (int)result["TotalRecords"];
                    Application.Current.Dispatcher.Invoke((Action)delegate {
                        foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                            model.ListMachine.Add(new Machine(child));
                        }
                    });
                    num += 50;

                    if (num > 150)
                        break;
                } while (num < records);
            });

            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate (object o, RunWorkerCompletedEventArgs args) {
                progressRefresh.IsIndeterminate = false;
            });

            bw.RunWorkerAsync();
        }

        #region Buttons: Launch
        private void btnConnectLaunch_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, LaunchMethod.System, LaunchAction.LiveConnect);
        }

        private void btnConnectShared_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, LaunchMethod.System, LaunchAction.RemoteControlShared);
        }

        private void btnConnectPrivate_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, LaunchMethod.System, LaunchAction.RemoteControlPrivate);
        }

        private void btnSendToProxy_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            if (agent == null)
                return;

            Process process = new Process();
            process.StartInfo.FileName = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\KLCProxy.exe";
            process.StartInfo.Arguments = agent.Guid;
            process.Start();
        }

        private void btnConnectAltLaunch_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, LaunchMethod.DirectAlternative, LaunchAction.LiveConnect);
        }

        private void btnConnectAltShared_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, LaunchMethod.DirectAlternative, LaunchAction.RemoteControlShared);
        }

        private void btnConnectAltPrivate_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, LaunchMethod.DirectAlternative, LaunchAction.RemoteControlPrivate);
        }

        private void btnConnectOriginalLiveConnect_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, (useMITM ? LaunchMethod.DirectKaseyaMITM : LaunchMethod.DirectKaseya), LaunchAction.LiveConnect);
        }

        private void btnConnectOriginalShared_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, (useMITM ? LaunchMethod.DirectKaseyaMITM : LaunchMethod.DirectKaseya), LaunchAction.RemoteControlShared);
        }

        private void btnConnectOriginalPrivate_Click(object sender, RoutedEventArgs e) {
            Machine agent = (Machine)dataGridAgents.SelectedValue;
            Launch(agent, (useMITM ? LaunchMethod.DirectKaseyaMITM : LaunchMethod.DirectKaseya), LaunchAction.RemoteControlPrivate);
        }
        #endregion

        private void chkUseMITM_Checked(object sender, RoutedEventArgs e) {
            useMITM = true;
        }

        private void chkUseMITM_Unchecked(object sender, RoutedEventArgs e) {
            useMITM = false;
        }

        private void dataGridAgents_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            SetConnectButtons(true);
        }

        private void SetConnectButtons(bool value) {
            btnConnectLaunch.IsEnabled = value;
            btnConnectShared.IsEnabled = value;
            btnConnectPrivate.IsEnabled = value;
            btnSendToProxy.IsEnabled = value;

            btnConnectAltLaunch.IsEnabled = value;
            btnConnectAltShared.IsEnabled = value;
            btnConnectAltPrivate.IsEnabled = value;

            btnConnectOriginalLiveConnect.IsEnabled = value;
            btnConnectOriginalShared.IsEnabled = value;
            btnConnectOriginalPrivate.IsEnabled = value;
        }

        private void menuViewTest_Click(object sender, RoutedEventArgs e) {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                model.VSAViews.Clear();
                model.VSAViews.Add(new VSAView("", "< No View >"));
            });

            IRestResponse response;
            response = Kaseya.GetRequest(authToken, "api/v1.0/system/views");
            dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
            //records = (int)result["TotalRecords"];
            foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                Application.Current.Dispatcher.Invoke((Action)delegate {
                    model.VSAViews.Add(new VSAView(child));
                });
            }

            //listView = listView.OrderBy(x => x.ViewName).ToList();

            //Console.WriteLine(response.Content);
        }

        private void cmbView_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                searchRefresh();
            }
        }

        private void cmbView_DropDownClosed(object sender, EventArgs e) {
            searchRefresh();
        }
    }
}
