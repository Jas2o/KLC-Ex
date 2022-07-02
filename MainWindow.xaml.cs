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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace KLCEx {

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private readonly ViewModel model;
        private string authToken;
        private bool useMITM = false;
        private BackgroundWorker bwRefresh;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10);
        private string vsaUserName;

        public MainWindow() {
            model = new ViewModel();
            this.DataContext = model;
            InitializeComponent();
            txtVersion.Text = Properties.Resources.BuildDate.Trim();

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

            if (!File.Exists(@"C:\Program Files\Kaseya Live Connect-MITM\KaseyaLiveConnect.exe") && !File.Exists(Environment.ExpandEnvironmentVariables(@"%localappdata%\Apps\Kaseya Live Connect-MITM\KaseyaLiveConnect.exe")))
                chkUseMITM.Visibility = Visibility.Collapsed;
            DisplayMachineNote(null);
        }

        private void btnLoadToken_Click(object sender, RoutedEventArgs e) {
            string savedAuthToken = KaseyaAuth.GetStoredAuth();

            WindowAuthToken dialog = new WindowAuthToken {
                Owner = this
            };
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
            RemoteControlOneClick,
            Terminal
        }

        public void Launch(Machine agent, LaunchMethod method, LaunchAction action) {
            if (agent == null)
                return;

            KLCCommand command = KLCCommand.Example(agent.Guid, authToken);
            //LiveConnect is default
            if (action == LaunchAction.RemoteControlShared)
            {
                command.SetForRemoteControl(false, true);
                if (!ConnectPromptWithAdminBypass(agent))
                    return;
            }
            else if (action == LaunchAction.RemoteControlPrivate)
                command.SetForRemoteControl(true, true);
            else if (action == LaunchAction.RemoteControlOneClick)
                command.SetForRemoteControl_OneClick();
            else if (action == LaunchAction.Terminal)
                command.SetForTerminal(agent.OSType == "Mac OS X");

            if (method == LaunchMethod.System) {
                Uri uri = new Uri("liveconnect:///" + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(command))));
                Process.Start(new ProcessStartInfo() { FileName = uri.ToString(), UseShellExecute = true }); //Fails
            } else if (method == LaunchMethod.DirectKaseya)
                command.Launch(false, LaunchExtra.None);
            else if (method == LaunchMethod.DirectKaseyaMITM)
                command.Launch(false, LaunchExtra.Hawk);
            else if (method == LaunchMethod.DirectAlternative)
                command.Launch(true, LaunchExtra.None);
        }

        private bool ConnectPromptWithAdminBypass(Machine agent) {
            string agentName = agent.AgentNameOnly;
            string agentDWG = agent.DomainWorkgroup ?? "";
            string agentUserLast = agent.UserLast ?? "";
            string agentUserCurrent = agent.UserCurrent ?? "";

            string displayGroup = agent.MachineGroupReverse;
            string displayUser = (agentUserCurrent != "" ? agentUserCurrent : agentUserLast);
            string displayGWG = "";

            if (agent.OSType != "Mac OS X")
                displayGWG = (agentDWG.Contains("(d") ? "Domain: " : "Workgroup: ") + agentDWG.Substring(0, agentDWG.IndexOf(" ("));

            MessageBoxResult result;
            string[] arrAdmins = new string[] { "administrator", "brandadmin", "adminc", "company" };
            if (arrAdmins.Contains(displayUser.ToLower())) {
                result = MessageBoxResult.Yes;
            } else {
                string textConfirm = string.Format("Agent: {0}\r\nGroup: {1}\r\n\r\nUser: {2}\r\n{3}", agentName, displayGroup, displayUser, displayGWG);
                result = MessageBox.Show("Connect to:\r\n\r\n" + textConfirm, "Connecting to " + agentName, MessageBoxButton.YesNo, MessageBoxImage.Question);
            }

            return (result == MessageBoxResult.Yes);
        }

        #endregion Launch

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
                IRestResponse response = Kaseya.GetRequest("api/v1.0/system/machinegroups?$orderby=MachineGroupName%20asc&$skip=" + num);
                dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                records = (int)result["TotalRecords"];
                foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                    Application.Current.Dispatcher.Invoke((Action)delegate {
                        model.MachineGroups.Add(new MachineGroup(child));
                    });
                }
                num += 100;
            } while (num < records);

            IRestResponse response2 = Kaseya.GetRequest("api/v1.0/system/views");
            dynamic result2 = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response2.Content);
            //records = (int)result["TotalRecords"];
            foreach (Newtonsoft.Json.Linq.JObject child in result2["Result"].Children()) {
                Application.Current.Dispatcher.Invoke((Action)delegate {
                    model.VSAViews.Add(new VSAView(child));
                });
            }

            Application.Current.Dispatcher.Invoke((Action)delegate {
                stackFilter.Opacity = 1.0;
                if (model.MachineGroups.Count > 1) {
                    btnLoadToken.Content = "Token Loaded";
                    btnLoadToken.Opacity = 0.5;
                    btnLoadToken.Background = Brushes.Transparent;
                } else {
                    btnLoadToken.Content = "Load Token";
                    btnLoadToken.Opacity = 1.0;
                    btnLoadToken.Background = Brushes.DarkOrange;
                }

                progressRefresh.IsIndeterminate = false;
            });
        }

        private void ConnectLMS() {
            //api/v1.0/system/machinegroups?$top=5&$filter=(substringof(%27ramvek%27,tolower(MachineGroupName))%20eq%20true)&$orderby=MachineGroupName%20asc
            btnLoadToken.Content = "Loading...";
            btnLoadToken.Background = Brushes.Gold;
            SetConnectButtons(false);

            Task.Run(() => {
                HasConnected callback = new HasConnected(UpdateGroupsAndViews);
                Kaseya.LoadToken(authToken);
                KaseyaAuth auth = KaseyaAuth.ApiAuthX(authToken, Kaseya.DefaultServer);
                vsaUserName = auth.UserName.Replace("/", "_"); //Same as in RC logs
                callback();
            });
        }

        private void TxtFilterMachineId_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter)
                SearchRefresh();
        }

        private void CmbOrg_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter)
                SearchRefresh();
        }

        private void CmbOrg_DropDownClosed(object sender, EventArgs e) {
            SearchRefresh();
        }

        private void BtnFilterRefresh_Click(object sender, RoutedEventArgs e) {
            SearchRefresh();
        }

        private void SearchRefresh() {
            if (bwRefresh != null) {
                if (bwRefresh.IsBusy)
                    return;
            }

            model.ListMachine.Clear();
            model.ListMachineRM.Clear();

            SetConnectButtons(false);
            if (authToken == null)
                return;

            progressRefresh.IsIndeterminate = true;
            MachineGroup group = (MachineGroup)cmbOrg.SelectedItem;
            VSAView vsaView = (VSAView)cmbView.SelectedItem;
            string filterName = Regex.Replace(txtFilterMachineId.Text, "[^a-zA-Z0-9_.-]+", "", RegexOptions.Compiled);

            bwRefresh = new BackgroundWorker();
            //bw.WorkerReportsProgress = true;
            bwRefresh.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args) {
                string root = (group != null ? group.GroupName.Replace(".root", "") : "");

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

                    IRestResponse response = Kaseya.GetRequest(query);

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

            bwRefresh.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate (object o, RunWorkerCompletedEventArgs args) {
                progressRefresh.IsIndeterminate = false;

                if (tabRM.IsSelected)
                    btnRmLoad_Click(null, null);
            });

            bwRefresh.RunWorkerAsync();
        }

        #region Buttons: Launch

        private Tuple<Machine, int> GetSelectedMachineByTab() {
            if (tabApListSchedule.IsSelected) {
                AgentProcMHS apMHS = (AgentProcMHS)dataGridAgentsSchedule.SelectedValue;
                if (apMHS != null && apMHS.Machine != null)
                    return Tuple.Create(apMHS.Machine, dataGridAgentsSchedule.SelectedItems.Count);
            } else if (tabRM.IsSelected) {
                MachineRM rm = (MachineRM)dataGridRM.SelectedValue;
                if (rm != null)
                    return Tuple.Create(rm.Machine, dataGridRM.SelectedItems.Count);
            } else {
                Machine agent = (Machine)dataGridAgents.SelectedValue;
                if (agent != null)
                    return Tuple.Create(agent, dataGridAgents.SelectedItems.Count);
            }

            return null;
        }

        private void BtnConnectLaunch_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, LaunchMethod.System, LaunchAction.LiveConnect);
        }

        private void BtnConnectShared_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, LaunchMethod.System, LaunchAction.RemoteControlShared);
        }

        private void BtnConnectPrivate_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, LaunchMethod.System, LaunchAction.RemoteControlPrivate);
        }

        private void BtnConnectOneClick_Click(object sender, RoutedEventArgs e)
        {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            if(agent.Item1.OneClickAccess)
                Launch(agent.Item1, LaunchMethod.System, LaunchAction.RemoteControlOneClick);
        }

        private void BtnSendToProxy_Click(object sender, RoutedEventArgs e) {
            if (tabApListSchedule.IsSelected) {
                if (dataGridAgentsSchedule.SelectedItems.Count == 0)
                    return;

                foreach (AgentProcMHS apMHS in dataGridAgentsSchedule.SelectedItems) {
                    Process process = new Process();
                    if (Process.GetProcessesByName("KLCProxyClassic").Any())
                        process.StartInfo.FileName = System.IO.Path.GetDirectoryName(Environment.ProcessPath) + @"\KLCProxyClassic.exe";
                    else
                        process.StartInfo.FileName = System.IO.Path.GetDirectoryName(Environment.ProcessPath) + @"\KLCProxy.exe";
                    process.StartInfo.Arguments = apMHS.Machine.Guid;
                    process.Start();
                    process.WaitForExit(2000);
                }
            } else {
                if (dataGridAgents.SelectedItems.Count == 0)
                    return;

                foreach (Machine agent in dataGridAgents.SelectedItems) {
                    Process process = new Process();
                    if (Process.GetProcessesByName("KLCProxyClassic").Any())
                        process.StartInfo.FileName = System.IO.Path.GetDirectoryName(Environment.ProcessPath) + @"\KLCProxyClassic.exe";
                    else
                        process.StartInfo.FileName = System.IO.Path.GetDirectoryName(Environment.ProcessPath) + @"\KLCProxy.exe";
                    process.StartInfo.Arguments = agent.Guid;
                    process.Start();
                    process.WaitForExit(2000);
                }
            }
        }

        private void BtnConnectAltLaunch_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, LaunchMethod.DirectAlternative, LaunchAction.LiveConnect);
        }

        private void BtnConnectAltShared_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, LaunchMethod.DirectAlternative, LaunchAction.RemoteControlShared);
        }

        private void BtnConnectAltPrivate_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, LaunchMethod.DirectAlternative, LaunchAction.RemoteControlPrivate);
        }

        private void BtnConnectAltOneClick_Click(object sender, RoutedEventArgs e)
        {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            if (agent.Item1.OneClickAccess)
                Launch(agent.Item1, LaunchMethod.DirectAlternative, LaunchAction.RemoteControlOneClick);
        }

        private void BtnConnectOriginalLiveConnect_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, (useMITM ? LaunchMethod.DirectKaseyaMITM : LaunchMethod.DirectKaseya), LaunchAction.LiveConnect);
        }

        private void BtnConnectOriginalShared_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, (useMITM ? LaunchMethod.DirectKaseyaMITM : LaunchMethod.DirectKaseya), LaunchAction.RemoteControlShared);
        }

        private void BtnConnectOriginalPrivate_Click(object sender, RoutedEventArgs e) {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            Launch(agent.Item1, (useMITM ? LaunchMethod.DirectKaseyaMITM : LaunchMethod.DirectKaseya), LaunchAction.RemoteControlPrivate);
        }

        private void BtnConnectOriginalOneClick_Click(object sender, RoutedEventArgs e)
        {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            if (agent.Item1.OneClickAccess)
                Launch(agent.Item1, (useMITM ? LaunchMethod.DirectKaseyaMITM : LaunchMethod.DirectKaseya), LaunchAction.RemoteControlOneClick);
        }

        #endregion Buttons: Launch

        private void ChkUseMITM_Checked(object sender, RoutedEventArgs e) {
            useMITM = true;
        }

        private void ChkUseMITM_Unchecked(object sender, RoutedEventArgs e) {
            useMITM = false;
        }

        private void MachineSelectionChangedFromTab() {
            Tuple<Machine, int> agent = GetSelectedMachineByTab();
            if (agent != null)
            {
                SetConnectButtons(true, agent.Item2, agent.Item1.OneClickAccess);
                if(agent.Item2 == 1)
                    DisplayMachineNote(agent.Item1);
                else
                    DisplayMachineNote(null);
                //DisplayRCNotify(agent.Item1)
            }
            else
            {
                SetConnectButtons(false);
                DisplayMachineNote(null);
            }
            model.ListAgentProcHistory.Clear();
            model.ListAgentProcLog.Clear();
            model.ListAgentProcScheduled.Clear();

            if (agent == null) {
                txtApMachineName.Content = "No machine";
                txtApMachineGroup.Content = " selected to view.";
                model.SelectedAgent = null;
            } else {
                txtApMachineName.Content = agent.Item1.AgentNameOnly;
                txtApMachineGroup.Content = "." + agent.Item1.MachineGroup;
                model.SelectedAgent = agent.Item1;
            }
        }

        /*
        public void DisplayRCNotify(LibKaseya.Enums.NotifyApproval policy)
        {
            switch (policy)
            {
                case LibKaseya.Enums.NotifyApproval.None:
                    txtRCNotify.Visibility = Visibility.Collapsed;
                    break;
                case LibKaseya.Enums.NotifyApproval.NotifyOnly:
                    txtRCNotify.Text = "Notification prompt only.";
                    break;
                case LibKaseya.Enums.NotifyApproval.ApproveAllowIfNoUser:
                    txtRCNotify.Text = "Approve prompt - allow if no one logged in.";
                    break;
                case LibKaseya.Enums.NotifyApproval.ApproveDenyIfNoUser:
                    txtRCNotify.Text = "Approve prompt - denied if no one logged in.";
                    break;
                default:
                    txtRCNotify.Text = "Unknown RC notify policy: " + policy;
                    break;
            }
        }
        */

        public void DisplayMachineNote(Machine agent)
        {
            if (agent == null || agent.MachineShowToolTip == 0 && agent.MachineNote.Length == 0)
            {
                txtSpecialInstructions.Visibility = txtMachineNote.Visibility = Visibility.Collapsed;
                return;
            } else
            {
                txtSpecialInstructions.Visibility = txtMachineNote.Visibility = Visibility.Visible;
            }

            if (agent.MachineShowToolTip > 0)
            {
                txtSpecialInstructions.Text = "Special Instructions for " + agent.AgentNameOnly.ToUpper();
                if (Enum.IsDefined(typeof(Machine.Badge), agent.MachineShowToolTip))
                    txtSpecialInstructions.Text += " (" + Enum.GetName(typeof(Machine.Badge), agent.MachineShowToolTip) + ")";
                else
                    txtSpecialInstructions.Text += " (" + agent.MachineShowToolTip + ")";
            }

            if (agent.MachineNoteLink != null)
            {
                txtMachineNoteLink.NavigateUri = new Uri(agent.MachineNoteLink);
                txtMachineNoteLinkText.Text = agent.MachineNoteLink;
                txtMachineNoteText.Text = agent.MachineNote;
            }
            else
            {
                txtMachineNoteLinkText.Text = string.Empty;
                txtMachineNoteText.Text = agent.MachineNote;
            }

        }

        private void txtMachineNoteLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
        }

        private void DataGridAgents_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            MachineSelectionChangedFromTab();
        }

        private void SetConnectButtons(bool value, int numSelected=0, bool IsOneClick=false) {
            bool v = (value && numSelected == 1);
            bool vOneClick = (IsOneClick && numSelected == 1);

            btnConnectLaunch.IsEnabled = v;
            btnConnectShared.IsEnabled = v;
            btnConnectPrivate.IsEnabled = v;
            btnConnectOneClick.IsEnabled = vOneClick;
            btnSendToProxy.IsEnabled = value;

            btnConnectAltLaunch.IsEnabled = v;
            btnConnectAltShared.IsEnabled = v;
            btnConnectAltPrivate.IsEnabled = v;
            btnConnectAltOneClick.IsEnabled = vOneClick;

            btnConnectOriginalLiveConnect.IsEnabled = v;
            btnConnectOriginalShared.IsEnabled = v;
            btnConnectOriginalPrivate.IsEnabled = v;
            btnConnectOriginalOneClick.IsEnabled = vOneClick;
        }

        private void MenuViewTest_Click(object sender, RoutedEventArgs e) {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                model.VSAViews.Clear();
                model.VSAViews.Add(new VSAView("", "< No View >"));
            });

            IRestResponse response = Kaseya.GetRequest("api/v1.0/system/views");
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

        private void CmbView_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SearchRefresh();
            }
        }

        private void CmbView_DropDownClosed(object sender, EventArgs e) {
            SearchRefresh();
        }

        private void txtAgentProcsFilter_TextChanged(object sender, TextChangedEventArgs e) {
            ListCollectionView collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dataGridAgentProcs.ItemsSource);
            collectionView.Filter = new Predicate<object>(x =>
                ((AgentProc)x).DisplayPath.IndexOf(txtAgentProcsFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                ((AgentProc)x).AgentProcedureName.IndexOf(txtAgentProcsFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0
            );

            //Resize the columns to fit what's displayed
            foreach (DataGridColumn c in dataGridAgentProcs.Columns) {
                c.Width = 0;
                c.Width = DataGridLength.Auto;
            }
        }

        private void btnAgentProcsLoad_Click(object sender, RoutedEventArgs e) {
            btnAgentProcsLoad.IsEnabled = false;
            progressRefresh.IsIndeterminate = true;

            BackgroundWorker bw = new BackgroundWorker();
            //bw.WorkerReportsProgress = true;
            bw.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args) {
                List<AgentProc> listAp = new List<AgentProc>();

                int totalRecords = 0;
                int skip = 0;
                IRestResponse response;
                do {
                    response = Kaseya.GetRequest("api/v1.0/automation/agentprocs?$top=100&$orderby=AgentProcedureName%20asc" + (skip > 0 ? "&$skip=" + skip : ""));

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        return;
                    dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                    totalRecords = (int)result["TotalRecords"];
                    foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                        listAp.Add(new AgentProc(child));
                    }

                    skip += 100;
                } while (skip < totalRecords);

                // Doing the sort in WPF is not too good for our needs.
                listAp = listAp.OrderBy(x => x.Path).ThenBy(x => x.AgentProcedureName).ToList();
                Application.Current.Dispatcher.Invoke((Action)delegate {
                    model.ListAgentProc.Clear();
                    foreach (AgentProc ap in listAp) {
                        model.ListAgentProc.Add(ap);
                    }
                });
            });

            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate (object o, RunWorkerCompletedEventArgs args) {
                progressRefresh.IsIndeterminate = false;
                btnAgentProcsLoad.IsEnabled = true;
            });

            bw.RunWorkerAsync();

            /*
            TreeViewItem root = new TreeViewItem();
            TreeViewItem currNode = null;
            foreach (AgentProc ap in listAp) {
                string[] parts = ap.Path.Split('/');

                TreeViewItem node = root;
                for (int i = 0; i < parts.Length; i++) {
                    string header = parts[i];

                    if (!TreeViewIfExists(node, header, ref node)) {
                        currNode = new TreeViewItem() {
                            Header = header
                        };

                        if (i == 0) {
                            root = currNode;
                            treeAgentProc.Items.Add(root);
                        } else {
                            node.Items.Add(currNode);
                            if (i == parts.Length - 1)
                                node = root;
                            else
                                node = currNode;
                        }
                    }
                }

                TreeViewItem apNode = new TreeViewItem() {
                    Header = ap.AgentProcedureName,
                    Tag = "<" + ap.Path + "> " + ap.AgentProcedureName
                };
                currNode.Items.Add(apNode);
            }
            */
        }

        private void btnAgentProcsFilterClear_Click(object sender, RoutedEventArgs e) {
            txtAgentProcsFilter.Clear();
        }

        private void expanderAp_Change(object sender, RoutedEventArgs e) {
            if (gsApScheduledHistory == null || gsApHistoryLogs == null)
                return;

            gridApMachine.RowDefinitions[1].Height = (expanderApScheduled.IsExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Auto));
            gridApMachine.RowDefinitions[3].Height = (expanderApHistory.IsExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Auto));
            gridApMachine.RowDefinitions[5].Height = (expanderApLogs.IsExpanded ? new GridLength(2, GridUnitType.Star) : new GridLength(1, GridUnitType.Auto));

            gsApScheduledHistory.IsEnabled = (expanderApScheduled.IsExpanded && expanderApHistory.IsExpanded);
            gsApHistoryLogs.IsEnabled = (expanderApHistory.IsExpanded && expanderApLogs.IsExpanded);
        }

        private void btnApMachineGet_Click(object sender, RoutedEventArgs e) {
            Machine agent = model.SelectedAgent;
            if (agent == null)
                return;

            model.ListAgentProcHistory.Clear();
            model.ListAgentProcScheduled.Clear();
            model.ListAgentProcLog.Clear();
            txtApMachineName.Content = agent.AgentNameOnly;
            txtApMachineGroup.Content = "." + agent.MachineGroup;

            IRestResponse responseHistory = Kaseya.GetRequest("api/v1.0/automation/agentprocs/" + agent.Guid + "/history?$top=25&$orderby=LastExecutionTime%20desc");
            dynamic resultHistory = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseHistory.Content);
            //int totalRecords = (int)result["TotalRecords"];

            IRestResponse responseScheduled = Kaseya.GetRequest("api/v1.0/automation/agentprocs/" + agent.Guid + "/scheduledprocs?$top=25&$orderby=NextExecTime%20desc");
            dynamic resultScheduled = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseScheduled.Content);
            //int totalRecords = (int)result["TotalRecords"];

            IRestResponse responseLogs = Kaseya.GetRequest("api/v1.0/assetmgmt/logs/" + agent.Guid + "/agentprocedure?$top=25&$orderby=LastExecution%20desc");
            dynamic resultLogs = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseLogs.Content);
            //int totalRecords = (int)result["TotalRecords"];

            foreach (Newtonsoft.Json.Linq.JObject child in resultHistory["Result"].Children()) {
                model.ListAgentProcHistory.Add(new AgentProcHistory(child));
            }
            foreach (Newtonsoft.Json.Linq.JObject child in resultScheduled["Result"].Children()) {
                model.ListAgentProcScheduled.Add(new AgentProcScheduled(child));
            }
            foreach (Newtonsoft.Json.Linq.JObject child in resultLogs["Result"].Children()) {
                model.ListAgentProcLog.Add(new AgentProcLog(child));
            }

            tabApMachine.IsSelected = true;
        }

        private async void btnAgentProcsSchedule_Click(object sender, RoutedEventArgs e) {
            AgentProc agentProc = (AgentProc)dataGridAgentProcs.SelectedItem;
            if (agentProc == null)
                return;

            btnAgentProcsSchedule.IsEnabled = btnAgentProcsRefreshAll.IsEnabled = btnAgentProcsRefreshPending.IsEnabled = false;
            progressRefresh.IsIndeterminate = true;

            model.ListAgentProcMHS.Clear();
            model.SelectedAgentProc = agentProc;
            expanderApSchedule.Header = "Schedule: " + agentProc.AgentProcedureName;

            //BackgroundWorker bw = new BackgroundWorker();
            //bw.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args) {
            //IRestResponse responseSchedule;
            //IRestResponse responseHistory;
            //foreach (Machine machine in model.ListMachine) {

            List<Task> tasks = model.ListMachine.Select(async machine => {
                await _semaphore.WaitAsync();
                AgentProcHistory history = null;
                AgentProcScheduled scheduled = null;

                try {    
                    IRestResponse responseSchedule = await Kaseya.GetRequestAsync("api/v1.0/automation/agentprocs/" + machine.Guid + "/scheduledprocs?$filter=AgentProcedureId eq " + agentProc.AgentProcedureId);
                    if (responseSchedule.StatusCode == System.Net.HttpStatusCode.OK) {
                        dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseSchedule.Content);
                        int records = (int)result["TotalRecords"];
                        if (records > 1) {
                            Application.Current.Dispatcher.Invoke((Action)delegate {
                                MessageBox.Show("Please tell Jason!\r\n\r\nProcedure: " + agentProc.AgentProcedureName + " (" + agentProc.Path + ")\r\nMachine: " + machine.AgentName + "\r\nSchedules: " + records);
                            });
                        }
                        foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                            scheduled = new AgentProcScheduled(child);
                        }
                    }

                    IRestResponse responseHistory = await Kaseya.GetRequestAsync("api/v1.0/automation/agentprocs/" + machine.Guid + "/history?$top=1&$filter=ScriptName eq '" + agentProc.AgentProcedureName + "'&$orderby=LastExecutionTime desc");
                    if (responseHistory.StatusCode == System.Net.HttpStatusCode.OK) {
                        dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseHistory.Content);
                        //int records = (int)result["TotalRecords"];
                        foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                            history = new AgentProcHistory(child);
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                Application.Current.Dispatcher.Invoke((Action)delegate {
                    model.ListAgentProcMHS.Add(new AgentProcMHS(machine, history, scheduled));
                });
            }).ToList();

            await Task.WhenAll(tasks);
            //}
            //});

            //bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            //delegate (object o, RunWorkerCompletedEventArgs args) {
                //Resize the columns to fit what's displayed
                foreach (DataGridColumn c in dataGridAgentsSchedule.Columns) {
                    c.Width = 0;
                    c.Width = DataGridLength.Auto;
                }

                progressRefresh.IsIndeterminate = false;
                btnAgentProcsSchedule.IsEnabled = btnAgentProcsRefreshAll.IsEnabled = btnAgentProcsRefreshPending.IsEnabled = true;
            //});

            //bw.RunWorkerAsync();
        }

        private void btnAgentProcsRefreshAll_Click(object sender, RoutedEventArgs e) {
            RefreshAgentProcMHS(false);
        }

        private void btnAgentProcsRefreshPending_Click(object sender, RoutedEventArgs e) {
            RefreshAgentProcMHS(true);
        }

        private void RefreshAgentProcMHS(bool JustPending) {
            if (model.SelectedAgentProc == null)
                return;

            btnAgentProcsSchedule.IsEnabled = btnAgentProcsRefreshAll.IsEnabled = btnAgentProcsRefreshPending.IsEnabled = false;
            progressRefresh.IsIndeterminate = true;

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args) {
                IRestResponse responseSchedule;
                IRestResponse responseHistory;

                AgentProc agentProc = model.SelectedAgentProc;
                for (int i = 0; i < model.ListAgentProcMHS.Count; i++) {
                    if (JustPending && model.ListAgentProcMHS[i].Schedule == null)
                        continue;

                    Machine machine = model.ListAgentProcMHS[i].Machine;
                    AgentProcHistory history = null;
                    AgentProcScheduled scheduled = null;

                    responseSchedule = Kaseya.GetRequest("api/v1.0/automation/agentprocs/" + machine.Guid + "/scheduledprocs?$filter=AgentProcedureId eq " + agentProc.AgentProcedureId);
                    if (responseSchedule.StatusCode == System.Net.HttpStatusCode.OK) {
                        dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseSchedule.Content);
                        int records = (int)result["TotalRecords"];
                        if (records > 1) {
                            Application.Current.Dispatcher.Invoke((Action)delegate {
                                MessageBox.Show("Please tell Jason!\r\n\r\nProcedure: " + agentProc.AgentProcedureName + " (" + agentProc.Path + ")\r\nMachine: " + machine.AgentName + "\r\nSchedules: " + records);
                            });
                        }
                        foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                            scheduled = new AgentProcScheduled(child);
                        }
                    }

                    responseHistory = Kaseya.GetRequest("api/v1.0/automation/agentprocs/" + machine.Guid + "/history?$top=1&$filter=ScriptName eq '" + agentProc.AgentProcedureName + "'&$orderby=LastExecutionTime desc");
                    if (responseHistory.StatusCode == System.Net.HttpStatusCode.OK) {
                        dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseHistory.Content);
                        //int records = (int)result["TotalRecords"];
                        foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                            history = new AgentProcHistory(child);
                        }
                    }

                    Application.Current.Dispatcher.Invoke((Action)delegate {
                        model.ListAgentProcMHS[i] = new AgentProcMHS(machine, history, scheduled);
                    });
                }
            });

            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate (object o, RunWorkerCompletedEventArgs args) {
                progressRefresh.IsIndeterminate = false;
                btnAgentProcsSchedule.IsEnabled = btnAgentProcsRefreshAll.IsEnabled = btnAgentProcsRefreshPending.IsEnabled = true;
            });

            bw.RunWorkerAsync();
        }

        private void expanderApListSchedule_Change(object sender, RoutedEventArgs e) {
            if (gsApListSchedule == null)
                return;

            gridAp.RowDefinitions[0].Height = (expanderApList.IsExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Auto));
            gridAp.RowDefinitions[2].Height = (expanderApSchedule.IsExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Auto));

            gsApListSchedule.IsEnabled = (expanderApList.IsExpanded && expanderApSchedule.IsExpanded);
        }

        private void dataGridAgentsSchedule_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            MachineSelectionChangedFromTab();
        }

        private async void btnRmLoad_Click(object sender, RoutedEventArgs e) {
            btnRmLoad.IsEnabled = false;
            progressRefresh.IsIndeterminate = true;

            model.ListMachineRM.Clear();

            //BackgroundWorker bw = new BackgroundWorker();
            //bw.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args) {
                List<Task> tasks = model.ListMachine.Select(async machine => {
                    await _semaphore.WaitAsync();
                    string cAvProd = "";
                    string cAvStatus = "";
                    string cAvProdDbDate = "";
                    string cEpsMaintNote = "";
                    string cPatchCompliance = "";
                    Dictionary<string, bool> dAv = new Dictionary<string, bool>();
                    int patchesMissing = 0;

                    try
                    {
                        IRestResponse responseSummary = await Kaseya.GetRequestAsync("api/v1.0/assetmgmt/audit/" + machine.Guid + "/summary");

                        
                        if (responseSummary.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseSummary.Content);

                            if (result["Result"] != null && result["Result"]["CustomFields"] != null)
                            {
                                if (result["Result"]["CustomFields"]["Antivirus Product"] != null)
                                    cAvProd = (string)result["Result"]["CustomFields"]["Antivirus Product"];
                                if (result["Result"]["CustomFields"]["Antivirus Status"] != null)
                                    cAvStatus = (string)result["Result"]["CustomFields"]["Antivirus Status"];
                                if (result["Result"]["CustomFields"]["Antivirus Product Database Date"] != null)
                                {
                                    cAvProdDbDate = (string)result["Result"]["CustomFields"]["Antivirus Product Database Date"];
                                    if (cAvProdDbDate.Length > 0)
                                    {
                                        DateTime cAvProdDbDate2;
                                        if (DateTime.TryParse(cAvProdDbDate, out cAvProdDbDate2))
                                        {
                                            cAvProdDbDate = cAvProdDbDate2.ToString("u");
                                        }
                                    }
                                }
                                if (result["Result"]["CustomFields"]["EPS Maint Note"] != null)
                                    cEpsMaintNote = (string)result["Result"]["CustomFields"]["EPS Maint Note"];
                                if (result["Result"]["CustomFields"]["Patch Compliance"] != null)
                                    cPatchCompliance = (string)result["Result"]["CustomFields"]["Patch Compliance"];
                            }
                        }

                        IRestResponse responseSecurity = await Kaseya.GetRequestAsync("api/v1.0/assetmgmt/audit/" + machine.Guid + "/software/securityproducts");
                        if (responseSecurity.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseSecurity.Content);
                            foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children())
                            {
                                if ((bool)child["IsActive"] == true && (string)child["ProductType"] == "Antivirus")
                                {
                                    string product = (string)child["ProductName"];
                                    if (product == "Kaspersky Endpoint Security 10 for Windows")
                                        product = "KAV";
                                    else if (product == "Sophos Anti-Virus")
                                        product = "Sophos";
                                    else if (product.StartsWith("Trend Micro"))
                                        product = "Trend Micro";
                                    else if (product == "Windows Defender" || product == "")
                                    {
                                        continue;
                                        //product = "Defender";
                                    }

                                    if (dAv.ContainsKey(product))
                                    {
                                        dAv[product] = dAv[product] || (bool)child["IsUpToDate"];
                                    }
                                    else
                                    {
                                        dAv.Add(product, (bool)child["IsUpToDate"]);
                                    }
                                }
                            }
                        }

                        IRestResponse responsePatch = await Kaseya.GetRequestAsync("api/v1.0/assetmgmt/patch/" + machine.Guid + "/machineupdate/false?$filter=SchedTogether gt 0");
                        if (responseSecurity.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responsePatch.Content);
                            patchesMissing = (int)result["TotalRecords"];
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }

                    Application.Current.Dispatcher.Invoke((Action)delegate {
                        model.ListMachineRM.Add(new MachineRM(machine, string.Join(", ", dAv.Keys), string.Join(", ", dAv.Values), patchesMissing, cAvProd, cAvStatus, cAvProdDbDate, cEpsMaintNote, cPatchCompliance));
                    });
                }).ToList();

                await Task.WhenAll(tasks);

                /*
                Console.WriteLine();
            });

            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate (object o, RunWorkerCompletedEventArgs args) {
                */
                //Resize the columns to fit what's displayed
                foreach (DataGridColumn c in dataGridRM.Columns) {
                    c.Width = 0;
                    c.Width = DataGridLength.Auto;
                }

                progressRefresh.IsIndeterminate = false;
                btnRmLoad.IsEnabled = true;
            //});

            //bw.RunWorkerAsync();
        }

        private void dataGridRM_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            MachineSelectionChangedFromTab();
        }

        private async void btnRCLogs_Click(object sender, RoutedEventArgs e)
        {
            btnRCLogsLoad.IsEnabled = false;
            progressRefresh.IsIndeterminate = true;

            List<AgentRCLog> listRCLog = new List<AgentRCLog>();

            List<Task> tasks = model.ListMachine.Select(async machine => {
                await _semaphore.WaitAsync();

                try
                {
                    IRestResponse responseLogs = await Kaseya.GetRequestAsync("api/v1.0/assetmgmt/logs/" + machine.Guid + "/remotecontrol?$orderby=LastActiveTime desc&$top=10");


                    if (responseLogs.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseLogs.Content);

                        if (result["Result"] != null)
                        {

                            foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children())
                            {
                                if (((string)child["Administrator"]).Contains(vsaUserName))
                                {
                                    listRCLog.Add(new AgentRCLog(machine.AgentNameOnly, child));
                                }
                            }
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                //Application.Current.Dispatcher.Invoke((Action)delegate {
                    //model.ListMachineRM.Add(new MachineRM(machine, string.Join(", ", dAv.Keys), string.Join(", ", dAv.Values), patchesMissing, cAvProd, cAvStatus, cAvProdDbDate, cEpsMaintNote, cPatchCompliance));
                //});
            }).ToList();

            await Task.WhenAll(tasks);

            

            listRCLog = listRCLog.OrderBy(x => x.StartTime).ToList();

            StringBuilder sb = new StringBuilder();
            foreach (AgentRCLog log in listRCLog)
            {
                sb.AppendLine(string.Format("{0},{1},{2}", log.IsFor, log.StartTime.ToString("s"), log.LastActiveTime.ToString("s")));
            }
            txtRCLogs.Text = sb.ToString();

            progressRefresh.IsIndeterminate = false;
            btnRCLogsLoad.IsEnabled = true;
        }

        private void btnRCLogsLoadAdv_Click(object sender, RoutedEventArgs e)
        {
            if (progressRefresh.IsIndeterminate)
                return;

            WindowRCLogs winRCLogs = new WindowRCLogs(vsaUserName, model.MachineGroups.ToList());
            winRCLogs.Owner = this;
            winRCLogs.Show();
        }

        private void btnColumns_Click(object sender, RoutedEventArgs e)
        {
            model.ShowColumnNetwork = !model.ShowColumnNetwork;
            model.ShowColumnExtras = !model.ShowColumnExtras;
        }

        /*
        private bool TreeViewIfExists(TreeViewItem itm, string header, ref TreeViewItem which) {
            if (itm.Header as string == header) {
                which = itm;
                return true;
            }
            foreach (TreeViewItem i in itm.Items) {
                if (i.Header as string == header) {
                    which = i;
                    return true;
                } else if (i.HasItems) {
                    if (TreeViewIfExists(i, header, ref which))
                        return true;
                }
            }
            return false;
        }
        */
    }
}