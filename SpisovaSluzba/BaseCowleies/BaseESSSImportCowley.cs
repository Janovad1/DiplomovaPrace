using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using KonceptHK.HeliosGluon.Utils;
using Noris.KonceptHK.SpisovaSluzba.Enums;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba.BaseCowleies
{
    public abstract class BaseESSSImportCowley : BaseESSSCowley
    {
        public BaseESSSImportCowley()
        {

        }

        /// <summary>
        /// Metoda provede zalogovani prichoziho XML
        /// neresi transakci
        /// </summary>
        /// <param name="serviceGateInputUserData"></param>
        protected INrsInstance ZalogovatXMLZpravu(ServiceGateFunctionUserData serviceGateInputUserData, Int32 folderNumber, Int32 typeImport)
        {
            //jednotne datum pro cele provadeni metody
            DateTime now = DateTime.Now;

            //provede se zalogovani vstupni zpravy, at je blbe ci nikoliv
            INrsInstance vyskytXmlZpravy = NrsInstance.GetInstance(ClassNumbers.VYSKYT_XML_ZPRAVY, folderNumber);
            vyskytXmlZpravy.Reset();
            vyskytXmlZpravy.Master.SetItem(0, "stav", (Int32)StavXmlZpravy.ZAVEDENO);
            vyskytXmlZpravy.Master.SetItem(0, "datum_prijeti", now);
            vyskytXmlZpravy.Master.SetItem(0, "lcs_uda_xml_vyskyt_datum_zpravy", now);
            vyskytXmlZpravy.Master.SetItem(0, "smer", "1"); //1=prichozi, 2=odchozi
            vyskytXmlZpravy.Master.SetItem(0, "text", serviceGateInputUserData.GetXml());
            vyskytXmlZpravy.Master.SetItem(0, "xml_typ", this.GetTypXmlZpravyESSS((TypeImportESSS)typeImport));
            
            vyskytXmlZpravy.Update();

            Int32 idXmlZpravy = vyskytXmlZpravy.Master.GetItemInt32(0, "cislo_subjektu");

            //Zalogovani s kontextem ze se XML zprava ulozila
            Message.InfoWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, folderNumber, idXmlZpravy, "XML zpráva z ESSS uložena.");

            return vyskytXmlZpravy;

        }

        /// <summary>
        /// Interni metoda pro dotazeni objektu Typ zpravy, kde se uvadi XSL transformace.
        /// </summary>
        /// <returns>Vraci NULL pokud se typ zpravy nenalezne, jinak Int32 identifikator do vlastnosti.</returns>
        private Int32? GetTypXmlZpravyESSS(TypeImportESSS typeImport)
        {
            try
            {
                String typ = String.Empty;
                switch (typeImport)
                {
                    case TypeImportESSS.IMPORT_FAD:
                        typ = "ImportFaDEsss";
                        break;
                    case TypeImportESSS.IMPORT_VRACENI_FAV:
                        typ = "ImportVraceniFaVEsss";
                        break;
                }

                QueryTemplate queryT = QueryManager.Instance.GetQuery("GetTypXmlZpravyESSS");
                queryT.ReplaceParametr("typ_zpracovani", typ);
                Datastore result = SqlSelect.CreateDatastore(queryT.GetFinalQuery());
                if (result.Retrieve() == 1)
                {
                    return result.GetItemInt32(0, "id");
                }
            }
            catch
            {
                Message.Warning("Chyba vyhledávání typu XML zprávy pro Import ESSS.");
            }

            return null;
        }

        /// <summary>
        /// Metoda vytvari XML fragment s odpoved do ESSS
        /// </summary>
        protected abstract void CreateResultForESSS(Int32 idXmlZpravy);
    }
}
