using KonceptHK.HeliosGluon;
using Noris.KonceptHK.SpisovaSluzba.BaseCowleies;
using Noris.KonceptHK.SpisovaSluzba.Enums;
using Noris.KonceptHK.SpisovaSluzba.Zpravy;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZpracovaniImportuFaDESSSCowley : BaseESSSZpracovaniImportuCowley
    {
        public void ZpracovaniImportuFaDESSS()
        {
            if (this.RecordNumbers != null && this.RecordNumbers.Count > 0)
            {
                for (int i = 0; i < this.RecordNumbers.Count; i++)
                {
                    Int32 cisloSubjektu = this.RecordNumbers[i];
                    try
                    {
                        //jeden hlavni velky zacatek transakce
                        DbTransaction.Current.Begin();

                        using (INrsInstance vyskytXmlZpravy = NrsInstance.GetInstance(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_SYMBASIS))
                        {
                            vyskytXmlZpravy.Retrieve(cisloSubjektu);

                            //kontrola opravneni zpracovani - vyhazuje EXCEPTION - nezpracovavat jiz zpracovane (maji stav Zpracovani aplikaci, nebo vazbu na EVLS)
                            this.KontrolaPripustnostiZpracovani(vyskytXmlZpravy);

                            // vlastni zpracovani vstupniho XML
                            this.Zpracovani(vyskytXmlZpravy);

                            //na zprave se nastavi stav zpracovani na USPECH
                            vyskytXmlZpravy.Master.SetItem(0, "stav", (Int32)StavXmlZpravy.ZPRACOVANO_APLIKACI);
                            vyskytXmlZpravy.Update();
                        }

                        //finalni commit transakce
                        DbTransaction.Current.SetComplete();
                        DbTransaction.Current.End();

                        Message.InfoWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_ESSS, cisloSubjektu, "Úspěšné provedení zpracování importu smlouvy ze Symbasis.");
                    }
                    catch (Exception e)
                    {
                        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                        // pri chybe se nesmi nastavit stav zpracovani na chybu, muze se jednat o chybu ze zprava jiz byla jednou zpracovana //
                        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                        //rollback transakce a vyhozeni vyjimky dal
                        if (DbTransaction.IsTransaction)
                            DbTransaction.Current.End();

                        Message.ErrorWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_ESSS, cisloSubjektu, e, "Chyba zpracování importu FaD ze ESSS.");
                        throw e;
                    }
                }
            }
            else
            {
                Message.Error("Funkce ZpracovaniImportuFaDESSS nemuze byt pustena, nema predan identifikator Vyskytu smluv.");
            }
        }

        /// <summary>
        /// Metoda provede zpracovani importu smlouvy
        /// </summary>
        protected override void Zpracovani(INrsInstance vyskytXmlZpravy)
        {
            Int32 idXmlZpravy = vyskytXmlZpravy.Master.GetItemInt32(0, "cislo_subjektu");

            //nacte se XML zprava a vytvori se objekt generujici XML Reader pro XSD validaci a nacitani.
            ServiceGateFunctionUserData inputData = new ServiceGateFunctionUserData();
            inputData.SetXml(vyskytXmlZpravy.Master.GetItemString(0, "text"));

            ////////////////////////////////////
            // provede se validace pomoci XSD //
            ////////////////////////////////////
            // base.validaceXmlPomociXsd(inputData, idXmlZpravy, "SymbasisSmlouvyImport.xsd");
            base.ValidaceXmlPomociXsd(inputData, idXmlZpravy, "K81PrijemFD.xsd");

            //////////////////////////////////////////////////////////////////////////
            // konce validace, zacina nacitani objektu a dotahovani podle referenci //
            //////////////////////////////////////////////////////////////////////////
            ObalkaFakturaDoslaESSS fad;
            try
            {
                fad = new ObalkaFakturaDoslaESSS(inputData.CreateXmlFragmentReader(), idXmlZpravy);
            }
            catch (Exception e)
            {
                Message.ErrorWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_ESSS, idXmlZpravy, e, "Chyba načítání XML zprávy do objektu! Zpráva:" + (String.IsNullOrEmpty(e.Message) ? "zpráva je prázdná" : e.Message));
                throw e;
            }

            this.ValidaceZakladnichParametruFAD(fad, idXmlZpravy);

            ////uvodni validace - overeni na duplicitu reference SymbasisID - nelze predavat 2x stejnou smlouvu
            ////rmbug://RSD-3514 kontrola je uvedena jeste tesne pred prvnim volanim update
            //base.kontrolaImportovanychSymbasisID(evls, idXmlZpravy);

            //// overeni, jestli se shoduje symbasis id a reference HeG (rezervovane_cislo) spolu s nove importovanou smlouvou
            //this.kontrolaSpravnostiReferenceHeG(evls, idXmlZpravy);

            ////doctou se objekty podle referenci, zkontroluje se, jestli se neodkazuje na objekt ktery v DB neni

            //try
            //{
            //    Message.Info("Načítám objekty dle referencí");
            //    fad.nacteniObjektuPodleReferenci(vyskytXmlZpravy);
            //}
            //catch (Exception e)
            //{
            //    Message.ErrorWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_SYMBASIS, idXmlZpravy, e, "Chyba načítání objektů podle referencí! " + e.Message);
            //    throw e; //pouze formalni, vyhozeni chyby udela uz Message.Error
            //}

            //////////////////////////////////////////////////////////////////////////////
            //// v tomto bode je vsechno v poradku a nactene, muze se zapocist ukladani //
            //////////////////////////////////////////////////////////////////////////////

            //this.CheckContractByType(evls);

            this.ZalozitNovouFad(fad, idXmlZpravy);
        }

        private void ValidaceZakladnichParametruFAD(ObalkaFakturaDoslaESSS fad, Int32 idXmlZpravy)
        {
            //nejake overeni zde
            if (String.IsNullOrEmpty(fad.TypDokumentu))
            {
                Message.ErrorWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_ESSS, idXmlZpravy, "Chybná vstupní zpráva. Nebyl správně uveden element:TypDokumentu");
            }
        }

        private void ZalozitNovouFad(ObalkaFakturaDoslaESSS fad, Int32 idXmlZpravy)
        {

            throw new NotImplementedException();

            //using (INrsInstance fadInst = NrsInstance.GetInstance(ClassNumbers.INVOICE_IN, 123456))
            //{
            //    fadInst.Reset();
            //    //fadInst.Master.SetRelation(0, "zakazka_vr", zakazkaVrId);
            //    fadInst.Update();
            //}
        }
    }
}
