using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLCEx {
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
