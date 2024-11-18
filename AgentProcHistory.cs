using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Ex {
    public class AgentProcHistory {

        public string ScriptName { get; private set; }
        public string LastExecutionTime { get; private set; }
        public string Status { get; private set; }
        public bool StatusFailed { get; private set; }
        public string Admin { get; private set; }
        public string Attributes { get; private set; }

        public AgentProcHistory(JObject child) {
            ScriptName = child["ScriptName"].ToString();
            LastExecutionTime = child["LastExecutionTime"].ToString();
            Status = child["Status"].ToString();
            Admin = child["Admin"].ToString();
            Attributes = child["Attributes"].ToString(); //Could be null

            if (Status.StartsWith("Failed"))
                StatusFailed = true;
        }
    }
}
