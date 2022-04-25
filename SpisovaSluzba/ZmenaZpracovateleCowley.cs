using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.LCS.Base;
using Noris.LCS.Helios.Common;
using Noris.Srv;
using System;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZmenaZpracovateleCowley : NrsCowley
    {
        private const int DV_NAHLIZITELE = 112323;
        public void ZmenaZpracovatele()
        {
            foreach (var record in RecordNumbers)
            {
                try
                {
                    using (INrsInstance odchoziPosta = NrsInstance.GetInstance(1727))
                    {
                        odchoziPosta.Retrieve(record);
                        PredatSpisNaJinyUzel(odchoziPosta.Master.GetItem<Int32?>(0, "spis"), record);
                    }
                }
                catch (Exception e)
                {
                    Message.Warning($"Chyba při přidání nahlížitele spisu pro odchozí poštu: {SqlSelect.GetReference(record)}, Chyba: {e.Message}");
                    continue;
                }
            }
        }

        private void PredatSpisNaJinyUzel(Int32? spis, int record)
        {
            if (spis.HasValue)
            {
                QueryTemplate qt = QueryManager.Instance.GetQuery("GetUzivateleZmenaZpracovatele");
                qt.ReplaceParametr("posta", record);
                SqlSelect.GetInt32Array(qt.GetFinalQuery(), out Int32Array uzivatele);

                int? superUser = GetSuperUser(spis.Value);
                if (superUser.HasValue)
                    uzivatele.Add(superUser.Value);

                if (uzivatele.Count > 0)
                {
                    foreach (var uzivatel in uzivatele)
                    {
                        try
                        {
                            using (PredatNaJinySpisUzelCwl cwl = (PredatNaJinySpisUzelCwl)NrsCowley.GetCowley(ClassNumbers.SPIS, "PredatSpisNaJinyUzel_N", FolderNumbers.SPIS, true))
                            {

                                cwl.Initialize(spis.Value, this);
                                cwl.SetParams_Nevizualni(0, uzivatel, false, 0, 0, true, "");
                                cwl.ParamsOK = true;
                                cwl.Run();
                            }
                        }
                        catch (Exception e)
                        {
                            Message.Warning($"Při předávání spisu na jiný uzel u zpracovatele {SqlSelect.GetReference(uzivatel)} došlo k chybě: {e.Message}");
                            continue;
                        }
                    }
                }
                else Message.Warning($"Nebyli zjištěny žádní uživatelé pro změnu zpracovatele nad odchozí poštou {SqlSelect.GetReference(record)}.");
            }
            else Message.Warning($"Odchozí pošta {SqlSelect.GetReference(record)} neobsahuje spis.");
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
