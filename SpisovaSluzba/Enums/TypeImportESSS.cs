using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba.Enums
{
    /// <summary>
    /// Hodnoty vyctu vyjadruji typ importu ze Symbasis (Rezervace -> Import Smlouvy -> Storno)
    /// </summary>
    enum TypeImportESSS : Int32
    {
        IMPORT_FAD = 0,
        IMPORT_VRACENI_FAV = 1, 
    }
}
