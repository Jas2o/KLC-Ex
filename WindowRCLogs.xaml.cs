using LibKaseya;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KLCEx
{
    /// <summary>
    /// Interaction logic for WindowRCLogs.xaml
    /// </summary>
    public partial class WindowRCLogs : Window
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10);
        private string vsaUserName;
        private List<MachineGroup> listMachineGroups;
        private Dictionary<string, string> dMachineGuidName;

        public WindowRCLogs(string currentVsaUser, List<MachineGroup> machineGroups)
        {
            InitializeComponent();
            progressBar.Visibility = Visibility.Collapsed;

            vsaUserName = currentVsaUser;
            listMachineGroups = machineGroups;

            dMachineGuidName = new Dictionary<string, string>();
        }

        private void btnInputNext_Click(object sender, RoutedEventArgs e)
        {
            btnInputNext.IsEnabled = btnPreCheckContinue.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            progressBar.IsIndeterminate = true;

            //Get rid of spaces
            txtInputGroup.Text = txtInputGroup.Text.Replace(" ", "").ToLower();
            txtInputMachine.Text = txtInputMachine.Text.Replace(" ", "");

            string[] linesGroup = txtInputGroup.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string[] linesMachine = txtInputMachine.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            List<MachineGroup> listCheckGroup = new List<MachineGroup>();
            dMachineGuidName.Clear();

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(delegate (object o, DoWorkEventArgs args) {
                foreach (string line in linesGroup) {
                    if (line.Length == 0)
                        continue;

                    MachineGroup group = listMachineGroups.Find(x => x.GroupNameDisplay == line);
                    if (group != null && !listCheckGroup.Contains(group)) {
                        listCheckGroup.Add(group);

                        string root = group.GroupName.Replace(".root", "");
                        int records = 0;
                        int num = 0;
                        do {
                            string query = "api/v1.0/assetmgmt/agents";
                            if (group.GroupName.EndsWith(".root"))
                                query += "?$top=50&$filter=endswith(MachineGroup,%20'" + root + "')&$orderby=AgentName%20asc&$skip=" + num;
                            else
                                query += "?$top=50&$filter=(MachineGroupId%20eq%20" + group.GroupId + "M)&$orderby=AgentName%20asc&$skip=" + num;
                            IRestResponse response = Kaseya.GetRequest(query);

                            dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                            if (result["Status"] != "OK")
                                break;
                            records = (int)result["TotalRecords"];
                            foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                                Machine machine = new Machine(child);
                                if (!dMachineGuidName.ContainsKey(machine.Guid))
                                    dMachineGuidName.Add(machine.Guid, machine.AgentName);
                            }
                            num += 50;

                            if (num > 150)
                                break;
                        } while (num < records);
                    }
                }

                foreach (string line in linesMachine) {
                    if (line.Length == 0)
                        continue;

                    int records = 0;
                    int num = 0;
                    do {
                        string query = "api/v1.0/assetmgmt/agents?$top=50&$filter=substringof('" + line + "',%20ComputerName)&$orderby=AgentName%20asc&$skip=" + num;

                        IRestResponse response = Kaseya.GetRequest(query);

                        dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);
                        if (result["Status"] != "OK")
                            break;
                        records = (int)result["TotalRecords"];
                        foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                            Machine machine = new Machine(child);
                            if (!dMachineGuidName.ContainsKey(machine.Guid))
                                dMachineGuidName.Add(machine.Guid, machine.AgentName);
                        }
                        num += 50;

                        if (num > 150)
                            break;
                    } while (num < records);
                }
            });

            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate (object o, RunWorkerCompletedEventArgs args) {
                btnPreCheckContinue.Content = "Continue check on " + dMachineGuidName.Count + " machines";

                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                btnInputNext.IsEnabled = btnPreCheckContinue.IsEnabled = true;

                expanderInput.IsExpanded = false;
                expanderPreCheck.IsExpanded = true;
                expanderOutput.IsExpanded = false;
            });

            bw.RunWorkerAsync();
        }

        private async void btnPreCheckContinue_Click(object sender, RoutedEventArgs e)
        {
            btnInputNext.IsEnabled = btnPreCheckContinue.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            progressBar.IsIndeterminate = true;

            List<AgentRCLog> listRCLog = new List<AgentRCLog>();

            List<Task> tasks = dMachineGuidName.Select(async machine => {
                await _semaphore.WaitAsync();

                try {
                    IRestResponse responseLogs = await Kaseya.GetRequestAsync("api/v1.0/assetmgmt/logs/" + machine.Key + "/remotecontrol?$orderby=LastActiveTime desc&$top=10");

                    if (responseLogs.StatusCode == System.Net.HttpStatusCode.OK) {
                        dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(responseLogs.Content);
                        if (result["Result"] != null) {
                            foreach (Newtonsoft.Json.Linq.JObject child in result["Result"].Children()) {
                                if (((string)child["Administrator"]).Contains(vsaUserName)) {
                                    listRCLog.Add(new AgentRCLog(machine.Value, child));
                                }
                            }
                        }
                    }
                } finally {
                    _semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);

            listRCLog = listRCLog.OrderBy(x => x.StartTime).ToList();

            StringBuilder sb = new StringBuilder();
            foreach (AgentRCLog log in listRCLog) {
                sb.AppendLine(string.Format("{0},{1},{2}", log.IsFor, log.StartTime.ToString("s"), log.LastActiveTime.ToString("s")));
            }
            txtOutput.Text = sb.ToString();

            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            btnInputNext.IsEnabled = btnPreCheckContinue.IsEnabled = true;

            expanderInput.IsExpanded = false;
            expanderPreCheck.IsExpanded = false;
            expanderOutput.IsExpanded = true;
        }
    }
}
