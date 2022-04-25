using KonceptHK.HeliosGluon.Queries;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class VlozitDoSpisuCowley : NrsCowley
    {

        public void VlozitDoSpisu()
        {
            if (this.RecordNumbers.Count > 0)
            {
                QueryTemplate query = QueryManager.Instance.GetQuery("DohledejESSSDokumentyProOpravu");
                query.ReplaceParametr("records", this.RecordNumbers);
                Datastore ds = SqlSelect.CreateDatastore(query.GetFinalQuery());

                var count = ds.Retrieve();

                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var record = ds.GetItemInt32(i, 0, 0);
                        var spis = ds.GetItemInt32(i, 1, 0);

                        using (INrsInstance instance = NrsInstance.GetInstance(this.ClassNumber))
                        {
                            instance.Retrieve(record);

                            try
                            {
                                DbTransaction.Current.Begin();

                                this.ZpracujSubjekt(instance, spis);

                                DbTransaction.Current.SetComplete();
                                DbTransaction.Current.End();
                            }
                            catch (Exception ex)
                            {

                                if (DbTransaction.IsTransaction)
                                {
                                    DbTransaction.Current.End();
                                }

                                Message.WarningWithContext(instance.ClassNumber, instance.FolderNumber, instance.RecordNumber, "Chyba při zpracování záznamu: " + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        private void ZpracujSubjekt(INrsInstance instance, Int32 spis)
        {
            instance.Master.SetItem(0, "spis", null);
            instance.Update();

            using (INrsCowley cowley = NrsCowley.GetCowley(instance.ClassNumber, "VlozitDokumentDoSpisuESSS", instance.FolderNumber, true))
            {
                cowley.Initialize(instance);
                cowley.Params.SetItem(0, "spis", spis);
                cowley.ParamsOK = true;
                cowley.Run();
            }
        }
    }
}
