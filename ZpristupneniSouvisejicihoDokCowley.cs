using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.LCS.Base;
using Noris.LCS.Helios.Common;
using Noris.Srv;
using System;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZpristupneniSouvisejicihoDokCowley : NrsCowley
    {
        private const int DV_NAHLIZITELE = 112422;

        public void ZpristupneniSouvisejicihoDok()
        {
            foreach (var faktura in RecordNumbers)
            {
                try
                {
                    LoadSouvisejiciDokumenty(faktura);
                }
                catch (Exception e)
                {
                    Message.Warning($"Pro fakturu {SqlSelect.GetReference(faktura)} nastala chyba: {e}");
                    continue;
                }
            }
        }

        private void LoadSouvisejiciDokumenty(int faktura)
        {
            QueryTemplate qt = QueryManager.Instance.GetQuery("GetSouvisejiciDokumenty");
            qt.ReplaceParametr("faktura", faktura);
            SqlSelect.GetInt32Array(qt.GetFinalQuery(), out Int32Array docs);

            foreach (var doc in docs)
            {
                if (KonceptHK.Service.BaseService.DebugModeEnabled())
                    Message.Info($"Spouštím funkce nad dokumentem s čs: {doc} a ref: {SqlSelect.GetReference(doc)}");
                try
                {
                    //Dotahne vlastnika, jestli neexistuje tak dodá, kdo založil doc
                    QueryTemplate qtVlastnik = QueryManager.Instance.GetQuery("GetOwnerOfDoc");
                    qtVlastnik.ReplaceParametr("doc", doc);
                    var owner = SqlSelect.GetInt32(qtVlastnik.GetFinalQuery());

                    Message.Info($"Owner dokumentu je: {owner}");
                    NacistSouvisejici(doc, owner);
                    ZpristupnitSouvisejici(doc, faktura, owner);
                }
                catch (Exception e)
                {
                    Message.Warning($"Nad dokumentem s čs: {doc} došlo k chybě: {e.Message}");
                    continue;
                }
            }
        }

        private void ZpristupnitSouvisejici(int doc, int faktura, int owner)
        {
            QueryTemplate qt = QueryManager.Instance.GetQuery("GetNahliziteleKO");
            qt.ReplaceParametr("faktura", faktura);
            SqlSelect.GetInt32Array(qt.GetFinalQuery(), out Int32Array users);

            int? superUser = GetSuperUser(doc);
            if (superUser.HasValue)
                users.Add(superUser.Value);

            foreach (var user in users)
            {
                try
                {
                    using (PredatNaJinySpisUzelCwl cwl = (PredatNaJinySpisUzelCwl)NrsCowley.GetCowley(ClassNumbers.ESSSSouvisejiciDokument, "ZpristupnitSouvisejici_N", FolderNumbers.ESSSSouvisejiciDokument, true))
                    {
                        cwl.Initialize(doc, this);
                        cwl.SetParams_Nevizualni(0, user, false, 0, 0, true, "");
                        cwl.SetParams_Autorizace(owner);
                        cwl.ParamsOK = true;
                        cwl.Run();
                    }
                }
                catch (Exception e)
                {
                    Message.Warning($"Při přidávání nahlížitele {SqlSelect.GetReference(user)} na dokument {doc} došlo k chybě: {e.Message}");
                    continue;
                }
            }
        }

        private void NacistSouvisejici(int doc, int owner)
        {
            using (NacistSouvisejiciZEsssCwl cowley = (NacistSouvisejiciZEsssCwl)NrsCowley.GetCowley(ClassNumbers.ESSSSouvisejiciDokument, "NacistSouvisejiciZEsss", FolderNumbers.ESSSSouvisejiciDokument, false))
            {
                cowley.Initialize(doc, this);
                //cowley.SetParamsNacteni(true, false); // (Jen u vybraných záznamů | Aktualizovat i již načtené)
                cowley.SetParamsAutorizace(true, owner);
                cowley.Params.SetItem(0, "only_new", 0);
                cowley.ParamsOK = true;
                cowley.Run();
            }
        }

        /// <summary>
        /// Přidá superUsera ze zak. konfigurace
        /// </summary>
        private int? GetSuperUser(int record)
        {
            int? superUser = null;
            if (CustomConfig.KeyExists("EPD", "superUser"))
                superUser = CustomConfig.GetRelationValue("EPD", "superUser").RecordNumber;
            else
                Message.Error($"Nenalezen záznam v zak. konfiguraci dle parametrů 'EPD' - 'superUser'");

            //Jestli existuje v DV, už ho nepřidávám
            SqlSelect.GetRecordsForRelation(DV_NAHLIZITELE, record, RelationSide.Left, out Int32Array uzivatele);
            if (uzivatele.Contains(superUser.Value))
                return null;

            return superUser.Value;
        }
    }
}