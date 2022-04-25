using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    /// <summary>
    /// Pomocna trida pro klice ve tride/poradaci ParametryKoncept
    /// </summary>
    class KonceptParams
    {
        //webova sluzba YOUR SYSTEM spisova sluzba 
        public const string YOUR_SYSTEM_SPISOVKA_URL_TO_WS = "spisova_sluzba.ys_spis.url.to.ws";
        public const string YOUR_SYSTEM_SPISOVKA_LOGIN_TO_WS = "spisova_sluzba.ys_spis.login.to.ws";
        public const string YOUR_SYSTEM_SPISOVKA_PASSWORD_TO_WS = "spisova_sluzba.ys_spis.password.to.ws";

        // TESTOVACI webova sluzba YOUR SYSTEM spisova sluzba 
        public const string TEST_YOUR_SYSTEM_SPISOVKA_URL_TO_WS = "spisova_sluzba.ys_spis.test.url.to.ws";
        public const string TEST_YOUR_SYSTEM_SPISOVKA_LOGIN_TO_WS = "spisova_sluzba.ys_spis.test.login.to.ws";
        public const string TEST_YOUR_SYSTEM_SPISOVKA_PASSWORD_TO_WS = "spisova_sluzba.ys_spis.test.password.to.ws";
    }
}
