using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLCEx
{
    class AgentRCLog
    {

        public string IsFor { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime LastActiveTime { get; private set; }
        public string SessionType { get; private set; }
        public string Administrator { get; private set; }

        public AgentRCLog(string isFor, JObject child)
        {
            IsFor = isFor;
            StartTime = (DateTime)child["StartTime"];
            LastActiveTime = (DateTime)child["LastActiveTime"];
            SessionType = child["SessionType"].ToString(); //Number
            Administrator = child["Administrator"].ToString();
        }

    }
}
