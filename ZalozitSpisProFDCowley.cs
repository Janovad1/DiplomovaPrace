using KonceptHK.HeliosGluon;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.LCS.Base;
using Noris.Srv;
using System;
using System.Linq;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZalozitSpisProFDCowley : NrsCowley
    {
        private const int VECNA_SKUPINA = 1212; //12.2.5
        public void ZalozitSpisProFD()
        {
            foreach (var record in RecordNumbers)
            {
                using (INrsInstance doslaPosta = NrsInstance.GetInstance(88))
                {
                    doslaPosta.Retrieve(record);
                    doslaPosta.MasterRelations.GetRelations(1399, RelationSide.Right, out Int32Array fds);
                    if (fds.Count == 0)
                    {
                        Message.Warning($"Pro došlou poštu {doslaPosta.Master.GetItemString(0, "reference_subjektu")} není navázaná FD. Záznam bude přeskočen.");
                        continue;
                    }
                    if (doslaPosta.Master.GetItem<Int32?>(0, "spis").HasValue)
                    {
                        Message.Warning($"Pro došlou poštu {doslaPosta.Master.GetItemString(0, "reference_subjektu")} je již založený spis. Záznam bude přeskočen.");
                        continue;
                    }

                    foreach (var fd in fds)
                    {
                        try
                        {
                            Int32Array spis = new Int32Array();
                            //Založení spisu
                            using (INrsCowley cowley = NrsCowley.GetCowley(88, "ZalozitNovySpisDoESSS", 0, true))
                            {
                                cowley.Initialize(record, this);
                                cowley.Params.SetItem(0, "nazev_spisu", SqlSelect.GetReference(fd));
                                cowley.Params.SetItem(0, "vlastnik_spisu", doslaPosta.Master.GetItem<Int32?>(0, "document_owner"));
                                cowley.ParamsOK = true;
                                cowley.Run();
                                spis = cowley.ResultSet;
                            }
                            if (spis.Count > 0)
                            {
                                IsVecnaSkupinaFilled(spis);
                                //Nastaví se superUser ze zak. konfigurace
                                ESSSUtils.PredatSpisNaJinyUzel(spis.First());
                            }
                            else Message.Warning($"Nezaložil se spis anebo fce neplní ResultSet.");
                        }
                        catch (Exception e)
                        {
                            Message.Warning($"Pro došlou poštu {SqlSelect.GetReference(record)} s navázanou fakturou {SqlSelect.GetReference(fd)} nastala chyba: {e}");
                            continue;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Zkontroluje zda je na spisu vyplněna věcná skupina, když ne, tak zavolá fci 8216 Upravit spis v ESSS
        /// </summary>
        private void IsVecnaSkupinaFilled(Int32Array spis)
        {
            using (INrsInstance inst = NrsInstance.GetInstance(ClassNumbers.SPIS))
            {
                inst.Retrieve(spis.First());
                if (!inst.Master.GetItem<Int32?>(0, "ess_vecna_skupina").HasValue)
                {
                    if (KonceptHK.Service.BaseService.DebugModeEnabled())
                        Message.Info($"Spis {inst.Master.GetItemString(0, "reference")} nemá vyplněnou věcnou skupinu. Volání fce 'Upravit spis v ESSS'");

                    using (ESSSSpisUpravaCwl cowley = (ESSSSpisUpravaCwl)NrsCowley.GetCowley(ClassNumbers.SPIS, "SpisUprava", FolderNumbers.SPIS, true))
                    {
                        cowley.Initialize(inst);
                        cowley.Params.SetItem(0, "nazev", inst.Master.GetItemString(0, "nazev"));
                        cowley.Params.SetItem(0, "popis", inst.Master.GetItemString(0, "poznamka"));
                        cowley.Params.SetItem(0, "vecna_skupina", VECNA_SKUPINA);
                        cowley.Params.SetItem(0, "vec", inst.Master.GetItemString(0, "vec"));
                        cowley.ParamsOK = true;
                        cowley.Run();
                    }
                }
            }
        }
    }
}