using KonceptHK.HeliosGluon;
using Noris.KonceptHK.KonceptHK;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    internal class ZobrazSpisVEPDCowley : KHKBaseCowley
    {
        private const Int32 DOSLA_POSTA_RELATION = 2301077;
        public void ZobrazeniSpisuVEPD()
        {
            foreach (var record in RecordNumbers)
                this.Process(record);
        }

        private void Process(int record)
        {
            SqlSelect.GetRecordsForRelation(DOSLA_POSTA_RELATION, record, RelationSide.Right, out Int32Array dp);

            if (dp.Count == 1)
            {
                var spis = SqlSelect.GetAttribute<Int32?>(ClassNumbers.DOSLA_POSTA, dp[0], "spis", Repo.Classes.AttributeTypes.Master);
                if (spis.HasValue)
                    Open(spis.Value);
                else
                    Message.Error("Došlá pošta neobsahuje spis.");
            }
            else
                Message.Error("V dynamickém vztahu není navázaná došla pošta nebo počet navázaných záznamů je více jak 1.");
        }

        private void Open(int spis)
        {
            using (INrsCowley cowley = NrsCowley.GetCowley(ClassNumbers.SPIS, "OtevritVEPD", FolderNumbers.SPIS, true))
            {
                cowley.Initialize(spis, this);
                cowley.ParamsOK = true;
                cowley.Run();
            }
        }
    }
}
