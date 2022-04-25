using KonceptHK.HeliosGluon;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.LCS.Base;
using Noris.LCS.Helios.Common;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class UpravitDokumentKOCowley : NrsCowley
    {
        private const int DMS_RELATION = 112340;
        private const int DMS_COPY_RELATION = 2301056;
        Int32Array doslaPosta;
        Int32Array odchoziPosta;
        public void UpravitDokumentKO()
        {
            foreach (var record in RecordNumbers)
            {
                using (INrsInstance fd = NrsInstance.GetInstance(46))
                {
                    fd.Retrieve(record);
                    fd.MasterRelations.GetRelations(1399, RelationSide.Left, out doslaPosta);
                    fd.MasterRelations.GetRelations(2301043, RelationSide.Left, out odchoziPosta);
                }

                //smažu si z kolekce záznamy začínající na auditní stopa
                DeleteRecordsAuditStopa();
            }
        }

        private void DeleteRecordsAuditStopa()
        {
            foreach (var op in odchoziPosta.ToList())
                if (SqlSelect.GetName(op).StartsWith("Auditní stopa")) odchoziPosta.RemoveValue(op);
        }

        protected override void Done(DoneEventArgs e)
        {
            Int32Array result = new Int32Array();
            if (doslaPosta.Count > 0)
            {
                using (PridelenySeznamESSSCwl cowley = (PridelenySeznamESSSCwl)NrsCowley.GetCowley(ClassNumbers.DOSLA_POSTA, "PridatSouvisejiciDokESSS", 2300309, false))
                {
                    cowley.Initialize(doslaPosta.First(), this);
                    cowley.SetParamsPridatSouvisejici("Související dokument", 2, 1);
                    cowley.RunDialog();
                    result = cowley.ResultSet;
                }
            }
            else if (odchoziPosta.Count() > 0)
            {
                using (PridelenySeznamESSSCwl cowley = (PridelenySeznamESSSCwl)NrsCowley.GetCowley(1727, "PridatSouvisejiciDokESSS", 10008658, false))
                {
                    cowley.Initialize(odchoziPosta.First(), this);
                    cowley.SetParamsPridatSouvisejici("Související dokument", 2, 1);
                    cowley.RunDialog();
                    result = cowley.ResultSet;
                }
            }
            else Message.Error("Faktura neobsahuje došlou ani odchozí poštu.");

            //kopírování záznamu řidí zakázková konfigurace
            if (CustomConfig.GetStringValue("kon_ESSS_DuplDokKO", "FD") == "A")
                CopyDMSDocs(result);

            AddSuperUser(result);
        }

        private void AddSuperUser(Int32Array result)
        {
            if (result.Count > 0)
            {
                var superUser = ESSSUtils.GetSuperUser();
                try
                {
                    using (INrsInstance instSD = NrsInstance.GetInstance(ClassNumbers.ESSSSouvisejiciDokument))
                    {
                        instSD.Retrieve(result[0]);
                        var owner = instSD.Master.GetItem<Int32?>(0, "vlastnik_dokumentu");

                        using (PredatNaJinySpisUzelCwl cwl = (PredatNaJinySpisUzelCwl)NrsCowley.GetCowley(ClassNumbers.ESSSSouvisejiciDokument, "ZpristupnitSouvisejici_N", FolderNumbers.ESSSSouvisejiciDokument, true))
                        {
                            cwl.Initialize(instSD);
                            cwl.SetParams_Nevizualni(0, superUser, false, 0, 0, true, "");
                            cwl.SetParams_Autorizace(owner.Value);
                            cwl.ParamsOK = true;
                            cwl.Run();
                        }
                    }
                }
                catch (Exception e)
                {
                    Message.WarningWithContext(this.ClassNumber, this.FolderNumber, result[0], e.Message);
                    throw;
                }
            }
        }

        private void CopyDMSDocs(Int32Array result)
        {
            if (result.Count > 0)
            {
                using (INrsInstance souvisDoc = NrsInstance.GetInstance(2821))
                {
                    souvisDoc.Retrieve(result.First());
                    souvisDoc.MasterRelations.GetRelations(DMS_RELATION, RelationSide.Right, out Int32Array dmsDoc);
                    //Udělám kopie dms dokumentů 
                    foreach (var docId in dmsDoc)
                    {
                        var newCopy = ESSSUtils.DuplicateESSSDoc(docId, SqlSelect.GetFolderNumber(docId));
                        souvisDoc.MasterRelations.AddRelation(DMS_COPY_RELATION, newCopy, RelationSide.Right);
                        DeleteDVFromDMS(newCopy);
                    }
                    souvisDoc.Update();
                }
            }
            else
                Message.Info("Fce PridatSouvisejiciDokESSS nevytvořila záznam v pořadači Související dokument.");
        }

        private void DeleteDVFromDMS(int newCopy)
        {
            using (INrsInstance dms = NrsInstance.GetInstance(1132))
            {
                dms.Retrieve(newCopy);
                dms.MasterRelations.DeleteRelations(DMS_RELATION, RelationSide.Left);
                dms.Update();
            }
        }
    }
}
