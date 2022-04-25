using KonceptHK.HeliosGluon.Queries;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.LCS.Base;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class VraceniFDDoSpisovkyCowley : NrsCowley
    {
        private INrsInstance inst;
        private (string name, string vec, int? spis) DP;
        private Int32Array dvDP;
        private Boolean error = false;
        protected override void Init(InitEventArgs e)
        {
            if (RecordNumbers.Count() > 1) Message.Error("Funkci lze spustit jen nad jedním záznamem.");
            inst = this.Instance;
            if (inst == null && this.RecordNumbers.Count == 1)
            {
                inst = NrsInstance.GetInstance(this.ClassNumber);
                inst.Retrieve(this.RecordNumbers[0]);
            }           
        }

        public void VraceniFDSpisovka()
        {
            if (inst.Master.GetItemInt32(0, "stav") == 1) 
                Message.Error("Faktura je ve stavu 'zaúčtovaná' nelze pokračovat.");
            inst.MasterRelations.GetRelations(1399, RelationSide.Left, out dvDP);
            if (dvDP.Count() != 0)
            {
                using (INrsInstance doslaPosta = NrsInstance.GetInstance(88))
                {
                    doslaPosta.Retrieve(dvDP.First());                    
                    DP = (name: doslaPosta.Master.GetItemString(0, "nazev_subjektu"), vec: doslaPosta.Master.GetItemString(0, "vec"), spis: doslaPosta.Master.GetItem<Int32?>(0, "spis"));
                    doslaPosta.Master.SetItem(0, "esss_typ_dokumentu", 44); //Nevyplněno    
                    doslaPosta.Master.SetItem(0, "poznamka", "Nejedná se o fakturu došlou.");
                    doslaPosta.Update();

                    //record útvaru přihlášeného uživatele
                    QueryTemplate qt = QueryManager.Instance.GetQuery("GetDepartmentRecordByUser");
                    qt.ReplaceParametr("user", gCache.GetUserNumber());
                    Int32 recordUtvar = SqlSelect.GetInt32(qt.GetFinalQuery());
                    try
                    {
                        //Zkontroluje vlastníka a popř udělá změnu
                        ESSSUtils.SetOwnerOfDoc(doslaPosta.RecordNumber, recordUtvar, gCache.GetUserNumber());
                    }
                    catch (Exception e)
                    {
                        Message.Error($"Při kontrole vlastníka nastala chyba: {e}");
                        error = true;
                    }
                    
                }               
            }
            else Message.Error("Faktura neobsahuje došlou poštu.");
        }

        protected override void Done(DoneEventArgs e)
        {
            //Nekteré převolávané fce (synchronní) mají problém (náš případ), že nejdou pustit v hlavní metodě třídy
            //a musí být zahrnuty v 1. nebo 3. části fce viz: https://public.helios.eu/green/doc/cs/index.php?title=Funkce_-_popis#Funkce_.E2.80.93_z.C3.A1kladn.C3.AD_popis
            //Je to trochu prasárna, ale funguje.
            if (!error)
            {
                try
                {
                    if (inst.Master.GetItemInt32(0, "stav") != 7) StornoFD();
                    UpravitAVratit(DP, dvDP);
                }
                catch (Exception ex)
                {
                    Message.Error("Chyba: " + ex);
                }
            }        
        }

        private void UpravitAVratit((string name, string vec, Int32? spis) DP, Int32Array dvDP)
        {
            using (INrsCowley cowley = NrsCowley.GetCowley(88, "DokumentUprava", 2300309, true))
            {
                cowley.Initialize(dvDP.First(), this);
                cowley.Params.SetItem(0, "souvisi_akce", 1);
                cowley.Params.SetItem(0, "vec", String.IsNullOrEmpty(DP.vec) ? DP.name : DP.vec);
                cowley.Params.SetItem(0, "nazev", DP.name);
                cowley.Params.SetItem(0, "typ_dokumentu", 44); //Nevyplněno   
                cowley.ParamsOK = true;
                cowley.Run();
            }
            if (DP.spis.HasValue)
            {
                using (INrsCowley cowley = NrsCowley.GetCowley(2111, "VratitSpisDoESSS", 6921, true))
                {
                    cowley.Initialize(DP.spis.Value, this);
                    cowley.Params.SetItem(0, "duvod", "Nejedná se o fakturu došlou.");
                    cowley.ParamsOK = true;
                    cowley.Run();
                }
            }
            else
            {
                using (INrsCowley cowley = NrsCowley.GetCowley(88, "VratitDokumentDoESSS", 2300309, true))
                {
                    cowley.Initialize(dvDP.First(), this);
                    cowley.ParamsOK = true;
                    cowley.Run();
                }
            }          
        }

        private void StornoFD()
        {
            using (INrsCowley cowley = NrsCowley.GetCowley(46, "stornovani_pd", 0, true))
            {
                cowley.Initialize(inst.RecordNumber, inst);                
                cowley.ParamsOK = true;
                cowley.Run();
            }
        }
    }
}
