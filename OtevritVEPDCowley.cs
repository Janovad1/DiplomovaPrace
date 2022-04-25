using Noris.LCS.Helios.Common;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class OtevritVEPDCowley : NrsCowley
    {
       public void OtevritVEPD()
       {
            GetUrlAndBarCode(out string url, out string barCode);
            try
            {
                Srv.IOTunnel.Process.Start(url + barCode);
            }
            catch (Exception e)
            {
                Message.Error($"Nepodařilo se otevřit url v EPD. Chyba: {e}");
            }         
       }

        private void GetUrlAndBarCode(out string url, out string barCode)
        {
            url = null;
            barCode = null;

            if (CustomConfig.KeyExists("SpisovaSluzba", "ESSS_URL"))
                url = CustomConfig.GetStringValue("SpisovaSluzba", "ESSS_URL");
            else
                Message.Error("Nepodařilo se nalézt url v zakázkové konfiguraci.");

            using (INrsInstance inst = NrsInstance.GetInstance(this.ClassNumber))
            {
                inst.Retrieve(RecordNumbers[0]);
                barCode = inst.Master.GetItemString(0, "esss_carovy_kod");
            }

            if (String.IsNullOrEmpty(barCode))
                Message.Error("Na instanci záznamu není vyplněn čárový kód.");
            if (String.IsNullOrEmpty(url))
                Message.Error("V zakázkové konfiguraci není vyplněna url adresa.");
        }
    }
}
