using Noris.KonceptHK.SpisovaSluzba.Enums;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class VlozitPotvrzeniZverDoSpisuCowley : NrsCowley
    {
        private Int32? spis = null;
        public void VlozitPotvrzeniZverDoSpisu()
        {
            foreach (var record in RecordNumbers)
            {
                try
                {
                    using (INrsInstance doslaPosta = NrsInstance.GetInstance(88))
                    {
                        doslaPosta.Retrieve(record);
                        doslaPosta.MasterRelations.GetRelations(106822, RelationSide.Left, out Int32Array odchoziPosta);
                        //kontroly
                        var warnings = KontrolyPredVlozenimDoSpisu(doslaPosta, odchoziPosta);
                        //Preneseni spisoveho uzlu z odchozí pošty
                        warnings += PreneseniSpisovehoUzlu(odchoziPosta, doslaPosta);

                        if (!String.IsNullOrEmpty(warnings))
                        {
                            Message.Warning(warnings);
                            continue;
                        }
                        if (spis.HasValue)
                        {
                            doslaPosta.Update();
                            //6329 Vložit dokument do spisu ESSS
                            using (INrsCowley cowley = NrsCowley.GetCowley(88, "VlozitDokumentDoSpisuESSS", 0, true))
                            {
                                cowley.Initialize(record, this);
                                cowley.Params.SetItem(0, "spis", spis);
                                cowley.ParamsOK = true;
                                cowley.Run();
                            }
                        }
                        else continue;
                    }
                }
                catch (Exception e)
                {
                    Message.Warning($"Při vkládání potvrzení o zveřejnení do spisu nad došlou poštou {SqlSelect.GetReference(record)} nastala chyba: {e}");
                    continue;
                    throw;
                }              
            }
        }

        private String PreneseniSpisovehoUzlu(Int32Array odchoziPosta, INrsInstance doslaPosta)
        {
            var warnings = "";
            if (odchoziPosta.Count > 0)
            {              
                using (INrsInstance odchoziP = NrsInstance.GetInstance(1727))
                {
                    odchoziP.Retrieve(odchoziPosta.First());
                    odchoziP.MasterRelations.GetRelations(106781, RelationSide.Left, out Int32Array spisovyUzel);

                    if (spisovyUzel.Count > 0)
                    {
                        //Smazu navazane spisove uzly na dosle poště
                        doslaPosta.MasterRelations.DeleteRelations(106789, RelationSide.Left);
                        doslaPosta.MasterRelations.AddRelation(106789, spisovyUzel.First(), RelationSide.Left);
                    }
                    else warnings += "Nelze přenést spisový uzel na došlou poštu. Na odchozí poště není navázaný spisový uzel.";
                }              
            }
            return warnings;
        }

        private String KontrolyPredVlozenimDoSpisu(INrsInstance doslaPosta, Int32Array odchoziPosta)
        {
            var warnings = "";            

            if (odchoziPosta.Count == 0)
                warnings += "Došlá pošta neobsahuje odchozí poštu. \n";
            else
            {
                if ((TypZpravy)doslaPosta.Master.GetItemInt32(0, "typ_zpravy") == TypZpravy.SPISOVA_SLUZBA
                && !IsInfoZverejneni(odchoziPosta.First()))
                    warnings += "Nejedná se o došlou poštu s informací o zveřejnění. \n";
            }

            if (doslaPosta.Master.GetItem<Int32?>(0, "spis").HasValue)
                warnings += "Na došlé poště je vyplněn spis.";

            return warnings;
        }

        private bool IsInfoZverejneni(Int32 csOdchoziPosta)
        {
            using (INrsInstance odchoziPosta = NrsInstance.GetInstance(1727))
            {
                odchoziPosta.Retrieve(csOdchoziPosta);
                if (odchoziPosta.Master.GetItem<Int32?>(0, "spis").HasValue) spis = odchoziPosta.Master.GetItem<Int32?>(0, "spis");
                else Message.Warning($"Odchozí pošta {SqlSelect.GetReference(csOdchoziPosta)} neobsahuje spis!");

                if ((TypZpravy)odchoziPosta.Master.GetItemInt32(0, "typ_zpravy") == TypZpravy.SPISOVA_SLUZBA
                    && odchoziPosta.Master.GetItem<Int32?>(0, "zverejneni_dokumentu").HasValue) return true;
                else return false;
            }
        }
    }
}
