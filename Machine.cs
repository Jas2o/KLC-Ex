using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Ex {
    public class Machine {

        public enum AgentStatus {
            Down = 0,
            Online = 1,
            UserActive = 11,
            UserIdle = 12
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
        public DateTime CheckInFirst { get; private set; }
        public DateTime CheckInLast { get; private set; }
        public bool CheckInLastMonth { get; private set; }
        public DateTime RebootLast { get; private set; }
        public bool RebootLastWeek { get; private set; }
        public string AgentVersion { get; private set; }

        public string AgentNameOnly { get; private set; }
        public string MachineGroup { get; private set; }
        public string MachineGroupReverse { get; private set; }
        public string DomainWorkgroup { get; private set; }

        public bool OneClickAccess { get; private set; }
        public string SystemSerialNumber { get; private set; }
        public string IPAddress { get; private set; }
        public string DefaultGateway { get; private set; }
        public string DNSServer1 { get; private set; }
        public string DNSServer2 { get; private set; }
        public string DHCPServer { get; private set; }
        public string ConnectionGatewayIP { get; private set; }
        public string MacAddr { get; private set; }

        public int MachineShowToolTip { get; private set; }
        public string MachineToolTipBadge { get; private set; }
        public string MachineNote { get; private set; }
        public string MachineNoteLink { get; private set; }

        public enum Badge
        {
            Blank,
            Note,
            FlagRed,
            FlagBlue,
            FlagGreen,
            FlagYellow,
            Recycle,
            Clock,
            Location,
            StarYellow,
            StarGreen,
            StarBlue,
            StarRed,
            UsePrivate,
            MagnifyGlass,
            PhoneOrange,
            PhoneBlue,
            Documentation,
            FilingCabinetBlue,
            UnknownArrowGreen,
            Envelope,
            PencilOrange,
            PencilBlue,
            SpeechBubble,
            PersonYellow
        };

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
            if (child["FirstCheckIn"] != null && child["FirstCheckIn"].Type != JTokenType.Null)
            {
                CheckInFirst = (DateTime)child["FirstCheckIn"];
            }
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

            //Added 2022-03-04
            OneClickAccess = (bool)child["OneClickAccess"];
            if (OneClickAccess)
            {
                if (OSType == "Mac OS X" || DomainWorkgroup.Contains("(dc)"))
                    OneClickAccess = false;
            }
            SystemSerialNumber = (string)child["SystemSerialNumber"];
            IPAddress = (string)child["IPAddress"];
            DefaultGateway = (string)child["DefaultGateway"];
            DNSServer1 = (string)child["DNSServer1"];
            DNSServer2 = (string)child["DNSServer2"];
            DHCPServer = (string)child["DHCPServer"];
            ConnectionGatewayIP = (string)child["ConnectionGatewayIP"];
            MacAddr = (string)child["MacAddr"];

            //txtMachines.AppendText(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\r\n", agentStatus, agentOS, agentName, agentUser, agentCheckInLast, agentRebootLast, agentVersion));

            DisplayUser = (UserCurrent == "" ? UserLast : UserCurrent);
            DisplayStatus = (AgentStatus)Status;
            //if(Status == 1 && UserCurrent != "") //No longer required
                //DisplayStatus = AgentStatus.UserActive;

            if (child["ShowToolTip"].ToString().Length > 0)
                MachineShowToolTip = (int)child["ShowToolTip"];
            MachineNote = (string)child["ToolTipNotes"];

            if (MachineNote == null)
                MachineNote = "";
            else
            {
                if (MachineNote.Trim() == "")
                {
                    MachineShowToolTip = 0;
                }
                else
                {
                    MachineNote = MachineNote.Trim().Replace("&nbsp;", " ");
                    string[] links = MachineNote.Split("\t\n ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(s => s.StartsWith("http://") || s.StartsWith("https://") || s.StartsWith("www.")).ToArray();
                    if (links != null && links.Length > 0)
                    {
                        MachineNoteLink = links[0];
                        MachineNote = MachineNote.Replace(MachineNoteLink, "").Trim();
                    }
                }
            }

            MachineToolTipBadge = (MachineShowToolTip > 0 ? Enum.GetName(typeof(Badge), MachineShowToolTip) : "");
        }

    }
}
