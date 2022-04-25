using Noris.KonceptHK.SpisovaSluzba.Enums;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZalozitSpisProFIKCowley : ZalozitDokumentAuditniStopyCowley
    {
        public int? spis = null;
        public void ZalozitSpisProFIK()
        {
            base.ZalozitDokumentAuditniStopy();
        }

        protected override int VytvorDokument(INrsInstance instance)
        {
            if (this.CheckRecord(instance))
            {
                using (INrsInstance odchoziPostaInst = NrsInstance.GetInstance(1727, 10008658))
                {
                    odchoziPostaInst.Reset();

                    odchoziPostaInst.Name = "Auditní stopa " + instance.Reference;
                    odchoziPostaInst.Master.SetItem(0, "typ_zpravy", TypZpravy.SPISOVA_SLUZBA); //36956617 - cs spisove sluzby
                    odchoziPostaInst.MasterRelations.AddRelation(2301044, instance.RecordNumber, RelationSide.Right);
                    odchoziPostaInst.Master.SetItem(0, "esss_typ_dokumentu", 44); //15 - typ dokumentu nevyplneno
                    odchoziPostaInst.Master.SetItem(0, "charakter_dokumentu", "D");

                    var spisUzel = this.GetSpisUzel(instance);
                    if (spisUzel > 0) odchoziPostaInst.MasterRelations.AddRelation(106781, spisUzel, RelationSide.Left);

                    odchoziPostaInst.Update();

                    spis = this.CallAdditionalFunctions(odchoziPostaInst, instance, true);
                }
            }
            else
            {
                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Záznam bude přeskočen");
            }
            return spis.Value;
        }

        private bool CheckRecord(INrsInstance instance)
        {
            instance.MasterRelations.GetRelations(2301044, RelationSide.Left, out Int32Array relations);
            if (relations != null && relations.Count > 0)
            {
                Message.Warning("Záznam již má navázanou odchozí poštu se spisem");
                return false;
            }
            return true;
        }

        private int GetSpisUzel(INrsInstance instance)
        {
            int result = 0;

            Message.Info("CS FIK: " + instance.RecordNumber);

            var utvarId = SqlSelect.GetInt32("select utvar from lcs.uda_pozadavek_na_fin_plneni_hlavicka where cislo_subjektu = " + instance.RecordNumber, 0);

            Message.Info("utvarId: " + utvarId);

            if (utvarId > 0)
            {
                var utvarRef = SqlSelect.GetReference(utvarId);
                result = SqlSelect.GetInt32("select cislo_nonsubjektu from lcs.spisovy_uzel where reference = '" + utvarRef + "'", 0);
            }

            if (result == 0) Message.Warning("Nebyl dohledán spisový uzel");

            return result;
        }
    }
}
