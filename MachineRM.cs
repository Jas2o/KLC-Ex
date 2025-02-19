namespace KLC_Ex {
    public class MachineRM {

        public Machine Machine { get; private set; }

        //API
        public string avProducts { get; private set; }
        public string avProductsUpToDate { get; private set; }
        public int PatchesMissing { get; private set; }
        public bool PatchesMissingFlag { get { return PatchesMissing > 0; } }

        //Custom Fields
        public string C_AvProd { get; }
        public string C_AvStatus { get; }
        public string C_AvProdDbDate { get; }
        public string C_EpsMaintNote { get; }
        public string C_PatchCompliance { get; }

        public MachineRM(Machine m, string avProd, string avProdUTD, int patchesMissing, string cAvProd, string cAvStatus, string cAvProdDbDate, string cEpsMaintNote, string cPatchCompliance) {
            Machine = m;
            avProducts = avProd;
            avProductsUpToDate = avProdUTD;
            PatchesMissing = patchesMissing;

            C_AvProd = cAvProd;
            C_AvStatus = cAvStatus;
            C_AvProdDbDate = cAvProdDbDate;
            C_EpsMaintNote = cEpsMaintNote;
            C_PatchCompliance = cPatchCompliance;
        }
    }
}
