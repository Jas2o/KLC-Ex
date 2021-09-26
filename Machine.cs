using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLCEx {
    public class Machine {

        public enum AgentStatus {
            Down = 0,
            Online = 1,
            UserIdle = 2,
            UserActive = 3 //This does not exist in Kaseya's status
        }

        public enum OSProfile {
            Other, Server, Mac
        }

        public string Guid { get; private set; }
        public int Status { get; private set; }
        public AgentStatus DisplayStatus { get; private set; }
        public string OSType { get; private set; }
        public OSProfile OSTypeProfile { get; private set; }

        public string AgentName { get; private set; }
        public string ComputerName { get; private set; }
        public string UserCurrent { get; private set; }
        public string UserLast { get; private set; }
        public string DisplayUser { get; private set; }
        public DateTime CheckInLast { get; private set; }
        public bool CheckInLastMonth { get; private set; }
        public DateTime RebootLast { get; private set; }
        public bool RebootLastWeek { get; private set; }
        public string AgentVersion { get; private set; }

        public string AgentNameOnly { get; private set; }
        public string MachineGroup { get; private set; }
        public string MachineGroupReverse { get; private set; }
        public string DomainWorkgroup { get; private set; }

        public Machine(JObject child) {
            Guid = (string)child["AgentId"];
            Status = (int)child["Online"];
            OSType = (string)child["OSType"];
            if (OSType == "Mac OS X")
                OSTypeProfile = OSProfile.Mac;
            else if (OSType.StartsWith("20"))
                OSTypeProfile = OSProfile.Server;
            else
                OSTypeProfile = OSProfile.Other;

            AgentName = (string)child["AgentName"];
            ComputerName = (string)child["ComputerName"];
            UserCurrent = (string)child["CurrentUser"];
            UserLast = (string)child["LastLoggedInUser"];
            if (child["LastCheckInTime"] != null && child["LastCheckInTime"].Type != JTokenType.Null) {
                CheckInLast = (DateTime)child["LastCheckInTime"];
                if ((DateTime.Now - CheckInLast).TotalDays < 32)
                    CheckInLastMonth = true;
            }
            if (child["LastRebootTime"] != null && child["LastRebootTime"].Type != JTokenType.Null) {
                RebootLast = (DateTime)child["LastRebootTime"];
                if ((DateTime.Now - RebootLast).TotalDays < 8)
                    RebootLastWeek = true;
            }
            AgentVersion = (string)child["AgentVersion"];

            AgentNameOnly = AgentName.Split('.')[0];
            MachineGroup = (string)child["MachineGroup"];
            MachineGroupReverse = string.Join(".", MachineGroup.Split('.').Reverse());
            DomainWorkgroup = (string)child["DomainWorkgroup"];

            //txtMachines.AppendText(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\r\n", agentStatus, agentOS, agentName, agentUser, agentCheckInLast, agentRebootLast, agentVersion));

            DisplayUser = (UserCurrent == "" ? UserLast : UserCurrent);
            DisplayStatus = (AgentStatus)Status;
            if(Status == 1 && UserCurrent != "")
                DisplayStatus = AgentStatus.UserActive;
        }

    }
}
