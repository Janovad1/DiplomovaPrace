using KonceptHK.HeliosGluon;
using KonceptHK.HeliosGluon.Queries;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.LCS.Base;
using Noris.Srv;
using System;

namespace Noris.KonceptHK.SpisovaSluzba
{
    internal class PridatSuperuzivateleCowley : NrsCowley
    {
        private void ProcessWithoutTransaction(Action<int> mainFunc)
        {
            foreach (var record in RecordNumbers)
            {
                try
                {
                    mainFunc(record);
                }
                catch (Exception e)
                {
                    Message.WarningWithContext(this.ClassNumber, this.FolderNumber, record, e.Message);
                    continue;
                }
            }
        }
        public void PridatSuperuzivateleKeSpisu()
        {
            ProcessWithoutTransaction(ProcessSpis);
        }

        private void ProcessSpis(int record)
        {
            ESSSUtils.PredatSpisNaJinyUzel(record);
        }

        public void PridatSuperuzivateleKDP()
        {
            ProcessWithoutTransaction(ProcessDP);
        }

        private void ProcessDP(int record)
        {
            var superUser = ESSSUtils.GetSuperUser();

            using (INrsInstance instDoslaPosta = NrsInstance.GetInstance(ClassNumbers.DOSLA_POSTA))
            {
                instDoslaPosta.Retrieve(record);
                var owner = instDoslaPosta.Master.GetItem<Int32?>(0, "document_owner");

                if (!owner.HasValue)
                    Message.Error("Není vyplněn vlastník došlé pošty.");

                instDoslaPosta.MasterRelations.GetRelations(112322, RelationSide.Left, out Int32Array users);

                if (!users.Contains(superUser))
                {
                    using (PredatNaJinySpisUzelCwl cwl = (PredatNaJinySpisUzelCwl)NrsCowley.GetCowley(ClassNumbers.DOSLA_POSTA, "PredatNaJinySpisUzel_N", 0, true))
                    {
                        cwl.Initialize(instDoslaPosta);
                        cwl.SetParams_Nevizualni(0, superUser, false, 0, 0, true, "");
                        cwl.SetParams_Autorizace(owner.Value);
                        cwl.ParamsOK = true;
                        cwl.Run();
                    }
                }
                else
                    Message.Info($"Došla pošta již obsahuje nahližitele: {SqlSelect.GetReference(superUser)}");        
            }
        }
        public void PridatSuperuzivateleSouvDoc()
        {
            ProcessWithoutTransaction(ProcessSD);
        }

        private void ProcessSD(int record)
        {
            var superUser = ESSSUtils.GetSuperUser();
            using (INrsInstance instSD = NrsInstance.GetInstance(ClassNumbers.ESSSSouvisejiciDokument))
            {
                instSD.Retrieve(record);
                var owner = instSD.Master.GetItem<Int32?>(0, "vlastnik_dokumentu");

                if (!owner.HasValue)
                    owner = gCache.GetUserNumber();

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
    }
}
