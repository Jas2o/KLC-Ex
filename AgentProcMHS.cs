using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Ex {
    public class AgentProcMHS {

        public Machine Machine { get; private set; }
        public AgentProcHistory History { get; private set; }
        public AgentProcScheduled Schedule { get; private set; }

        public AgentProcMHS(Machine m, AgentProcHistory h, AgentProcScheduled s) {
            Machine = m;
            History = h;
            Schedule = s;
        }
    }
}
