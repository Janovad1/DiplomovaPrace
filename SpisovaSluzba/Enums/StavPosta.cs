using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba.Enums
{
    /// <summary>
    /// Hodnoty vyctu vyjadruji stav zpravy
    /// </summary>
    enum StavPosta : Int32
    {
        PRIPRAVENO_K_ODELSANI = 1,
        ODESLANO = 2,
        NEPRIPRAVENO_k_ODESLANI = 3,
        ZALOZENO = 4,
        PREDANO_DO_VYPRAVNY = 5,
        STORNOVANO = 6,
        ZALOZENO_ZNOVUVYPRAVENI = 7,
        VYRIZENO = 8,
        ZALOZENO_VYPRAVENI = 9,
        UZAVRENO = 10,
        VRACENO_DO_ESSS = 11,
        CHYBA_ODESLANI = 12
    }
}
