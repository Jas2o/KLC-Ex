using Newtonsoft.Json.Linq;

namespace KLC_Ex {
    public class VSAView {

        public string ViewId { get; private set; }
        public string ViewName { get; private set; }

        public VSAView(JObject child) {
            ViewId = child["ViewDefId"].ToString();
            ViewName = child["ViewDefName"].ToString();
        }

        public VSAView(string Id, string Name) {
            ViewId = Id;
            ViewName = Name;
        }

    }
}
