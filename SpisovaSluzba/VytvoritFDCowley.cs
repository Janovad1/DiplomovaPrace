using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using KonceptHK.HeliosGluon.Utils;
using Noris.KonceptHK.SpisovaSluzba.BaseCowleies;
using Noris.KonceptHK.SpisovaSluzba.Zpravy;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class VytvoritFDCowley : BaseESSSCowley
    {
        //Funkce spoustena nad doslou postou ESSS
        //Vytvori zaznam dosle faktury na zaklade dat v XML

        protected override void Init(NrsCowley.InitEventArgs e)
        {

            base.Init(e);

            this.UseProgress = true;
            this.Progress.Init("Příprava dat pro dialog.", 0, this.RecordNumbers.Count, false);
            this.PrepareData();
            try
            {
                //otevreme Pdf Plugin
                if (Srv.IOTunnel.Plugin.Exists("KonceptHK", "PdfPlugin"))
                {
                    XmlString xsin = new XmlString();
                    XmlString xsout = Srv.IOTunnel.Plugin.Call("KonceptHK", "PdfPlugin", "OPEN", xsin);
                }
            }
            catch
            {
                Message.Warning("Nepodařilo se otevřít PDF plugin");
            }
        }

        //kontrolni validace xml a naplneni formulare daty
        private void PrepareData()
        {
            ServiceGateFunctionUserData ServiceGateInputUserData = new ServiceGateFunctionUserData();

            if (this.RecordNumbers != null && this.RecordNumbers.Count > 0)
            {
                foreach (Int32 recordNumber in RecordNumbers)
                {
                    rNumber = recordNumber;

                    DbTransaction.Current.Begin();
                    QueryTemplate queryT = QueryManager.Instance.GetQuery("GetXmlByDoslePosty");
                    queryT.ReplaceParametr("posta", recordNumber);
                    Datastore result = SqlSelect.CreateDatastore(queryT.GetFinalQuery());

                    using(INrsInstance esssInst = NrsInstance.GetInstance(ClassNumber, FolderNumber))
                    {
                        esssInst.Retrieve(recordNumber);
                        Int32 ps = 0;
                        if (esssInst.Master.GetItemString(0, "lcs_uda_dosla_posta_pocet_spusteni_vytvoritFD") != null)
                            ps = esssInst.Master.GetItemInt32(0, "lcs_uda_dosla_posta_pocet_spusteni_vytvoritFD");

                        String xml;
                        Int32 xmlId;
                        if (result != null && result.Retrieve() > 0)
                        {
                            xml = result.GetItemString(0, "text");
                            xmlId = result.GetItemInt32(0, "cislo_subjektu");

                            try
                            {
                                ServiceGateInputUserData.SetXml(xml);
                                //potrebuji ID xml zpravy
                                base.ValidaceXmlPomociXsd(ServiceGateInputUserData, xmlId, "K81PrijemFD.xsd");

                                oFD = new ObalkaFakturaDoslaESSS(ServiceGateInputUserData.CreateXmlFragmentReader(), xmlId);

                                this.Params.SetItem<Int32>(0, "pocet_spusteni", ps);

                                this.Params.SetItem<string>(0, "Podatelna", oFD.Podatelna);
                                this.Params.SetItem<string>(0, "IDSpisu", oFD.IDSpisu);
                                this.Params.SetItem<string>(0, "SpisovaZnacka", oFD.SpisovaZnacka);
                                this.Params.SetItem<string>(0, "CisloJednaci", oFD.CisloJednaci);
                                this.Params.SetItem<string>(0, "CarovyKod", oFD.CarovyKod);

                                this.Params.SetItem<string>(0, "CisloFaktury", oFD.FakturaDosla.CisloFaktury);
                                this.Params.SetItem<string>(0, "VSY", oFD.FakturaDosla.VSY);
                                this.Params.SetItem<string>(0, "SSY", oFD.FakturaDosla.SSY);

                                this.Params.SetItem<DateTime>(0, "DatumPrijeti", oFD.FakturaDosla.HlavickaFD.DatumPrijeti);
                                this.Params.SetItem<DateTime>(0, "DUZP", oFD.FakturaDosla.HlavickaFD.DUZP);
                                this.Params.SetItem<DateTime>(0, "DatumSplatnosti", oFD.FakturaDosla.HlavickaFD.DatumSplatnosti);

                                this.Params.SetItem<string>(0, "IC", oFD.FakturaDosla.HlavickaFD.Dodavatel.IC);
                                this.Params.SetItem<string>(0, "NazevDodavatele", oFD.FakturaDosla.HlavickaFD.Dodavatel.NazevDodavatele);

                                this.Params.SetItem<string>(0, "ICVytez", oFD.FakturaDosla.HlavickaFD.Dodavatel.IC);
                                this.Params.SetItem<string>(0, "NazevDodavateleVytez", oFD.FakturaDosla.HlavickaFD.Dodavatel.NazevDodavatele);

                                this.Params.SetItem<string>(0, "UcetDodavatele", oFD.FakturaDosla.HlavickaFD.Dodavatel.UcetDodavatele);
                                this.Params.SetItem<string>(0, "BankaDodavatele", oFD.FakturaDosla.HlavickaFD.Dodavatel.BankaDodavatele);
                                this.Params.SetItem<string>(0, "IBAN", oFD.FakturaDosla.HlavickaFD.Dodavatel.IBAN);
                                this.Params.SetItem<string>(0, "BIC", oFD.FakturaDosla.HlavickaFD.Dodavatel.BIC);

                                this.Params.SetItem<string>(0, "UcetDodavateleVytez", oFD.FakturaDosla.HlavickaFD.Dodavatel.UcetDodavatele);
                                this.Params.SetItem<string>(0, "BankaDodavateleVytez", oFD.FakturaDosla.HlavickaFD.Dodavatel.BankaDodavatele);
                                this.Params.SetItem<string>(0, "IBANVytez", oFD.FakturaDosla.HlavickaFD.Dodavatel.IBAN);
                                this.Params.SetItem<string>(0, "BICVytez", oFD.FakturaDosla.HlavickaFD.Dodavatel.BIC);

                                //this.Params.SetItem<string>(0, "Stavba", oFD.FakturaDosla.HlavickaFD.StavbaCislo);
                                //this.Params.SetItem<string>(0, "Utvar", oFD.FakturaDosla.HlavickaFD.Dodavatel.);
                                //this.Params.SetItem<string>(0, "Smlouva", oFD.FakturaDosla.HlavickaFD.SmlouvaCislo);
                                if(oFD.FakturaDosla.HlavickaFD.Zalohy != null)
                                {
                                    String zalohy = "";
                                    foreach (ZalohaFDESSS z in oFD.FakturaDosla.HlavickaFD.Zalohy)
                                    {
                                        zalohy += z.CastkaZalohy + "; ";
                                    }

                                    this.Params.SetItem<string>(0, "Zalohy", zalohy);
                                }

                                this.Params.SetAdditionalRelationParams("poradac_nazpor", "C(" + ClassNumbers.INVOICE_IN + ")");
                            }
                            catch (System.Xml.XmlException e)
                            {
                                if (DbTransaction.IsTransaction)
                                {
                                    DbTransaction.Current.SetComplete();
                                    DbTransaction.Current.End();
                                }

                                Message.Error("Soubor XML obsahuje chyby: " + e.Message);

                            }
                        }
                        else
                        {
                            Message.Warning("K došlé poště není připojeno žádné XML");
                        }
                        DbTransaction.Current.SetComplete();
                        DbTransaction.Current.End();

                    }
                }
            }
        }

        private Int32 rNumber;

        public void VytvoritFD()
        {
            try
            {
                DbTransaction.Current.Begin();

                Int32 folderNumber = this.Params.GetItemInt32(0, "poradac");

                using(INrsInstance newFD = NrsInstance.GetInstance(ClassNumbers.INVOICE_IN, folderNumber))
                {
                    QueryTemplate queryT = QueryManager.Instance.GetQuery("GetOrganizaceByIC");
                    queryT.ReplaceParametr("ico", oFD.FakturaDosla.HlavickaFD.Dodavatel.IC);
                    string organizace = SqlSelect.GetValue<string>(queryT.GetFinalQuery());

                    newFD.Reset();

                    String utvar = this.Params.GetItemString(0, "Utvar");
                    String stavba = this.Params.GetItemString(0, "Stavba");
                    Int32 smlouva = this.Params.GetItemInt32(0, "Smlouva");

                    newFD.Master.SetItem(0, "nazev_subjektu", oFD.FakturaDosla.CisloFaktury);
                    newFD.Master.SetItem(0, "poznamka", oFD.FakturaDosla.HlavickaFD.Poznamka);
                    newFD.Master.SetItem(0, "organizace", organizace);
                    newFD.Master.SetItem(0, "organizace_cispor", 50);
                    newFD.Master.SetItem(0, "utvar", utvar);
                    newFD.Master.SetItem(0, "obchodni_pripad", stavba);
                    newFD.Master.SetItem(0, "cislo_bankovni_spojeni", 45661955);

                    newFD.Master.SetItem(0, "datum_duzp", oFD.FakturaDosla.HlavickaFD.DUZP);
                    newFD.Master.SetItem(0, "datum_splatnosti", oFD.FakturaDosla.HlavickaFD.DatumSplatnosti);
                    //newFD.Master.SetItem(0, "datum_splatnosti", this.Params.GetItemString(0, "DatumPrijeti"));

                    newFD.MasterRelations.AddRelation(1701, smlouva, RelationSide.Left);

                    //TODO vytvoreni faktury dosle
                    //newFD.Master.SetItem(0, "CisloFaktury", this.Params.GetItemString(0, "CisloFaktury")); //like dis

                    newFD.Update();
                }

                using (INrsInstance esssInst = NrsInstance.GetInstance(ClassNumber, FolderNumber))
                {
                    esssInst.Retrieve(rNumber);

                    Int32 ps = 0;
                    if (esssInst.Master.GetItemString(0, "lcs_uda_dosla_posta_pocet_spusteni_vytvoritFD") != null)
                        ps = esssInst.Master.GetItemInt32(0, "lcs_uda_dosla_posta_pocet_spusteni_vytvoritFD");

                    esssInst.Master.SetItem(0, "lcs_uda_dosla_posta_pocet_spusteni_vytvoritFD", ++ps);
                    esssInst.Update();
                }

                DbTransaction.Current.SetComplete();
                DbTransaction.Current.End();
            }
            catch (Exception ex)
            {
                if (DbTransaction.IsTransaction)
                {
                    DbTransaction.Current.End();
                }
                Message.ErrorWithContext(ClassNumber, FolderNumber, rNumber, "Při pokusu o vytvoření faktury došlé došlo k chybě. Výpis chyby:" + ex.ToString());
            }
        }

        protected override void ItemButtonClicked(ItemButtonClickedEventArgs e)
        {
            if(e.ActualItem.Equals("btnNewIC"))
            {
                this.NoveIC();
            }
        }

        private void NoveIC()
        {
            using (INrsCowley exportDoGorion = NrsCowley.GetCowley(ClassNumbers.ORGANIZACE, "ZalozitOrganizaciZAres", FolderNumbers.ORGANIZATION, false))
            {
                exportDoGorion.Initialize(0, this);
                exportDoGorion.ParamsOK = true;
                exportDoGorion.Run();
                if(exportDoGorion.ResultSet != null && exportDoGorion.ResultSet.Length > 0)
                {
                    //TODO
                }
            }
        }

        protected override void SysItemButtonClicked(ItemButtonClickedEventArgs e)
        {
            if (e.ActualItem.Equals("Continue"))
            {
                bool valid = true;
                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "poradac_nazpor")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "poradac_nazpor", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "CisloFaktury")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "CisloFaktury", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "VSY")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "VSY", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "SSY")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "SSY", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "DatumSplatnosti")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "DatumSplatnosti", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "DatumPrijeti")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "DatumPrijeti", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "DUZP")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "DUZP", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "IC")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "IC", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "UcetDodavatele")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "UcetDodavatele", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "BankaDodavatele")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "BankaDodavatele", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "Stavba")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "Stavba_refer", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "Utvar")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "Utvar_refer", UI.NrsFontColor.Error);
                }

                if (String.IsNullOrEmpty(this.Params.GetItemString(0, "Smlouva")))
                {
                    valid = false;
                    this.Params.Form.CellFontAndColor(0, "Smlouva_refer", UI.NrsFontColor.Error);
                }

                if (valid) base.SysItemButtonClicked(e);
            }
            else base.SysItemButtonClicked(e);
        }

        private ObalkaFakturaDoslaESSS oFD;
    }
}
