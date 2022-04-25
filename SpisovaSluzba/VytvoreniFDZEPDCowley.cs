using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.LCS.Helios.Common;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    public class VytvoreniFDZEPDCowley : NrsCowley
    {
        private Int32 fdFolder;
        private Int32Array documentsToReturn;

        protected override void Init(InitEventArgs e)
        {
            base.Init(e);
            this.documentsToReturn = new Int32Array();
        }

public void VytvoreniFDZEPD()
{
    this.fdFolder = CustomConfig.GetInt32Value("EFD", "Folder", 0);
    if (fdFolder <= 0) Message.Error("Nebyl dohledán cílový pořadač FD");

    foreach (Int32 record in this.RecordNumbers)
    {
        using (INrsCowley addSuperUser = NrsCowley.GetCowley(this.ClassNumber, "PridatSuperuzivateleKDP", this.FolderNumber, true))
        {
            addSuperUser.Initialize(record, this);
            addSuperUser.ParamsOK = true;
            addSuperUser.Run();
        }

        using (INrsInstance instance = NrsInstance.GetInstance(this.ClassNumber))
        {
            instance.Retrieve(record);

            this.Process(instance);
        }
    }
}

        private void Process(INrsInstance instance)
        {
            try
            {
                DbTransaction.Current.Begin();

                this.VytvorFD(instance);

                DbTransaction.Current.SetComplete();
                DbTransaction.Current.End();
            }
            catch (Exception ex)
            {
                if (DbTransaction.IsTransaction)
                    DbTransaction.Current.End();

                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Chyba při zpracování záznamu: " + ex.Message);
            }
        }

        private void VytvorFD(INrsInstance instance)
        {
            if (this.CheckRecord(instance))
            {
                using (INrsInstance fdInst = NrsInstance.GetInstance(ClassNumbers.INVOICE_IN, fdFolder))
                {
                    fdInst.Reset();

                    fdInst.Master.SetItem(0, "organizace", this.DohledejNeboZalozOrganizaci(instance));
                    fdInst.MasterRelations.AddRelation(1399, instance.RecordNumber, RelationSide.Left);
                    instance.MasterRelations.GetRelations(105627, RelationSide.Right, out int[] docs);
                    fdInst.MasterRelations.AddRelations(9268, docs, RelationSide.Left);
                    fdInst.Update();
                }
            }
            else
            {
                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Na záznamu nejsou navázány žádné dokumenty");

                instance.Master.SetItem(0, "poznamka", "Dokument neobsahuje přílohu");
                instance.Master.SetItem(0, "esss_typ_dokumentu", 44);
                instance.Update();

                this.documentsToReturn.Add(instance.RecordNumber);
            }
        }

        private int GetUtvar(INrsInstance instance)
        {
            Int32 result = 0;

            instance.MasterRelations.GetRelations(106789, RelationSide.Left, out int[] idS);
            if (idS != null && idS.Length > 0)
            {
                String reference = SqlSelect.GetAttributeString(2112, idS.First(), "reference", false);
                if (!String.IsNullOrEmpty(reference))
                {
                    QueryTemplate query = QueryManager.Instance.GetQuery("SpisSluzbaGetUtvarByReference");
                    query.ReplaceParametr("poradac", FolderNumbers.UTVARY);
                    query.ReplaceParametr("reference", reference);
                    result = SqlSelect.GetInt32(query.GetFinalQuery(), 0);
                }
            }

            return result;
        }

        private String DohledejNeboZalozOrganizaci(INrsInstance instance)
        {
            int orgId = instance.Master.GetItemInt32(0, "cislo_organizace", 0);
            if (orgId > 0) return orgId.ToString();

            string ico = this.GetIco(instance);

            if (!String.IsNullOrEmpty(ico))
            {
                CSO.BaseObjects.Organizace.GetOrganizaceDleIC(ico, out CSO.BaseObjects.Organizace org);
                if (org != null && org.Id.HasValue && org.Id.Value > 0)
                {
                    return org.Id.Value.ToString();
                }
                else
                {
                    org = CSO.BaseObjects.Organizace.GetOrganizaceFromAres(ico);
                    if(org != null)
                    {
                        if (org.Id.HasValue && org.Id.Value > 0)
                        {
                            return org.Id.Value.ToString();
                        }
                        else
                        {
                            if(String.IsNullOrEmpty(org.Zeme.IsoKodZeme))
                            {
                                if(String.IsNullOrEmpty(org.DIC))
                                {
                                    org.Zeme.IsoKodZeme = "CZ";
                                }
                                else
                                {
                                    org.Zeme.IsoKodZeme = org.DIC.Substring(0, 2);
                                }
                            }
                            org.Save();
                            return org.Id.Value.ToString();
                        }
                    }
                    
                }
                return null;
            }
            else
            {
                Message.Warning("IČO není vyplněno");

                String name = instance.Master.GetItemString(0, "odesilatel");
                String prijmeni = instance.Master.GetItemString(0, "prijmeni");
                String jmeno = instance.Master.GetItemString(0, "jmeno");
                if (!String.IsNullOrEmpty(name) && !String.IsNullOrEmpty(prijmeni) && !String.IsNullOrEmpty(jmeno))
                {
                    using (INrsInstance orgInst = NrsInstance.GetInstance(ClassNumbers.ORGANIZACE, FolderNumbers.ORGANIZATION))
                    {
                        orgInst.Reset();

                        orgInst.Name = name;
                        orgInst.Master.SetItem(0, "prijmeni", prijmeni);
                        orgInst.Master.SetItem(0, "jmeno", jmeno);
                        orgInst.Master.SetItem(0, "pravni_forma", "SO");
                        orgInst.Master.SetItem(0, "ulice_ds", instance.Master.GetItemString(0, "ulice"));
                        orgInst.Master.SetItem(0, "cislo_cp", instance.Master.GetItemString(0, "cislo_domovni"));
                        orgInst.Master.SetItem(0, "misto", instance.Master.GetItemString(0, "misto"));
                        orgInst.Master.SetItem(0, "psc", instance.Master.GetItemString(0, "psc"));
                        orgInst.Master.SetItem(0, "cislo_co", instance.Master.GetItemString(0, "cislo_orientacni"));

                        orgInst.Master.SetItem(0, "lcs_uda_organizace_vlozeno_z", "p");

                        orgInst.Update();
                        return orgInst.RecordNumber.ToString();
                    }
                }
                else
                {
                    Message.Warning("Nebude založena organizace");
                    return null;
                }
            }
        }

        //ziskam ico a dam ho do spravnweho formatu
        private string GetIco(INrsInstance instance)
        {
            string ico = instance.Master.GetItemString(0, "ico_att");

            if(!String.IsNullOrEmpty(ico))
            {
                var tempIco = ico;
                ico = "";
                foreach(char c in tempIco)
                {
                    if (Char.IsDigit(c)) ico += c;
                }

                if (ico.All(x => x == '0')) ico = null;
            }


            return ico;
        }

        private bool CheckRecord(INrsInstance instance)
        {
            instance.MasterRelations.GetRelations(105627, RelationSide.Right, out Int32Array documents);

            return (documents != null && documents.Count > 0);
        }

        protected override void Done(DoneEventArgs e)
        {
            if(this.documentsToReturn != null && this.documentsToReturn.Length > 0)
            {
                foreach(var doc in this.documentsToReturn)
                {
                    int spis = SqlSelect.GetAttributeInt32(new RecordId(this.ClassNumber, doc), "spis", 0);
                    if(spis > 0)
                    {
                        using (INrsCowley cowley = NrsCowley.GetCowley(2111, "VratitSpisDoESSS", 6921, true))
                        {
                            cowley.Initialize(spis, this);
                            cowley.Params.SetItem(0, "duvod", "Dokument neobsahuje přílohu.");
                            cowley.ParamsOK = true;
                            cowley.Run();
                        }
                    }
                    else
                    {
                        using (INrsCowley cowley = NrsCowley.GetCowley(88, "VratitDokumentDoESSS", 2300309, true))
                        {
                            cowley.Initialize(doc, this);
                            cowley.ParamsOK = true;
                            cowley.Run();
                        }
                    }
                }
            }

            base.Done(e);
        }
    }
}
