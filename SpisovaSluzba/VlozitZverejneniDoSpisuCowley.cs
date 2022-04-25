using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.KonceptHK.SpisovaSluzba.Enums;
using Noris.LCS.Base;
using Noris.LCS.Helios.Common;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class VlozitZverejneniDoSpisuCowley : NrsCowley
    {
        public void VlozitZverejneniDoSpisu()
        {
            foreach (var record in RecordNumbers)
            {
                try
                {
                    using (INrsInstance odchoziPosta = NrsInstance.GetInstance(1727))
                    {
                        odchoziPosta.Retrieve(record);
                        odchoziPosta.MasterRelations.GetRelations(2301046, RelationSide.Right, out Int32Array smlouva);
                        //kontroly
                        var warnings = KontrolyPredVlozenimDoSpisu(odchoziPosta, smlouva);
                        if (!String.IsNullOrEmpty(warnings))
                        {
                            Message.Warning($"Záznam ({odchoziPosta.Master.GetItemString(0, "reference_subjektu")}) bude přeskočen, protože: \n {warnings}");
                            continue;
                        }
                        Zpracovani(odchoziPosta, record, smlouva);
                    }
                }
                catch (Exception e)
                {
                    Message.Warning($"Při vkládání zveřejnění do spisu nad odchozí poštou {SqlSelect.GetReference(record)} nastala chyba {e}");
                    continue;
                    throw;
                }
            }
        }
        private void Zpracovani(INrsInstance odchoziPosta, int record, Int32Array smlouva)
        {
            if (String.IsNullOrEmpty(odchoziPosta.Master.GetItemString(0, "esss_zdroj_id"))
                && String.IsNullOrEmpty(odchoziPosta.Master.GetItemString(0, "esss_hodnota_id")))
            {
                //Založení dokumentu v ESSS
                using (INrsCowley cowley = NrsCowley.GetCowley(odchoziPosta.ClassNumber, "ZalozitDokumentVESSS", odchoziPosta.FolderNumber, true))
                {
                    cowley.Initialize(odchoziPosta);
                    cowley.ParamsOK = true;
                    cowley.Run();
                }
            }

            QueryTemplate qt = QueryManager.Instance.GetQuery("GetSpisSmlouvy"); //Jestli existuje spis nad doš. odch. poštou 
            qt.ReplaceParametr("odchPosta", record);
            Int32 spis = SqlSelect.GetInt32(qt.GetFinalQuery(), -1);

            if (spis > 0)
            {
                //porovnam spisove uzly na spisu a odchozi poste
                odchoziPosta.MasterRelations.GetRelations(106781, RelationSide.Left, out Int32Array spisovyUzelPosty);
                var spisUzelSpisu = SqlSelect.GetAttributeInt32(ClassNumbers.SPIS, spis, "spisovy_uzel", false, 0);

                if (spisovyUzelPosty.First() != spisUzelSpisu) //pokud se uzly lisi zavolam nad spisem funkci pro zmenu uzlu
                {
                    using (PredatNaJinySpisUzelCwl cwl = (PredatNaJinySpisUzelCwl)NrsCowley.GetCowley(ClassNumbers.SPIS, "PredatSpisNaJinyUzel_N", 6921, true))
                    {
                        cwl.Initialize(spis, this);
                        cwl.SetParams_Nevizualni(spisovyUzelPosty.First(), gCache.GetUserNumber(), false, 0, 0, false, "");
                        cwl.ParamsOK = true;
                        cwl.Run();
                    }
                }

                //Vložit dokument do spisu
                using (INrsCowley cowley = NrsCowley.GetCowley(1727, "VlozitDokumentDoSpisuESSS", 0, true))
                {
                    cowley.Initialize(record, this);
                    cowley.Params.SetItem(0, "spis", spis);
                    cowley.ParamsOK = true;
                    cowley.Run();
                }
            }
            else
            {
                //Založí nový spis
                var novySpis = new Int32Array();
                using (INrsCowley cowley = NrsCowley.GetCowley(1727, "ZalozitNovySpisDoESSS", 0, true))
                {
                    cowley.Initialize(record, this);
                    cowley.Params.SetItem(0, "nazev_spisu", "Zveřejnění " + SqlSelect.GetReference(smlouva.First()));
                    cowley.Params.SetItem(0, "vlastnik_spisu", gCache.GetUserNumber());
                    cowley.ParamsOK = true;
                    cowley.Run();
                    novySpis.AddRange(cowley.ResultSet);
                }

                var nahlizitel = 0;
                var majKey = "EPD"; var minKey = "superUser";

                if (CustomConfig.KeyExists(majKey, minKey))
                {
                    nahlizitel = CustomConfig.GetRelationValue(majKey, minKey).RecordNumber;
                }
                else
                {
                    Message.Warning($"V zakázkové konfigurace nebyl dohledán nahlížitel spisu, major Key = '{majKey}', minor Key = '{minKey}'");
                }

                if (nahlizitel > 0)
                {
                    using (PredatNaJinySpisUzelCwl cwl = (PredatNaJinySpisUzelCwl)NrsCowley.GetCowley(ClassNumbers.SPIS, "PredatSpisNaJinyUzel_N", 6921, true))
                    {
                        cwl.Initialize(novySpis, this);
                        cwl.SetParams_Nevizualni(SqlSelect.GetAttributeInt32(ClassNumbers.SPIS, novySpis.First(), "spisovy_uzel", false, 0), nahlizitel, false, 0, 0, false, "");
                        cwl.ParamsOK = true;
                        cwl.Run();
                    }
                }

            }

            if (odchoziPosta.Master.GetItemString(0, "stav") == "4" && odchoziPosta.Master.GetItemInt32(0, "zpusob_vypraveni", 0) > 0) //pokud je posta ve stavu zalozeno a je zadan zpusob vypraveni
            {
                using (INrsCowley cowley = NrsCowley.GetCowley(odchoziPosta.ClassNumber, "VypravitSPrilohouDoESSS", odchoziPosta.FolderNumber, true))
                {
                    cowley.Initialize(odchoziPosta);
                    cowley.ParamsOK = true;
                    cowley.Run();
                }
            }
        }

        private string KontrolyPredVlozenimDoSpisu(INrsInstance odchoziPosta, Int32Array smlouva)
        {
            var warnings = "";
            if (!IsOdchoziPostaZverejneni(odchoziPosta))
                warnings += $"Nejedná se o odchozí poštu zveřejnění.\n";

            if (smlouva.Count() == 0)
                warnings += $"Na odchozí poště ({odchoziPosta.Master.GetItemString(0, "reference_subjektu")}) není navázaná smlouva.\n";

            odchoziPosta.MasterRelations.GetRelations(106781, RelationSide.Left, out Int32Array spisovyUzel);
            if (spisovyUzel.Count() == 0)
                warnings += "Na odchozí poště není vyplněn spisový uzel.\n";

            if (odchoziPosta.Master.GetItem<Int32?>(0, "spis").HasValue)
                warnings += "Na odchozí poště je vyplněn spis.";
            return warnings;
        }

        private Boolean IsOdchoziPostaZverejneni(INrsInstance odchoziPosta)
        {
            if ((TypZpravy)odchoziPosta.Master.GetItemInt32(0, "typ_zpravy") == TypZpravy.SPISOVA_SLUZBA
                && odchoziPosta.Master.GetItem<Int32?>(0, "zverejneni_dokumentu").HasValue) return true;
            else return false;
        }
    }
}
