using KonceptHK.HeliosGluon.Queries;
using Noris.LCS.Base;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noris.KonceptHK.SpisovaSluzba.YSService;
using System.ServiceModel;
using System.ServiceModel.Security;
using KonceptHK.HeliosGluon;
using System.Xml.Linq;
using KonceptHK.HeliosGluon.Utils;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZverejnitSmlouvuCowley : NrsCowley
    {
        private const Int32 MAIL_EXTERNAL_DOCUMENT_DV = 108174;
        private const Int32 IDDS_DV = 102619;
        private const String ZVEREJNENI_XML_NAME = "zverejneni";
        private YSServiceClient _ysClient;
        private String _currentConnection;
        private Boolean _isTest;
        public Boolean DebugMode { get; set; } = false;


        public ZverejnitSmlouvuCowley()
            : base()
        {
            this._isTest = true;
            this._currentConnection = String.Empty;
        }

        protected override void Init(NrsCowley.InitEventArgs e)
        {
            base.Init(e);

            if (this.RecordNumbers == null || this.RecordNumbers.Count == 0)
                Message.Error("SSnoRecordsError");

            this._currentConnection = Current.Session["ProfileDB"].ToString();

#if DEBUG
            this._currentConnection = Current.Session["ProfileDB"].ToString() + "_test";
#else
            this._currentConnection = Current.Session["ProfileDB"].ToString();
#endif

            this.Progress.Init("Zveřejnit smlouvu", 0, this.RecordNumbers.Count, false);

            QueryTemplate debug = QueryManager.Instance.GetQuery("getIsDebugMode");
            DebugMode = SqlSelect.GetString(debug.GetFinalQuery()) == "A";
        }

        public void ZverejnitSmlouvu()
        {
            this.ResolveConnection();
            Boolean loggedId = this.LoginWS();

            if (loggedId)
            {
                //odeslani dat
                if (this._ysClient != null && this._ysClient.State != System.ServiceModel.CommunicationState.Closed)
                {
                    if (this.RecordNumbers.Length == 1 && this.Instance != null)
                    {
                        if (!(this.Instance is OdchoziPostaInst))
                            Message.Error("Funkci lze spustit pouze nad záznamem odchozí pošty");
                        else
                            this.ProcessInstance((OdchoziPostaInst)this.Instance);
                    }
                    else
                    {
                        foreach (Int32 id in this.RecordNumbers)
                        {
                            using (INrsInstance instance = NrsInstance.GetInstance(this.ClassNumber, this.FolderNumber))
                            {
                                instance.Retrieve(id);
                                if (!(instance is OdchoziPostaInst))
                                    Message.Error("Funkci lze spustit pouze nad záznamem odchozí pošty");
                                else
                                    this.ProcessInstance((OdchoziPostaInst)instance);
                            }
                        }
                    }
                }
            }

            //zavreni pripojeni
            this.LogOffWS();
        }

        private void ResolveConnection()
        {
            Message.Info("Aktivní HeG spojení: " + this._currentConnection);
            if (!this._currentConnection.ToLower().Contains("test"))
            {
                Message.Info("Přihlášení pro ostrou verzi.");
                this._isTest = false;
            }
            else
            {
                Message.Info("Přihlášení pro testovací verzi.");
                this._isTest = true;
            }
        }

        private Boolean LoginWS()
        {
            //vytvoreni pripojeni
            Message.Info("Vytvářím připojení k WS");
            try
            {
                //nactu prihlasovaci udaje z parametru koncept
                String endpointUrl = null;
                String userName = null;
                String userPassword = null;
                if (this._isTest)
                {
                    endpointUrl = Noris.KonceptHK.KonceptHK.Service.BaseService.GetHodnotuParametryKoncept(KonceptParams.TEST_YOUR_SYSTEM_SPISOVKA_URL_TO_WS);
                    userName = Noris.KonceptHK.KonceptHK.Service.BaseService.GetHodnotuParametryKoncept(KonceptParams.TEST_YOUR_SYSTEM_SPISOVKA_LOGIN_TO_WS);
                    userPassword = Noris.KonceptHK.KonceptHK.Service.BaseService.GetNativPasswordHodnotuParametryKoncept(KonceptParams.TEST_YOUR_SYSTEM_SPISOVKA_PASSWORD_TO_WS);

                }
                else
                {
                    endpointUrl = Noris.KonceptHK.KonceptHK.Service.BaseService.GetHodnotuParametryKoncept(KonceptParams.YOUR_SYSTEM_SPISOVKA_URL_TO_WS);
                    userName = Noris.KonceptHK.KonceptHK.Service.BaseService.GetHodnotuParametryKoncept(KonceptParams.YOUR_SYSTEM_SPISOVKA_LOGIN_TO_WS);
                    userPassword = Noris.KonceptHK.KonceptHK.Service.BaseService.GetNativPasswordHodnotuParametryKoncept(KonceptParams.YOUR_SYSTEM_SPISOVKA_PASSWORD_TO_WS);
                }

                if (String.IsNullOrEmpty(endpointUrl) || String.IsNullOrEmpty(userName) || String.IsNullOrEmpty(userPassword))
                    throw new Exception("Nepodařilo se načíst přihlašovací údaje k WS.");

                /*příklad od YS
                Nemuzu pouzit app.config => nefunguje u class library
                <binding name="WSHttpBinding_IYSService" closeTimeout="00:01:00"
                    openTimeout="00:01:00" receiveTimeout="00:10:00" sendTimeout="00:01:00"
                    bypassProxyOnLocal="false" transactionFlow="false" hostNameComparisonMode="StrongWildcard"
                    maxBufferPoolSize="524288" maxReceivedMessageSize="65536"
                    messageEncoding="Mtom" textEncoding="utf-8" useDefaultWebProxy="true"
                    allowCookies="false">
                    <readerQuotas maxDepth="32" maxStringContentLength="8192" maxArrayLength="16384"
                        maxBytesPerRead="4096" maxNameTableCharCount="16384" />
                    <reliableSession ordered="true" inactivityTimeout="00:10:00"
                        enabled="false" />
                    <security mode="Message">
                        <transport clientCredentialType="Windows" proxyCredentialType="None"
                            realm="" />
                        <message clientCredentialType="UserName" negotiateServiceCredential="true"
                            algorithmSuite="Default" />
                    </security>
                </binding>
                */

                //musim takto ... app.config nefunguje
                WSHttpBinding binding = new WSHttpBinding();
                binding.Name = "WSHttpBinding_IYSService";
                binding.MessageEncoding = WSMessageEncoding.Mtom;

                binding.TextEncoding = Encoding.UTF8;
                binding.UseDefaultWebProxy = true;
                binding.AllowCookies = false;
                binding.HostNameComparisonMode = HostNameComparisonMode.StrongWildcard;
                binding.BypassProxyOnLocal = false;
                binding.TransactionFlow = false;
                binding.MaxBufferPoolSize = 524288;
                binding.MaxReceivedMessageSize = 65536;

                binding.Security.Message.NegotiateServiceCredential = true;
                binding.Security.Message.AlgorithmSuite = SecurityAlgorithmSuite.Default;
                binding.Security.Mode = SecurityMode.Message;
                binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
                binding.Security.Transport.ProxyCredentialType = HttpProxyCredentialType.None;
                binding.Security.Transport.Realm = String.Empty;

                binding.ReliableSession.Ordered = true;
                binding.ReliableSession.InactivityTimeout = new TimeSpan(0, 10, 0);
                binding.ReliableSession.Enabled = false;

                binding.ReaderQuotas.MaxDepth = 32;
                binding.ReaderQuotas.MaxStringContentLength = 8192;
                binding.ReaderQuotas.MaxArrayLength = 16384;
                binding.ReaderQuotas.MaxBytesPerRead = 4096;
                binding.ReaderQuotas.MaxNameTableCharCount = 16384;

                binding.OpenTimeout = new TimeSpan(0, 1, 0);
                binding.CloseTimeout = new TimeSpan(0, 1, 0);
                binding.SendTimeout = new TimeSpan(0, 1, 0);
                binding.ReceiveTimeout = new TimeSpan(0, 10, 0);

                EndpointAddress endpoint = new EndpointAddress(new Uri(endpointUrl), EndpointIdentity.CreateDnsIdentity("ISRS"));

                //tvorba konektoru
                this._ysClient = new YSServiceClient(binding, endpoint);
                this._ysClient.ClientCredentials.UserName.UserName = userName;
                this._ysClient.ClientCredentials.UserName.Password = userPassword;
                this._ysClient.ClientCredentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.None;

                this._ysClient.Open();
                Message.Info("Připojení vytvořeno");
                //mozna se musi zavolat open
            }
            catch (Exception ex)
            {
                Message.Error("Nepodařilo se ukončit spojení s webovou službou: " + ex.Message);
                return false;
            }

            return true;
        }

        private void LogOffWS()
        {
            if (this._ysClient != null && this._ysClient.State != System.ServiceModel.CommunicationState.Closed)
            {
                try
                {
                    Message.Info("Ohlašuji se od WS");
                    this._ysClient.Close();
                    Message.Info("Odhlášení proběho úspěšně");
                }
                catch (Exception ex)
                {
                    Message.Error("Nepodařilo se ukončit spojení s webovou službou: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Metoda provede odeslani dat z instance odchozi posty
        /// </summary>
        private void ProcessInstance(OdchoziPostaInst instance)
        {
            try
            {
                //result
                Boolean success = false;
                String message = null;

                //kontrola jestli uz nebyla zprava odeslana
                if (DebugMode == true) Message.Info("Provádím validaci");
                this.ValidInstance(instance);

                try
                {
                    //muze nastat napr. timeout nebo jiny podobny problem
                    try
                    {
                        String id_odeslane_zpravy = instance.RecordNumber.ToString();
                        if (DebugMode == true) Message.Info("Připojuji dokumenty");
                        List<Attachment> attachements = this.GetAttachementsByInstance(instance);
                        Attachment[] attachementsArray = new Attachment[] { };
                        Attachment xml = this.GetContractXmlByInstance(instance); //zde je file_name_xml, content_xml

                        //kontroly
                        if (xml == null || String.IsNullOrEmpty(xml.FileName) || String.IsNullOrEmpty(xml.FileContent))
                            throw new Exception("Odchozí pošta neobsahuje prováděcí XML soubor.");

                        if (xml.FileName.ToLower().StartsWith(ZVEREJNENI_XML_NAME))
                        {
                            Message.Info(String.Format("Odchozí zpráva je typu \"{0}\". Proběhne kontrola připojených dokumentů.", ZVEREJNENI_XML_NAME));

                            if (attachements == null || attachements.Count == 0)
                                throw new Exception("Odchozí pošta neobsahuje přílohy.");
                            else
                                Message.Info(String.Format("Odchozí pošta obsahuje {0} dokumentů.", attachements.Count));
                        }
                        else
                        {
                            Message.Info(String.Format("Odchozí zpráva není typu \"{0}\". Zpráva nemusí obsahovat připojené dokumenty.", ZVEREJNENI_XML_NAME));
                        }
                        if (DebugMode == true) Message.Info("Editace navázaneho záznamu");
                        this.editXmlNavazZaznam(xml, instance.RecordNumber);
                        if (DebugMode == true) Message.Info("Uložení xml na instanci");
                        this.SetContractXmlToInstance(xml, instance);

                        if (attachements != null && attachements.Count > 0)
                            attachementsArray = attachements.ToArray();


                        //samotné volání WS YS
                        //WS YS ma obsahovat parametry: file_name_xml, content_xml, attachments, id_odeslane_zpravy                        

                        Noris.KonceptHK.SpisovaSluzba.YSService.PublishContractDataResponse response = this._ysClient.PublishContractData(xml.FileName, xml.FileContent, attachementsArray, id_odeslane_zpravy);

                        if (response == null)
                            throw new Exception("Odpověď webové služby je prázdná!");

                        success = response.Success;
                        message = response.Message;
                        if (DebugMode == true) Message.Info("Konec procesu na instanci");
                        if (!success)
                        {
                            if (String.IsNullOrEmpty(message))
                                throw new Exception("Spisové službě se nepodařilo zpracovat zprávu.");
                            else throw new Exception(message);
                        }
                        else
                        {
                            Message.InfoWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Data smlouvy byla úspěšně zveřejněna.");
                        }
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        message = ex.Message;

                        Message.Error(message); //aby se zapsala chyba
                    }
                }
                catch
                {
                    //nic jen potrebuju zapsat message error a zapsat duvod do UDA a zpracovat dalsi zpravu
                }

                DbTransaction.Current.Begin();

                this.WriteResultToInstance(instance, success, message);

                DbTransaction.Current.SetComplete();
                DbTransaction.Current.End();
            }
            catch (Exception ex)
            {
                if (DbTransaction.IsTransaction)
                {
                    DbTransaction.Current.End();
                }

                //nepodarilo se zapsat odpoved do instance ... ostatni je ve vnitrni try catch 
                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Nepodařilo se odeslat data zveřejnění smlouvy: " + ex.Message);
            }
            finally
            {
                this.Progress.DoProgress(this.Progress.Value + 1);
            }
        }

        /// <summary>
        /// Metoda doplni elemnt navazanyZaznam v pripade, ze navazana smlouva je typu provadeci
        /// Ziska ID ramcove smlouvy navazane na provadeci smlouve
        /// </summary>
        private void editXmlNavazZaznam(Attachment xml, Int32 recordNumber)
        {
            //Message.Info("Edituji xml");
            QueryTemplate queryT = QueryManager.Instance.GetQuery("GetSmlouvuOdchoziPosty");
            queryT.ReplaceParametr("subj", recordNumber);
            Datastore ds = SqlSelect.CreateDatastore(queryT.GetFinalQuery());
            if (ds.Retrieve() > 0)
            {
                //nejprve ziskame xml ve stringu
                byte[] fileBytes = Convert.FromBase64String(xml.FileContent);
                String xmlString = Encoding.UTF8.GetString(fileBytes);
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
                            Int32Array i;
                            elsInst.MasterRelations.GetRelations(2300329, RelationSide.Left, out i);
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
                        Int32Array clenoveSdruzeni;
                        elsInst.MasterRelations.GetRelations(10542, RelationSide.Left, out clenoveSdruzeni);
                        Message.Info(clenoveSdruzeni.Length + " členů");
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
                                        Message.Info("Root není null");
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
                        Int32Array clenoveSdruzeni = new Int32Array();
                        elsInst.MasterRelations.GetRelations(10543, RelationSide.Left, out clenoveSdruzeni);
                        Message.Info(clenoveSdruzeni.Length + " členů");
                        if (clenoveSdruzeni.Count() > 0)
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
                fileBytes = Encoding.UTF8.GetBytes(xmlString);
                xml.FileContent = Convert.ToBase64String(fileBytes);
            }
            else
            {
                Message.Warning("Nebyla dohledána smlouva odchozí pošty");
            }
        }

        /// <summary>
        /// Metoda vraci seznam objektu priloh pro zverejneni
        /// Dokumenty se nacitaji z dv 108174 
        /// </summary>
        private List<Attachment> GetAttachementsByInstance(OdchoziPostaInst instance)
        {
            List<Attachment> result = null;

            Int32Array ids = null;
            Int32 count = instance.MasterRelations.GetRelations(MAIL_EXTERNAL_DOCUMENT_DV, RelationSide.Right, out ids);
            if (count > 0)
            {
                result = new List<Attachment>();

                foreach (Int32 item in ids)
                {
                    ExternalDocumentItem edi = Noris.Srv.ExternalDocument.GetContent(item);
                    Attachment a = new Attachment();
                    a.FileName = edi.DocumentPath;

                    Message.Info("Přidávám ke zprávě soubor: " + a.FileName);

                    a.FileContent = Convert.ToBase64String(edi.Content);

                    result.Add(a);
                }
            }

            count = instance.MasterRelations.GetRelations(105627, RelationSide.Right, out ids);
            if (count > 0)
            {
                if(result == null)
                    result = new List<Attachment>();

                foreach (Int32 item in ids)
                {
                    using (EdmDocInstance edmInst = (EdmDocInstance)NrsInstance.GetInstance(1132))
                    {
                        edmInst.Retrieve(item);
                        var content = edmInst.GetContent(EdmDocWorkingVersion.Valid);

                        Attachment a = new Attachment();
                        a.FileName = edmInst.Master_PhysicalName;

                        Message.Info("Přidávám ke zprávě soubor: " + a.FileName);

                        a.FileContent = Convert.ToBase64String(content);

                        result.Add(a);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Metoda vraci objekt s xml souborem pro zverejneni (nazev a obsah)
        /// Dokumenty se ze vztahu zprava (priloha)
        /// </summary>
        private Attachment GetContractXmlByInstance(OdchoziPostaInst instance)
        {
            Attachment result = null;
            if (instance.AttachmentCollection != null && instance.AttachmentCollection.Count > 0)
            {
                foreach (MailAttachmentData item in instance.AttachmentCollection)
                {
                    if (item != null && !String.IsNullOrEmpty(item.Master_DocumentName) && item.Master_DocumentName.ToLower().EndsWith(".xml"))
                    {
                        result = new Attachment();
                        result.FileName = item.Master_DocumentName;
                        result.FileContent = Convert.ToBase64String(item.Content);
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Opak metody GetContractXmlByInstance
        /// </summary>
        private void SetContractXmlToInstance(Attachment xml, OdchoziPostaInst instance)
        {
            //Message.Info("Ukládám xml");
            if (instance.AttachmentCollection != null && instance.AttachmentCollection.Count > 0)
            {
                foreach (MailAttachmentData item in instance.AttachmentCollection)
                {
                    if (item != null && !String.IsNullOrEmpty(item.Master_DocumentName) && item.Master_DocumentName.ToLower().EndsWith(".xml"))
                    {
                        item.Content = Convert.FromBase64String(xml.FileContent);
                        item.Update();
                        //Message.Info("Xml uloženo");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Metoda zapise odpoved do instance odchozi posty
        /// resp do UDA atributu
        /// Metoda provadi UPDATE
        /// </summary>
        private void WriteResultToInstance(OdchoziPostaInst instance, Boolean success, String message)
        {
            instance.Master.SetItem(0, "lcs_uda_odchozi_posta_zpracovano_spisovou_sluzbou", success ? "A" : "N");
            instance.Master.SetItem(0, "lcs_uda_odchozi_posta_chybove_hlaseni_ss", message);

            //Vývoj #33896
            if (success)
            {
                instance.Master.SetItem(0, "stav", 2);
            }

            instance.Update();
        }

        /// <summary>
        /// Metoda validuje instanci odchozi posty zdali je mozne poslat data do spisove sluzby
        /// </summary>
        private void ValidInstance(OdchoziPostaInst instance)
        {
            Boolean isAlreadySent = instance.Master.GetItemString(0, "lcs_uda_odchozi_posta_zpracovano_spisovou_sluzbou", String.Empty).Equals("A");
            if (isAlreadySent)
                Message.Error("Zpráva už byla spisové službě předána.");

            //kontrola jestli neni zprava rozdelena (podrizena jine zprave) a jestli hl. zprava ma IDDS
            Int32? mainMessage = instance.Master.GetItem<Int32?>(0, "hlavni_dokument_zverejneni");
            if (mainMessage.HasValue)
            {
                Message.Info("Funkce zpracovává podřízenou zprávu.");

                Int32Array ids = null;
                Int32 idsCount = SqlSelect.GetRecordsForRelation(IDDS_DV, mainMessage.Value, RelationSide.Left, out ids);

                if (idsCount == 0)
                {
                    Message.Error("Nadřazené zprávě nebylo přiděleno ID odeslané zprávy!");
                }
            }
        }

        //tuto tridu nahradit skutecnou tridou pro prilohu z WS YS
        //private class Attachement
        //{
        //    public String FileName { get; set; }
        //    public String FileContent { get; set; }
        //    public Attachement()
        //    {

        //    }
        //}
    }
}
