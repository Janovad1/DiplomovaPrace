using KonceptHK.HeliosGluon.Queries;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class AuditniStopaDoEPDCowley : NrsCowley
    {
        public void AuditniStopaDoEPD()
        {
            foreach (Int32 record in this.RecordNumbers)
            {
                using (INrsInstance instance = NrsInstance.GetInstance(this.ClassNumber))
                {
                    instance.Retrieve(record);

                    this.Process(instance);
                }
            }
        }

        protected void Process(INrsInstance instance)
        {
            try
            {
                DbTransaction.Current.Begin();

                this.ProcessDocuments(instance);

                DbTransaction.Current.SetComplete();
                DbTransaction.Current.End();
            }
            catch (Exception ex)
            {
                if (DbTransaction.IsTransaction)
                    DbTransaction.Current.End();

                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Chyba při zpracování záznamu: " + ex.Message);
            }
        }

        private void ProcessDocuments(INrsInstance instance)
        {
            var dokumentyAS = this.DohledejDokumentyAS(instance.RecordNumber);
            //String zkratka = this.DohledejZkratku(instance.RecordNumber);
            var rowCount = dokumentyAS.Retrieve();
            if (dokumentyAS != null && rowCount > 0)
            {
                if (KonceptHK.Service.BaseService.DebugModeEnabled())
                    Message.InfoWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, $"Nalezeno {rowCount} záznamů");
                for (int i = 0; i < rowCount; i++)
                {
                    int cs = dokumentyAS.GetItemInt32(i, 0);
                    string typ = dokumentyAS.GetItemString(i, 1);
                    string zkratka = dokumentyAS.GetItemString(i, 2);

                    using (INrsInstance edmInst = NrsInstance.GetInstance(1132))
                    {
                        edmInst.Retrieve(cs);
                        var date = edmInst.Master.GetItemDateTime(0, "created");
                        string nazev = "AS_" + date.ToString("yyyyMMdd_HHmmss") + "_" + typ + "_" + zkratka + "_" + instance.Master.GetItemString(0, "esss_carovy_kod") + ".PDF";
                        edmInst.Name = nazev;
                        edmInst.Master.SetItem(0, "physical_name", nazev);
                        edmInst.MasterRelations.AddRelation(105627, instance.RecordNumber, RelationSide.Left);
                        edmInst.Update();
                    }
                }

                using (INrsCowley zalozDocCowley = NrsCowley.GetCowley(instance.ClassNumber, "ZalozPrilohuDoESSS", instance.FolderNumber, true))
                {
                    zalozDocCowley.Initialize(instance);
                    zalozDocCowley.ParamsOK = true;
                    zalozDocCowley.Run();
                }
            }
            else
            {
                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, $"K danému záznamu nebyly nalezeny žádné dokumenty");
            }
        }

        private string DohledejZkratku(int recordNumber)
        {
            QueryTemplate query = QueryManager.Instance.GetQuery("DohledejZkratkuSchv");
            query.ReplaceParametr("posta", recordNumber);
            return SqlSelect.GetString(query.GetFinalQuery());
        }

        private Datastore DohledejDokumentyAS(int recordNumber)
        {
            QueryTemplate query = QueryManager.Instance.GetQuery("DohledejDokumentyAS");
            query.ReplaceParametr("posta", recordNumber);
            var ds = SqlSelect.CreateDatastore(query.GetFinalQuery());
            return ds;
        }
    }
}
