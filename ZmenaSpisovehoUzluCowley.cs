using KonceptHK.HeliosGluon;
using Noris.KonceptHK.SpisovaSluzba.Utils;
using Noris.Srv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noris.KonceptHK.SpisovaSluzba
{
    class ZmenaSpisovehoUzluCowley : NrsCowley
    {
        public void ZmenaSpisovehoUzlu()
        {
            GetRealationsToCurrentClass(out int? dv);

            foreach (var item in RecordNumbers)
            {
                using (INrsInstance inst = NrsInstance.GetInstance(this.ClassNumber))
                {
                    inst.Retrieve(item);
                    inst.MasterRelations.GetRelations(dv.Value, RelationSide.Left, out Int32Array dp);

                    if (dp.Count > 0)
                        ESSSUtils.SetOwnerOfDoc(dp.First(), this.Params.GetItemInt32(0, "spisovy_uzel"),
                            this.Params.GetItemInt32(0, "uzivatel"));                   
                    else 
                        Message.Warning($"Záznam {SqlSelect.GetReference(item)} třídy {this.ClassNumber}" +
                            $" neobsahuje došlou poštu.");
                }
            }
        }
        private void GetRealationsToCurrentClass(out int? dv)
        {
            dv = null;
            switch (this.ClassNumber)
            {
                case 46:
                    dv = 1399;
                    break;
                //case 65:
                //    dv = ;
                //    break;              
                //case 70:
                //    dv = ;
                //    break;
                //case 644:
                //    dv = ;
                //    break;
                //case 2274:
                //    dv = ;
                //    break;
                default: Message.Error($"Nepodařilo se dohledat dv ke třídě {this.ClassNumber}, nejspíš není nastaveno v kódu.");
                    break;
            }
        }
    }
}
