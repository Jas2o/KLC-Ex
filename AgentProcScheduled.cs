using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLCEx {
    public class AgentProcScheduled {

        public string AgentProcedureId { get; private set; }
        public string AgentProcedureName { get; private set; }
        public string NextExecTime { get; private set; }
        public string Status { get; private set; }
        public string Admin { get; private set; }
        public string Recurrence { get; private set; }
        public string Attributes { get; private set; }

        public AgentProcScheduled(JObject child) {
            AgentProcedureId = child["AgentProcedureId"].ToString(); //Number
            AgentProcedureName = child["AgentProcedureName"].ToString();
            NextExecTime = child["NextExecTime"].ToString();
            Status = child["Status"].ToString();
            Admin = child["Admin"].ToString();
            Recurrence = child["Recurrence"].ToString();
            Attributes = child["Attributes"].ToString(); //Could be null
        }
    }
}
