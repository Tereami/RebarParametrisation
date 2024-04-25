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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
#endregion


namespace RebarParametrisation
{
    public static class ViewSupport
    {
        public static View GetDefaultView(Document doc)
        {
            View curView = doc.ActiveView;
            if (curView is View3D)
            {
                return curView;
            }
            else
            {
                List<View3D> views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(v => v.IsTemplate == false)
                    .Where(v => v.Name.Contains("3D"))
                    .ToList();
                if (views.Count == 0) throw new Exception("Создайте 3D-вид в файле " + doc.Title);

                View3D view = views.First();

                if(view.DetailLevel != ViewDetailLevel.Fine)
                {
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Высокий уровень детализации для вида");

                        try
                        {
                            view.DetailLevel = ViewDetailLevel.Fine;
                        }
                        catch
                        {
                            throw new Exception($"Не удалось установить высокий уровень детализации для вида {view.Name} id: {view.Id}");
                        }

                        t.Commit();
                    }
                }

                return view;
            }
        }
    }
}
