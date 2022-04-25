using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using KonceptHK.HeliosGluon.Utils;
using Noris.Kernel;
using Noris.LCS.Base;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    public class ZruseniKrizovehoOdkazuCowley : NrsCowley
    {
        private List<Tuple<int, int>> filteredRecords = new List<Tuple<int, int>>();
        protected override void Init(InitEventArgs e)
        {
            base.Init(e);
            if (this.RecordNumbers.Count() > 1) Message.Error("Funkci lze spustit jen nad jedním záznamem.");

            QueryTemplate qt = QueryManager.Instance.GetQuery("GetSouvisejiciDocsFD");
            qt.ReplaceParametr("fd", RecordNumbers[0]);
            Datastore ds = SqlSelect.CreateDatastore(qt.GetFinalQuery());
            int count = ds.Retrieve();

            if (count > 0)
                filteredRecords = GetFilteredRecords(ds);
            else
                Message.Info($"Pro fakturu {SqlSelect.GetReference(RecordNumbers[0])} neexistují žádné křížové odkazy.");
        }
        public void ZruseniKrizovehoOdkazu()
        {
            foreach (var item in filteredRecords)
            {
                try
                {
                    CallDokumentUprava(item.Item1, item.Item2);
                }
                catch (Exception e)
                {
                    Message.Warning($"Při rušení KO nad fakturou {SqlSelect.GetReference(RecordNumbers[0])} pro související dokument s ref {SqlSelect.GetReference(item.Item2, ClassNumbers.ESSSSouvisejiciDokument)} došlo k chybě: {e.Message}");
                    continue;
                }
            }               
        }
        private List<Tuple<int, int>> GetFilteredRecords(Datastore ds)
        {
            var pairs = new List<Tuple<int, int>>();
            for (int i = 0; i < ds.RowCount(); i++)
                pairs.Add(Tuple.Create(ds.GetItemInt32(i, "posta"), ds.GetItemInt32(i, "cislo_nonsubjektu")));

            OpenBrowseData obd = new OpenBrowseData(ClassNumbers.ESSSSouvisejiciDokument, FolderNumbers.ESSSSouvisejiciDokument);
            BigFilter bf = new BigFilter();
            bf.Reset(ClassNumbers.ESSSSouvisejiciDokument);
            bf.AddFilterExpression(0, "lcs.esss_souvisejici_dokument.cislo_nonsubjektu", $"lcs.esss_souvisejici_dokument.cislo_nonsubjektu IN ({String.Join(",", pairs.Select(x => x.Item2))})");
            obd.BigFilter = bf;
            obd.MultiSelect = true;
            obd.TitleText = "Vyberte související dokumenty pro zrušení KO";
            var selectedRecords = ModalWindows.OpenBrowse(obd);

            pairs.RemoveAll(x => !selectedRecords.Contains(x.Item2)); //Ponechám jen vybrané záznamy, ostatní smažu
            return pairs;
        }

        private void CallDokumentUprava(int posta, int souvisejiciDokument)
        {
            int cisloTridy = SqlSelect.GetClassNumber(posta);
            using (ESSSDokumentUpravaCwl cwl = (ESSSDokumentUpravaCwl)NrsCowley.GetCowley(cisloTridy, "DokumentUprava", SqlSelect.GetFolderNumber(cisloTridy, posta), true))
            {
                cwl.Initialize(posta, this);
                cwl.SetParams_OdpojitSouvisejici(souvisejiciDokument);
                cwl.ParamsOK = true;
                cwl.Run();
            }
        }
    }
}
