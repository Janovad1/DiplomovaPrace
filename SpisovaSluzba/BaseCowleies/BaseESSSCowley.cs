using KonceptHK.HeliosGluon;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace Noris.KonceptHK.SpisovaSluzba.BaseCowleies
{
    public class BaseESSSCowley : NrsCowley
    {
        /// <summary>
        /// provede se validace pomoci XSD
        /// </summary>
        protected void ValidaceXmlPomociXsd(ServiceGateFunctionUserData inputData, Int32 idXmlZpravy, String xsdFileName)
        {
            String xsdPath = Noris.Srv.Files.GetPhysicalPath(String.Empty) + "XSD\\" + xsdFileName;
            Message.Info("Cesta k XSD souboru: " + xsdPath);
            System.Xml.Schema.XmlSchema xsdSchema = System.Xml.Schema.XmlSchema.Read(new XmlTextReader(xsdPath), new System.Xml.Schema.ValidationEventHandler(ValReader_ValidationEventHandler));

            //validase se provadi pomoci XmlReaderSettings
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.ConformanceLevel = ConformanceLevel.Auto; //nutne, pokud validatoru davame
            settings.Schemas.Add(xsdSchema);
            settings.ValidationEventHandler += new ValidationEventHandler(ValReader_ValidationEventHandler);

            XmlReader xmlReader = inputData.CreateXmlFragmentReader();
            XmlReader valReader = XmlReader.Create(xmlReader, settings);
            //vlastní validace, projede cely vstup
            try
            {
                while (valReader.Read()) { }
            }
            catch(Exception ex)
            {
                if(DbTransaction.IsTransaction)
                {
                    DbTransaction.Current.SetComplete();
                    DbTransaction.Current.End();
                }
                Message.ErrorWithContext(ClassNumbers.VYSKYT_XML_ZPRAVY, FolderNumbers.IMPORT_ESSS, idXmlZpravy, "Chyba validace vstupní zprávy oproti XSD! " + ex.Message);
            }
        }

        /// <summary>
        /// interni pomocna metoda pro registraci delagata u validatoru xml podle xsd, vola se v pripade chyby a pouze vyhodi chybu
        /// </summary>
        protected static void ValReader_ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            Message.Error(e.Message == null ? "Zpráva chyby validace neuvedena." : e.Message);
            throw new Exception(); //pouze formalni, vyhozeni chyby udela uz Message.Error
        }
    }
}
