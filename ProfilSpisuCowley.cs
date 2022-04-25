using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    public class ProfilSpisuCowley : NrsCowley
    {
        private Int32Array spisy;

        public void ProfilSpisu()
        {
            spisy = new Int32Array();

            foreach (var record in this.RecordNumbers)
            {
                var spis = this.DohledejSpisDleSkody(record);

                if(spis > 0) spisy.Add(spis);
                else
                {
                    Message.Warning($"Spis pro škodu {SqlSelect.GetReference(record)} neby nalezen");
                }
            }
        }

        protected override void Done(DoneEventArgs e)
        {
            foreach(var spis in spisy)
            {
                using (LCS.Base.ZobrazSpisZESSSCwl cwl = NrsCowley.GetCowley(ClassNumbers.SPIS, "ZobrazSpisZESSS", SqlSelect.GetFolderNumber(spis), false) as LCS.Base.ZobrazSpisZESSSCwl)
                {
                    cwl.Initialize(spis, this);

                    cwl.RunDialogDesktop();
                }
            }
        }

        private Int32 DohledejSpisDleSkody(int record)
        {
            QueryTemplate query = QueryManager.Instance.GetQuery("DohledejSpisPodleSkody");
            query.ReplaceParametr("skoda", record);
            return SqlSelect.GetInt32(query.GetFinalQuery(), 0);
        }
    }
}
