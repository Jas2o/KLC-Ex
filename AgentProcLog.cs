﻿using Newtonsoft.Json.Linq;

namespace KLC_Ex {
    public class AgentProcLog {

        public string LastExecution { get; private set; }
        public string ProcedureHistory { get; private set; }
        public string Status { get; private set; }
        public string Admin { get; private set; }

        public AgentProcLog(JObject child) {
            LastExecution = child["LastExecution"].ToString(); //Number
            ProcedureHistory = child["ProcedureHistory"].ToString();
            Status = child["Status"].ToString();
            Admin = child["Admin"].ToString();
        }
    }
}
