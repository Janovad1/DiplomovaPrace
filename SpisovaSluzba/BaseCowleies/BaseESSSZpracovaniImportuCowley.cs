using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.KonceptHK.SpisovaSluzba.Enums;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace Noris.KonceptHK.SpisovaSluzba.BaseCowleies
{
    public abstract class BaseESSSZpracovaniImportuCowley : BaseESSSCowley
    {       

        /// <summary>
        /// Metoda provede samotne zpracovani XML
        /// </summary>
        protected abstract void Zpracovani(INrsInstance vyskytXmlZpravy);


        /// <summary>
        /// Metoda ostestuje, jestli je mozne poustet zpracovani nad objektem VyskytXmlZpravy.
        /// Pokud je objekt ve stavu Zpracovani, nebo pokud nejaka smlouva na neho odkazuje, vyhodi se chyba.
        /// </summary>
        protected void KontrolaPripustnostiZpracovani(INrsInstance vyskytXmlZpravy)
        {
            Int32 stavZpracovani = vyskytXmlZpravy.Master.GetItemInt32(0, "stav");
            if (stavZpracovani == (Int32)StavXmlZpravy.ZPRACOVANO_APLIKACI)
            {
                throw new Exception("Nelze zpracovat již zpracovanou XML zprávu. Stav XML zprávy je Zpracováno aplikací.");
            }
        }
    }
}