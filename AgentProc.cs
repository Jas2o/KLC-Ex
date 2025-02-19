using Newtonsoft.Json.Linq;

namespace KLC_Ex {
    public class AgentProc {

        public string AgentProcedureId { get; private set; }
        public string AgentProcedureName { get; private set; }
        public string Path { get; private set; }
        public string Description { get; private set; } //Practically useless, user created don't seem to have
        //public string Attributes { get; private set; }

        public string DisplayPath { get; private set; }

        public AgentProc(JObject child) {
            AgentProcedureId = child["AgentProcedureId"].ToString(); //Number
            AgentProcedureName = child["AgentProcedureName"].ToString();
            Path = child["Path"].ToString();
            Description = child["Description"].ToString();
            //Attributes = child["Attributes"].ToString(); //Could be null, appears to be unused

            if(Path.StartsWith("Private/")) {
                //Path = Path.Replace("company/com/au/", "company.com.au_");
                int slash = Path.IndexOf('/', 8);
                if (slash == -1)
                    Path = "Private";
                else
                    Path = "Private" + Path.Substring(slash);
            }

            DisplayPath = Path.Replace("/", " / ");
        }
    }
}
