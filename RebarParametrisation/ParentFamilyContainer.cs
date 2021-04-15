#region License
/*Данный код опубликован под лицензией Creative Commons Attribution-ShareAlike.
Разрешено использовать, распространять, изменять и брать данный код за основу для производных в коммерческих и
некоммерческих целях, при условии указания авторства и если производные лицензируются на тех же условиях.
Код поставляется "как есть". Автор не несет ответственности за возможные последствия использования.
Зуев Александр, 2020, все права защищены.
This code is listed under the Creative Commons Attribution-ShareAlike license.
You may use, redistribute, remix, tweak, and build upon this work non-commercially and commercially,
as long as you credit the author by linking back and license your new creations under the same terms.
This code is provided 'as is'. Author disclaims any implied warranty.
Zuev Aleksandr, 2020, all rigths reserved.*/
#endregion
#region Usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace RebarParametrisation
{
    public class ParentFamilyContainer : IComparable<ParentFamilyContainer>, IEquatable<ParentFamilyContainer>
    {
        public Element hostElement;
        public FamilyInstance parentFamily;
        public int parentFamilyId;
        public List<FamilyInstance> childFamilies;

        public ParentFamilyContainer(FamilyInstance ParentFamily, FamilyInstance FirstNestedFamily)
        {
            parentFamily = ParentFamily;
            parentFamilyId = ParentFamily.Id.IntegerValue;
            childFamilies = new List<FamilyInstance>() { FirstNestedFamily };
        }

        public int CompareTo(ParentFamilyContainer other)
        {
            return this.parentFamilyId.CompareTo(other.parentFamilyId);
        }

        public bool Equals(ParentFamilyContainer other)
        {
            return this.parentFamilyId.Equals(other.parentFamilyId);
        }


        /// <summary>
        /// Получает родительское семейство верхнего уровня, в которое вложенно данное семейство
        /// </summary>
        /// <param name="fi"></param>
        /// <returns>null если семейство не вложенное</returns>
        public static FamilyInstance GetMainParentFamily(FamilyInstance fi)
        {
            FamilyInstance parentFamily = fi.SuperComponent as FamilyInstance;
            if (parentFamily == null)
            {
                return null;
            }
            else
            {
                FamilyInstance parentFamily2 = parentFamily.SuperComponent as FamilyInstance;
                if (parentFamily2 != null)
                {
                    return parentFamily2;
                }
                else
                {
                    return parentFamily;
                }
            }
        }

    }
}
