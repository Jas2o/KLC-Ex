using Newtonsoft.Json.Linq;

namespace KLC_Ex {
    public class MachineGroup {

        public string GroupId { get; private set; }
        public string GroupName { get; private set; }
        public string ParentId { get; private set; }
        public string OrgId { get; private set; }

        public string GroupNameDisplay { get; private set; }

        public MachineGroup(JObject child) {
            GroupId = child["MachineGroupId"].ToString();
            GroupName = child["MachineGroupName"].ToString();
            ParentId = child["ParentMachineGroupId"].ToString();
            OrgId = child["OrgId"].ToString();

            if (GroupName.EndsWith(".root"))
                GroupNameDisplay = GroupName.Substring(0, GroupName.LastIndexOf(".root"));
            else
                GroupNameDisplay = GroupName;
            //Attributes
        }

        public MachineGroup(string id, string name, string parent, string orgid) {
            GroupId = id;
            GroupName = name;
            ParentId = parent;
            OrgId = orgid;

            if (GroupName.EndsWith(".root"))
                GroupNameDisplay = GroupName.Substring(0, GroupName.LastIndexOf(".root"));
            else
                GroupNameDisplay = GroupName;
        }

    }
}
