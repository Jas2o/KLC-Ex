using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLCEx {
    public class Machine {

        public string Guid { get; private set; }
        public int Status { get; private set; }
        public string OS { get; private set; }
        public string Name { get; private set; }
        public string UserCurrent { get; private set; }
        public string UserLast { get; private set; }
        public string User { get; private set; }
        public string CheckInLast { get; private set; }
        public string RebootLast { get; private set; }
        public string AgentVersion { get; private set; }

        //public string MachineGroup { get; private set; }
        public string DomainWorkgroup { get; private set; }

        public Machine(JObject child) {
            Guid = (string)child["AgentId"];
            Status = (int)child["Online"];
            OS = (string)child["OSType"];
            Name = (string)child["AgentName"];
            UserCurrent = (string)child["CurrentUser"];
            UserLast = (string)child["LastLoggedInUser"];
            User = (UserCurrent == "" ? "-" + UserLast + "-" : UserCurrent);
            CheckInLast = (string)child["LastCheckInTime"];
            RebootLast = (string)child["LastRebootTime"];
            AgentVersion = (string)child["AgentVersion"];

            //MachineGroup = (string)child["MachineGroup"];
            DomainWorkgroup = (string)child["DomainWorkgroup"];

            //txtMachines.AppendText(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\r\n", agentStatus, agentOS, agentName, agentUser, agentCheckInLast, agentRebootLast, agentVersion));
        }

    }
}
