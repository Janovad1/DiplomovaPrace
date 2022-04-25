using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZalozitDokumentAuditniStopyCowley : NrsCowley
    {
        public void ZalozitDokumentAuditniStopy()
        {
            foreach (Int32 record in this.RecordNumbers)
            {
                using (INrsInstance instance = NrsInstance.GetInstance(this.ClassNumber))
                {
                    instance.Retrieve(record);

                    this.Process(instance);
                }
            }
        }

        private bool revert;

        protected void Process(INrsInstance instance)
        {
            try
            {
                DbTransaction.Current.Begin();

                revert = true;

                var spis = this.VytvorDokument(instance);

                DbTransaction.Current.SetComplete();
                DbTransaction.Current.End();

                //Nastaví se superUser ze zak. konfigurace, nelze v transakci
                ESSSUtils.PredatSpisNaJinyUzel(spis);
            }
            catch (Exception ex)
            {

                if (DbTransaction.IsTransaction)
                {
                    if (!revert) DbTransaction.Current.SetComplete();
                    DbTransaction.Current.End();
                }

                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Chyba při zpracování záznamu: " + ex.Message);
            }
        }

        protected virtual int VytvorDokument(INrsInstance instance)
        {
            using (INrsInstance odchoziPostaInst = NrsInstance.GetInstance(1727, 10008658))
            {
                odchoziPostaInst.Reset();

                odchoziPostaInst.Name = "Auditní stopa " + instance.Reference;
                odchoziPostaInst.Master.SetItem(0, "typ_zpravy", 36956617); //36956617 - cs spisove sluzby
                odchoziPostaInst.MasterRelations.AddRelation(2301043, instance.RecordNumber, RelationSide.Right);
                odchoziPostaInst.Master.SetItem(0, "esss_typ_dokumentu", 44);
                odchoziPostaInst.Master.SetItem(0, "charakter_dokumentu", "D");

                var spisUzel = this.GetSpisUzel(instance);
                var spisPlan = this.GetSpisPlan(instance);
                if (spisUzel > 0) odchoziPostaInst.MasterRelations.AddRelation(106781, spisUzel, RelationSide.Left);

                odchoziPostaInst.Master.SetItem(0, "ess_vecna_skupina", spisPlan);

                odchoziPostaInst.Update();

                return this.CallAdditionalFunctions(odchoziPostaInst, instance, false);
            }
        }

        protected int CallAdditionalFunctions(INrsInstance odchoziPostaInst, INrsInstance instance, bool zalozitSpis)
        {
            using (INrsCowley zalozDocCowley = NrsCowley.GetCowley(odchoziPostaInst.ClassNumber, "ZalozitDokumentVESSS", odchoziPostaInst.FolderNumber, true))
            {
                zalozDocCowley.Initialize(odchoziPostaInst);
                zalozDocCowley.ParamsOK = true;
                zalozDocCowley.Run();
            }

            //pokud byl dokument zalozen v ESSS a pozdeji nastane chyba, uz nebudu zmeny vracet
            this.revert = false;
            Int32Array spis = new Int32Array();
            if (zalozitSpis)
            {
                using (INrsCowley vlozitDocCowley = NrsCowley.GetCowley(odchoziPostaInst.ClassNumber, "ZalozitNovySpisDoESSS", odchoziPostaInst.FolderNumber, true))
                {
                    vlozitDocCowley.Initialize(odchoziPostaInst);
                    vlozitDocCowley.Params.SetItem(0, "nazev_spisu", GetNazevSpisu(instance));
                    vlozitDocCowley.Params.SetItem(0, "vlastnik_spisu", instance.Master.GetItem<Int32?>(0, "lcs_uda_pozadavek_na_fin_plneni_hlavicka_manazer_zakazky"));
                    vlozitDocCowley.ParamsOK = true;
                    vlozitDocCowley.Run();
                    spis = vlozitDocCowley.ResultSet;
                }
            }
            else
            {
                spis.Add(this.GetSpis(instance));

                if (spis.First() != 0)
                {
                    //Vložit dokument do spisu
                    using (INrsCowley cowley = NrsCowley.GetCowley(odchoziPostaInst.ClassNumber, "VlozitDokumentDoSpisuESSS", odchoziPostaInst.FolderNumber, true))
                    {
                        cowley.Initialize(odchoziPostaInst);
                        cowley.Params.SetItem(0, "spis", spis.First());
                        cowley.ParamsOK = true;
                        cowley.Run();
                    }
                }
                else
                {
                    Message.Warning("Spis na odchozí poště nebyl dohledán");
                }
            }
            return spis.First();
        }

        private string GetNazevSpisu(INrsInstance instance)
        {
            instance.MasterRelations.GetRelations(10570, RelationSide.Left, out Int32Array smlouva);
            instance.MasterRelations.GetRelations(10572, RelationSide.Left, out Int32Array dodatek);

            if ((smlouva.Count + dodatek.Count) > 0)
            {
                if (smlouva.Count > 0)
                    return $"Smlouva {instance.Master.GetItemString(0, "nazev_subjektu")}";
                else
                    return $"Dodatek {instance.Master.GetItemString(0, "nazev_subjektu")}";
            }
            else return instance.Master.GetItemString(0, "nazev_subjektu");
        }

        private int GetSpisPlan(INrsInstance instance)
        {
            int result = 0;

            //z faktury ziskam doslou postu, z ni pak ziskam vecnou skupinu
            QueryTemplate query = QueryManager.Instance.GetQuery("DohledejVecnouSkupinuDleFaktury");
            query.ReplaceParametr("faktura", instance.RecordNumber);
            SqlSelect.GetInt32Array(query.GetFinalQuery(), out Int32Array array);

            if (array != null && array.Length > 0) result = array[0];

            if (result == 0) result = 1212; //pokud neni dohledana vecna skupina, vyplnim vs 12.2.5

            return result;
        }

        private int GetSpis(INrsInstance instance)
        {
            int result = 0;

            //z faktury ziskam doslou postu, z ni pak ziskam vecnou skupinu
            QueryTemplate query = QueryManager.Instance.GetQuery("DohledejVecnouSkupinuDleFaktury");
            query.ReplaceParametr("faktura", instance.RecordNumber);
            SqlSelect.GetInt32Array(query.GetFinalQuery().Replace("ess_vecna_skupina", "spis"), out Int32Array array);

            if (array != null && array.Length > 0) result = array[0];

            if (result == 0)
            {
                query = QueryManager.Instance.GetQuery("DohledejSpisFakturyDleOP");
                query.ReplaceParametr("fd", instance.RecordNumber);
                query.ReplaceParametr("reference", instance.Reference);
                SqlSelect.GetInt32Array(query.GetFinalQuery().Replace("ess_vecna_skupina", "spis"), out Int32Array array2);

                if (array2 != null && array2.Length > 0) result = array2[0];

                if (result == 0) Message.Warning("Nebyl dohledán spis");
            }

            return result;
        }

        private int GetSpisUzel(INrsInstance instance)
        {
            var result = 0;
            //nejprve zkusim dohledat doslou postu
            instance.MasterRelations.GetRelations(1399, RelationSide.Left, out Int32Array relations);
            if (relations != null && relations.Length > 0)
            {
                QueryTemplate query = QueryManager.Instance.GetQuery("DohledejSpisUzelFD");
                query.ReplaceParametr("fd", instance.RecordNumber);
                result = SqlSelect.GetInt32(query.GetFinalQuery(), 0);
            }

            if (result == 0)
            {
                var utvarId = instance.Master.GetItemInt32(0, "utvar", 0);

                if (utvarId > 0)
                {
                    var utvarRef = SqlSelect.GetReference(utvarId);
                    result = SqlSelect.GetInt32("select cislo_nonsubjektu from lcs.spisovy_uzel where reference = '" + utvarRef + "'", 0);
                }
            }

            return result;
        }
    }
}
