using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.LCS.Base;
using Noris.LCS.Helios.Common;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Noris.KonceptHK.SpisovaSluzba
{

    class PripravitZverejneniVESSSCowley : NrsCowley
    {
        private const int MAIL_FOLDER = 10008658;
        private const int DOCUMENT_RELATION = 105627;
        private int zpusobVypraveni;

        public void PripravitZverejneniVESSS()
        {
            zpusobVypraveni = CustomConfig.GetRelationValue("SpisovaSluzba", "ZpusobVypraveni").RecordNumber;

            foreach (Int32 record in this.RecordNumbers)
            {
                using (INrsInstance instance = NrsInstance.GetInstance(this.ClassNumber))
                {
                    instance.Retrieve(record);

                    this.PripravitZverejneniVESSS(instance);
                }
            }
        }

        private void PripravitZverejneniVESSS(INrsInstance instance)
        {
            try
            {
                DbTransaction.Current.Begin();

                this.Process(instance);

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

        private void Process(INrsInstance instance)
        {
            if (this.CheckRecord(instance))
            {
                var attachments = this.GetAttachments(instance.RecordNumber);
                if (attachments != null && attachments.Count > 0)
                {
                    bool update = true;
                    instance.Master.SetItem(0, "stav", 3);

                    var spisUzel = this.GetSU(instance);
                    if (spisUzel > 0)
                        instance.MasterRelations.AddRelation(106781, spisUzel, RelationSide.Left);

                    var smlouva = this.DohledejSmlouvu(instance);
                    if (smlouva > 0) instance.MasterRelations.AddRelation(2301046, smlouva, RelationSide.Right);

                    instance.Master.SetItem(0, "zpusob_vypraveni", zpusobVypraveni);

                    //nejprve projdu jiz navazane dokumenty v DV 105627 - pokud ma dokument vyplneny atribut esss_id, vytvorim kopii a puvodni zaznam odvazu
                    //
                    instance.MasterRelations.GetRelations(DOCUMENT_RELATION, RelationSide.Right, out Int32Array documents);
                    if (documents != null && documents.Length > 0)
                    {
                        foreach (int docId in documents)
                        {
                            String esssId = SqlSelect.GetAttributeString(new RecordId(ClassNumbers.DOCUMENT_DMS, docId), "esss_id");
                            if (!String.IsNullOrEmpty(esssId))
                            {
                                //esss_id je vyplneno, provedu tedy upravy
                                var newDocId = ESSSUtils.DuplicateESSSDoc(docId, SqlSelect.GetFolderNumber(docId));

                                instance.MasterRelations.DeleteRelation(DOCUMENT_RELATION, docId, RelationSide.Right);
                                instance.MasterRelations.AddRelation(DOCUMENT_RELATION, newDocId, RelationSide.Right);
                            }
                        }
                    }
                    
                    var hlavni_dokument_zverejneni = instance.Master.GetItemInt32(0, "hlavni_dokument_zverejneni", 0);

                    foreach (var id in attachments)
                    {
                        using (MailAttachmentInst inst = (NrsInstance.GetInstance(1708)) as MailAttachmentInst)
                        {
                            inst.Retrieve(id);
                            if (!this.DocumentExists(inst, instance))
                                using (EdmDocInstance edmDocumentInstance = (EdmDocInstance)NrsInstance.GetInstance(1132, 10006341))
                                {
                                    edmDocumentInstance.Reset();
                                    string name = inst.Master.GetItemString(0, "document_name");
                                    edmDocumentInstance.Master.SetItem(0, "nazev_subjektu", name);
                                    edmDocumentInstance.Master.SetItem(0, "physical_name", name);

                                    var content = inst.Content;

                                    if (Path.GetExtension(name).ToLower() == ".xml")
                                    {
                                        content = this.EditXml(content, instance);
                                    }

                                    //pokud je vyplnen hlavni dokument zverejneni, doplnim do XML id_zpravy
                                    if (name == "pridani_prilohy.xml" && hlavni_dokument_zverejneni > 0)
                                    {
                                        var id_zpravy = SqlSelect.GetAttributeString(new RecordId(this.ClassNumber, hlavni_dokument_zverejneni), "id_zpravy");
                                        if (!String.IsNullOrEmpty(id_zpravy) && id_zpravy != "0")
                                            content = this.EditAttachmentXml(content, id_zpravy);
                                        else
                                        {
                                            Message.Info("Id zprávy nebylo dohledáno, záznam nebude zpracován");
                                            update = false;
                                            break;
                                        }
                                    }

                                    try
                                    {
                                        edmDocumentInstance.SaveBlobBin(content, name, false);
                                    }
                                    catch
                                    {
                                        Message.Error("Nepodařilo se uložit soubor. Kontaktujte správce.");
                                    }

                                    edmDocumentInstance.MasterRelations.AddRelation(DOCUMENT_RELATION, instance.RecordNumber, RelationSide.Left);
                                    edmDocumentInstance.Update();
                                }
                        }
                    }
                    if (update) instance.Update();
                }
            }
            else
            {
                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Záznam bude přeskočen");
            }
        }

        private Int32Array GetAttachments(int recordNumber)
        {
            SqlSelect.GetInt32Array("SELECT cislo_nonsubjektu FROM lcs.mail_attachment WHERE message = " + recordNumber, out Int32Array count);
            return count;
        }

        private bool CheckRecord(INrsInstance instance)
        {
            bool result = true;

            if (instance.FolderNumber != MAIL_FOLDER)
            {
                Message.Warning("Funkci lze poštět pouze v záznamech z pořadače " + MAIL_FOLDER);
                result = false;
            }

            if (instance.Master.GetItemInt32(0, "zverejneni_dokumentu", -1) <= 0)
            {
                Message.Warning("Není vyplněn vztah zveřejnění dokumentu");
                result = false;
            }

            return result;
        }
        private int GetSU(INrsInstance odchoziPostaInst)
        {
            //vyhledam Spisovy uzel dle reference utvaru na smlouve
            int result = 0;

            QueryTemplate queryT = QueryManager.Instance.GetQuery("GetSmlouvuOdchoziPosty");
            queryT.ReplaceParametr("subj", odchoziPostaInst.RecordNumber);
            Datastore ds = SqlSelect.CreateDatastore(queryT.GetFinalQuery());
            if (ds.Retrieve() > 0)
            {
                int smlouva = ds.GetItemInt32(0, 0, 0);
                if (ds.GetItemInt32(0, 1) == 651 || ds.GetItemInt32(0, 1) == 429)
                {
                    int utvarId = SqlSelect.GetAttributeInt32(ds.GetItemInt32(0, 1), smlouva, "utvar", false, 0);
                    if (utvarId > 0)
                    {
                        string utvarRef = SqlSelect.GetReference(utvarId);
                        result = SqlSelect.GetInt32("select cislo_nonsubjektu from lcs.spisovy_uzel where reference = '" + utvarRef + "'", 0);
                    }
                }
            }

            return result;
        }

        private int DohledejSmlouvu(INrsInstance odchoziPostaInst)
        {
            int result = 0;

            QueryTemplate queryT = QueryManager.Instance.GetQuery("GetSmlouvuOdchoziPosty");
            queryT.ReplaceParametr("subj", odchoziPostaInst.RecordNumber);
            Datastore ds = SqlSelect.CreateDatastore(queryT.GetFinalQuery());
            if (ds.Retrieve() > 0)
            {
                result = ds.GetItemInt32(0, 0, 0);
            }

            return result;
        }

        private bool DocumentExists(MailAttachmentInst inst, INrsInstance odchoziPostaInst)
        {
            odchoziPostaInst.MasterRelations.GetRelations(DOCUMENT_RELATION, RelationSide.Right, out Int32Array docs);
            foreach (var docId in docs)
            {
                using (EdmDocInstance edmDocumentInstance = (EdmDocInstance)NrsInstance.GetInstance(1132, 10006341))
                {
                    edmDocumentInstance.Retrieve(docId);
                    if (edmDocumentInstance.Master.GetItemString(0, "physical_name") == inst.Master.GetItemString(0, "document_name"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private byte[] EditXml(byte[] content, INrsInstance odchoziPostaInst)
        {
            QueryTemplate queryT = QueryManager.Instance.GetQuery("GetSmlouvuOdchoziPosty");
            queryT.ReplaceParametr("subj", odchoziPostaInst.RecordNumber);
            Datastore ds = SqlSelect.CreateDatastore(queryT.GetFinalQuery());
            if (ds.Retrieve() > 0)
            {
                //nejprve ziskame xml ve stringu
                String xmlString = Encoding.UTF8.GetString(content);
                XDocument xDoc = XDocument.Parse(xmlString);
                XNamespace ns = xDoc.Root.GetDefaultNamespace();

                //cislo smlouvy/dodatku nacteme do stringu
                String s = ds.GetItemString(0, 0);
                if (ds.GetItemInt32(0, 1) == 651)
                {
                    using (INrsInstance elsInst = NrsInstance.GetInstance(ClassNumbers.EVIDENCNI_LIST_SMLOUVY))
                    {
                        elsInst.Retrieve(int.Parse(s));
                        int? ramcSml = null;

                        //Message.Info("Rámcová smlouva?");
                        String typSml = elsInst.Master.GetItemString(0, "lcs_uda_smlouva_hlavicka_typ_smlouvy_2");
                        if ((typSml == "P") || (typSml == "N"))
                        {
                            elsInst.MasterRelations.GetRelations(2300329, RelationSide.Left, out Int32Array i);
                            if (i.Length > 0)
                                ramcSml = i[0];
                        }
                        if (ramcSml.HasValue)
                        {
                            queryT = QueryManager.Instance.GetQuery("GetReleaseIdRamcoveSmlouvy");
                            queryT.ReplaceParametr("subj", ramcSml.Value);
                            s = SqlSelect.GetString(queryT.GetFinalQuery());
                            if (!String.IsNullOrEmpty(s))
                            {
                                //super, mame id navazane smlouvy, ted rozsrime xml
                                if (!xmlString.ToUpper().Contains("NAVAZANYZAZNAM"))
                                {
                                    XElement root = xDoc.Element(ns + "zverejneni");
                                    if (root == null)
                                        root = xDoc.Element(ns + "modifikace");
                                    if (root != null)
                                    {
                                        root = root.Element(ns + "smlouva");
                                        XElement xe = new XElement(ns + "navazanyZaznam", s);
                                        root.Add(xe);
                                    }
                                }
                            }
                        }
                        //Message.Info("Získám členy sdružení");
                        elsInst.MasterRelations.GetRelations(10542, RelationSide.Left, out Int32Array clenoveSdruzeni);
                        //Message.Info(clenoveSdruzeni.Length + " členů");
                        if (clenoveSdruzeni.Length > 0)
                        {
                            foreach (int clenId in clenoveSdruzeni)
                            {
                                //Message.Info("Člen: " + clenId);
                                using (INrsInstance clenInst = NrsInstance.GetInstance(ClassNumbers.ORGANIZACE))
                                {
                                    clenInst.Retrieve(clenId);

                                    XElement root = xDoc.Element(ns + "zverejneni");
                                    if (root == null)
                                        root = xDoc.Element(ns + "modifikace");
                                    if (root != null)
                                    {
                                        //Message.Info("Root není null");
                                        root = root.Element(ns + "smlouva");
                                        XElement smluvniStrana = root.Element(ns + "smluvniStrana");

                                        XElement newSmluvniStrana = new XElement(ns + "smluvniStrana");

                                        XElement nazev = new XElement(ns + "nazev", clenInst.Master.GetItemString(0, "nazev_subjektu"));
                                        XElement ico = new XElement(ns + "ico", clenInst.Master.GetItemString(0, "ico"));
                                        XElement adresa = new XElement(ns + "adresa", clenInst.Master.GetItemString(0, "ulice") + ", " + clenInst.Master.GetItemString(0, "misto"));
                                        XElement prijemce = new XElement(ns + "prijemce", 1);

                                        newSmluvniStrana.Add(nazev);
                                        newSmluvniStrana.Add(ico);
                                        newSmluvniStrana.Add(adresa);
                                        newSmluvniStrana.Add(prijemce);

                                        smluvniStrana.AddAfterSelf(newSmluvniStrana);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (INrsInstance elsInst = NrsInstance.GetInstance(ClassNumbers.SMLOUVA_DODATEK))
                    {

                        elsInst.Retrieve(int.Parse(s));

                        //Message.Info("Získám členy sdružení");
                        elsInst.MasterRelations.GetRelations(10543, RelationSide.Left, out Int32Array clenoveSdruzeni);
                        //Message.Info(clenoveSdruzeni.Length + " členů");
                        if (clenoveSdruzeni.Length > 0)
                        {
                            foreach (int clenId in clenoveSdruzeni)
                            {
                                //Message.Info("Člen: " + clenId);
                                using (INrsInstance clenInst = NrsInstance.GetInstance(ClassNumbers.ORGANIZACE))
                                {
                                    clenInst.Retrieve(clenId);

                                    XElement root = xDoc.Element(ns + "zverejneni");
                                    if (root == null)
                                        root = xDoc.Element(ns + "modifikace");
                                    if (root != null)
                                    {
                                        //Message.Info("Root není null");
                                        root = root.Element(ns + "smlouva");
                                        XElement smluvniStrana = root.Element(ns + "smluvniStrana");

                                        XElement newSmluvniStrana = new XElement(ns + "smluvniStrana");

                                        XElement nazev = new XElement(ns + "nazev", clenInst.Master.GetItemString(0, "nazev_subjektu"));
                                        XElement ico = new XElement(ns + "ico", clenInst.Master.GetItemString(0, "ico"));
                                        XElement adresa = new XElement(ns + "adresa", clenInst.Master.GetItemString(0, "ulice") + ", " + clenInst.Master.GetItemString(0, "misto"));
                                        XElement prijemce = new XElement(ns + "prijemce", 1);

                                        newSmluvniStrana.Add(nazev);
                                        newSmluvniStrana.Add(ico);
                                        newSmluvniStrana.Add(adresa);
                                        newSmluvniStrana.Add(prijemce);

                                        smluvniStrana.AddAfterSelf(newSmluvniStrana);
                                    }
                                }
                            }
                        }
                    }
                }
                xmlString = String.Concat(xDoc.Declaration.ToString(), xDoc.ToString());
                return Encoding.UTF8.GetBytes(xmlString);
            }
            else
            {
                return content;
            }
        }

        private byte[] EditAttachmentXml(byte[] content, string id_zpravy)
        {
            String xmlString = Encoding.UTF8.GetString(content);
            XDocument xDoc = XDocument.Parse(xmlString);

            var result = xDoc.Root.Descendants().Where(o => o.Name.LocalName == "datovaZprava").FirstOrDefault();

            if (result != null)
            {
                result.Value = id_zpravy;
            }

            return Encoding.UTF8.GetBytes(String.Concat(xDoc.Declaration.ToString(), xDoc.ToString()));
        }
    }
}
