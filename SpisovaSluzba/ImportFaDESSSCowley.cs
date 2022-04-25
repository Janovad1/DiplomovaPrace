using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.KonceptHK.SpisovaSluzba.BaseCowleies;
using Noris.KonceptHK.SpisovaSluzba.Enums;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ImportFaDESSSCowley : BaseESSSImportCowley
    {
        protected Int32 DOSLA_POSTA_ESSS_FOLDER = 2300309;
        protected Int32 DOSLA_POSTA_ESSS_XML_ZPRAVA_DV = 2300822;
        public void ImportFaDZESSS()
        {

            //////////////////////////////////////
            //////////////////////////////////////
            // blok na podvrhnuti prijmu zpravy //
            //////////////////////////////////////
            //////////////////////////////////////

            ///////////////////////// Zprava pro IMPORT FaD z ESSS ///////////////////////////////
#if DEBUG
            ServiceGateFunctionUserData ServiceGateInputUserData = new ServiceGateFunctionUserData();

            String TestMessage = "<Obalka> <TypDokumentu>test string</TypDokumentu> <CisloJednaci>test string</CisloJednaci> <SpisovaZnacka>test string</SpisovaZnacka> <IDSpisu>test string</IDSpisu> <Podatelna>test string</Podatelna> <CarovyKod>test string</CarovyKod> <FakturaDosla> <CisloFaktury>test string</CisloFaktury> <VSY>test string</VSY> <SSY>test string</SSY> <Prijemce> <NazevPrijemce>test string</NazevPrijemce> <UlicePrijemce>test string</UlicePrijemce> <MestoPrijemce>test string</MestoPrijemce> <PSCPrijemce>test string</PSCPrijemce> </Prijemce> <HlavickaFD> <DatumPrijeti>20161129</DatumPrijeti> <DUZP>20161129</DUZP> <DatumSplatnosti>20161129</DatumSplatnosti> <TabulkaDPH> <Zaklad>123.456</Zaklad> <Valorizace>123.456</Valorizace> <SazbaDPH>123.456</SazbaDPH> <DPH>123.456</DPH> <CelkemSazba>123.456</CelkemSazba> </TabulkaDPH> <Zaokrouhleni>123.456</Zaokrouhleni> <CelkemFaktura>123.456</CelkemFaktura> <KUhrade>123.456</KUhrade> <Zalohy> <Zaloha> <CastkaZalohy>123.456</CastkaZalohy> </Zaloha> <Zaloha> <CastkaZalohy>123.456</CastkaZalohy> </Zaloha> <Zaloha> <CastkaZalohy>123.456</CastkaZalohy> </Zaloha> </Zalohy> <CisloZFD>test string</CisloZFD> <Dodavatel> <IC>test string</IC> <DIC>test string</DIC> <NazevDodavatele>test string</NazevDodavatele> <UcetDodavatele>test string</UcetDodavatele> <BankaDodavatele>bank</BankaDodavatele> <IBAN>test string</IBAN> <BIC>test string</BIC> </Dodavatel> <StavbaCislo>test string</StavbaCislo> <Poznamka>test string</Poznamka> <SmlouvaCislo>test string</SmlouvaCislo> </HlavickaFD> <Polozka> <Predmet>test string</Predmet> <Mnozstvi>123.456</Mnozstvi> <ZakladPolozka>123.456</ZakladPolozka> <SazbaDPHPolozka>123.456</SazbaDPHPolozka> <DPHPolozka>123.456</DPHPolozka> <CelkemPolozka>123.456</CelkemPolozka> </Polozka> <Dokument>test string</Dokument> </FakturaDosla> </Obalka>";
            ServiceGateInputUserData.SetXml(TestMessage);
#endif
            //////////////////////////////////////
            //////////////////////////////////////
            //////////////////////////////////////
            //////////////////////////////////////
            if (ServiceGateInputUserData == null || String.IsNullOrEmpty(ServiceGateInputUserData.GetXml()))
            {
                // pokud neni zadna vstupni zprava, jedna se o zasadni chybu
                Message.Error("Chyba importu FaD. Nebyla předána vstupní zpráva/data.");
                return; //Message.Error vyhodi chybu, return je zde pouze formalne, pro jistotu, nesmi se pokracovat
            }
            ServiceGateInputUserData.SetXml(System.Net.WebUtility.HtmlDecode(ServiceGateInputUserData.GetXml()));

            using (INrsInstance vyskytXmlZpravy = this.ZalogovatXMLZpravu(ServiceGateInputUserData, FolderNumbers.IMPORT_ESSS, (Int32)TypeImportESSS.IMPORT_FAD))
            {
                try
                {
                    Int32 idXmlZpravy = vyskytXmlZpravy.Master.GetItemInt32(0, "cislo_subjektu");

                    this.ProcessImoprtESSS(vyskytXmlZpravy, ServiceGateInputUserData);

                    //stav na zpracovano
                    vyskytXmlZpravy.Master.SetItem(0, "stav", (Int32)StavXmlZpravy.ZPRACOVANO_APLIKACI);
                    vyskytXmlZpravy.Update();

                    try
                    {
                        this.CreateResultForESSS(idXmlZpravy);
                    }
                    catch (Exception e)
                    {
                        Message.ErrorWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_SYMBASIS, idXmlZpravy, e, "Chyba vytváření výstupu.");
                        throw e;
                    }
                }
                catch (Exception e)
                {
                    //zmenit stav xml zpravy na chybu a probublat chybu dal...
                    vyskytXmlZpravy.Master.SetItem(0, "stav", (Int32)StavXmlZpravy.CHYBA);
                    vyskytXmlZpravy.Update();

                    throw e;
                }
            }
        }

        /// <summary>
        /// Metoda validuje xml zpravu a zalozi zaznam v dosle poste + navaze dv
        /// </summary>
        private void ProcessImoprtESSS(INrsInstance vyskytXmlZpravy, ServiceGateFunctionUserData inputData)
        {

            //kontrola xsd
            base.ValidaceXmlPomociXsd(inputData, vyskytXmlZpravy.RecordNumber, "K81PrijemFD.xsd");

            //zalozeni zaznam v dosle poste
            using (INrsInstance postaInst = NrsInstance.GetInstance(ClassNumbers.DOSLA_POSTA, DOSLA_POSTA_ESSS_FOLDER))
            {
                postaInst.Reset();

                //provazani xml
                postaInst.MasterRelations.AddRelation(DOSLA_POSTA_ESSS_XML_ZPRAVA_DV, vyskytXmlZpravy.RecordNumber, RelationSide.Left);

                postaInst.Update();
            }            
        }        

        /// <summary>
        /// Metoda vytvari XML fragment s odpoved do ESSS
        /// </summary>
        protected override void CreateResultForESSS(Int32 idXmlZpravy)
        {
            Boolean success = false;

            using (INrsInstance instXmlZprava = NrsInstance.GetInstance(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_ESSS))
            {
                instXmlZprava.Retrieve(idXmlZpravy);

                if (instXmlZprava != null && (instXmlZprava.Master.GetItemInt32(0, "stav") == (Int32)StavXmlZpravy.ZPRACOVANO_APLIKACI))
                {
                    success = true;
                }
                else
                {
                    Message.Warning("Nepodařilo se provést import FaD z ESSS!");
                }
            }

            if (ServiceGateOutputUserData == null)
            {
                ServiceGateOutputUserData = new ServiceGateFunctionUserData();
            }

            using (XmlWriter writer = ServiceGateOutputUserData.CreateXmlFragmentWriter())
            {
                if (success)
                {
                    // vratit se ma pouze prazdna zprava, neodkomentovavat, jen kdyby se nekdy menila vystupni zprava
                    //writer.WriteElementString("STATE", "SUCCESS");
                }
            }
        }
    }
}