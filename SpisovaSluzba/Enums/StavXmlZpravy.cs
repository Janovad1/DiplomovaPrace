using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba.Enums
{
    /// <summary>
    /// Hodnoty vyctu vyjadruji editacni styl pro stav na objektu tridy VYSKYT XML ZPRAV
    /// Kopie z Noris.KonceptHK.Myto43.Enums.StavXmlZpravy (v budoucnu dát např do projektu KonceptHk)
    /// </summary>
    enum StavXmlZpravy : Int32
    {
        ZAVEDENO = 0,                //Zavedeno = 0,
        ZPRACOVANO_ZAKLAD = 1,       //Zpracováno základ = 1,
        ZPRACOVANO_APLIKACI = 2,     //Zpracováno aplikací = 2,
        ODMITNUTO_VECNE = 3,         //Odmítnuto věcně = 3,
        ODMITNUTO_NESROZUMITELNE = 4,//Odmítnuto nesrozumitelné = 4
        CHYBA = 9                    //stav z RSD
    }
}
