using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    public class ZpracovaniBankovnihoVypisuCowley : KonceptHK.KHKBaseCowley
    {
        private Int32Array typDokumentuBV;

        protected override void Init(InitEventArgs e)
        {
            base.Init(e);

            var recs = SqlSelect.GetRecordsByReference("Bankovní výpis", 2628);
            if (recs != null && recs.Count > 0)
            {
                this.typDokumentuBV = new Int32Array();
                foreach (var rec in recs) this.typDokumentuBV.Add(rec.RecordNumber);
            }
        }

        public void ZpracovaniBankovnihoVypisu()
        {
            this.ProcessWithTransaction(Process);
        }

        private void Process(INrsInstance instance)
        {
            if(this.Validace(instance))
            {
                var prilohy = this.NactiPrilohyZpravy(instance.RecordNumber);
                if(prilohy != null && prilohy.Rows.Count > 0)
                {
                    for (int i = 0; i < prilohy.Rows.Count; i++)
                    {
                        var nazev = prilohy.GetItemString(i, 1).ToLower();

                        if (nazev.StartsWith("Vypis") && nazev.EndsWith(".pdf"))
                        {

                        }
                    }
                }
            }
            else
            {
                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Záznam bude přeskočen");
            }
        }

        private Datastore NactiPrilohyZpravy(int recordNumber)
        {
            return SqlSelect.RetrieveDatastore($"SELECT cislo_nonsubjektu, document_name FROM lcs.mail_attachment WHERE message = {recordNumber}");
        }

        private bool Validace(INrsInstance instance)
        {
            bool result = true;

            if(!this.typDokumentuBV.Contains(instance.Master.GetItemInt32(0, "esss_typ_dokumentu", -1)))
            {
                result = false;
                Message.Warning($"Došlá pošta č. {instance.Reference} není typu bankovní výpis");
            }

            return result;
        }
    }
}
