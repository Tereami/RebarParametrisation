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
    public static class Intersection
    {
        /// <summary>
        /// Получает конструкцию, которой принадлежит IFC-стержень
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="curView"></param>
        /// <param name="rebar"></param>
        /// <param name="concreteElements"></param>
        /// <returns></returns>
        public static Element GetHostElementForIfcRebar(Document doc, View view, Element rebar, List<Element> concreteElements, Transform transform2)
        {
            Element hostElem = null;
            List<Element> intersectElems = Intersection.GetAllIntersectionElements(doc, view, rebar, concreteElements, transform2);
            if (intersectElems == null || intersectElems.Count == 0)
            {
                //эта ifc-арматура висит в воздухе, пропускаем
                return null;
            }

            if (intersectElems.Count == 1) hostElem = intersectElems.First(); //ifc-арматура пересекается только с одной конструкцией, это и есть её основа

            //если пересекает несколько конструкций - берем нижнюю
            if (intersectElems.Count > 1)
            {
                View defaultView = ViewSupport.GetDefaultView(doc);
                hostElem = Intersection.GetBottomElement(intersectElems, defaultView);
            }

            return hostElem;
        }


        /// <summary>
        /// Получает самый нижерасположенный элемент из списка
        /// </summary>
        /// <param name="elems"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        public static Element GetBottomElement(List<Element> elems, View view)
        {
            Element elem = null;
            double TopPointOfBottomElement = -999999;

            foreach (Element curElem in elems)
            {
                BoundingBoxXYZ box = curElem.get_BoundingBox(view);
                XYZ topPoint = box.Max;
                double curTopElev = topPoint.Z;

                if (elem == null)
                {
                    elem = curElem;
                    TopPointOfBottomElement = curTopElev;
                    continue;
                }


                if (curTopElev < TopPointOfBottomElement)
                {
                    TopPointOfBottomElement = curTopElev;
                    elem = curElem;
                }
            }

            return elem;
        }




        public enum IntersectionResult { NoIntersection, Touching, Intersection, Incorrect }

        public static bool CheckIsIntersectsRebarAndElement(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar, Element elem, View view, Transform linkTransform)
        {
            GeometryElement geoElem = elem.get_Geometry(new Options());
            List<Solid> solids = GetSolidsOfElement(geoElem);

#if R2017
            List<Curve> rebarCurves = rebar.ComputeDrivingCurves().ToList();
#else
            List<Curve> rebarCurves = rebar.GetShapeDrivenAccessor().ComputeDrivingCurves().ToList();
#endif
            for(int i = 0; i< solids.Count; i++)
            {
                Solid s = solids[i];
                if(!linkTransform.IsIdentity)
                {
                    s = SolidUtils.CreateTransformed(s, linkTransform.Inverse);
                }
                foreach(Face face in s.Faces)
                {
                    foreach(Curve rebarCurve in rebarCurves)
                    {
                        SetComparisonResult result = face.Intersect(rebarCurve);
                        if (result == SetComparisonResult.Overlap) return true;
                    }
                }
            }
            return false;
        }


        public static IntersectionResult CheckElementsIsIntersect(Document doc, View view, Element elem1, Element elem2, Transform transformSecondElementToFirst)
        {
            //BoundingBoxXYZ box1 = elem1.get_BoundingBox(view);
            //BoundingBoxXYZ box2 = elem2.get_BoundingBox(view);

            //if (!transformSecondElementToFirst.IsIdentity)
            //{
            //    box2.Transform = transformSecondElementToFirst;
            //}

            //bool checkBoxIsIntersect = CheckBoxIsIntersect(box1, box2);
            //if (!checkBoxIsIntersect) return IntersectionResult.NoIntersection;

            GeometryElement gelem1 = elem1.get_Geometry(new Options());
            GeometryElement gelem2 = elem2.get_Geometry(new Options());

            List<Solid> solids1 = GetSolidsOfElement(gelem1);
            List<Solid> solids2 = GetSolidsOfElement(gelem2);

            for (int i = 0; i < solids1.Count; i++)
            {
                Solid solid1 = solids1[i];
                for (int j = 0; j < solids2.Count; j++)
                {
                    Solid solid2 = solids2[j];
                    if (!transformSecondElementToFirst.IsIdentity)
                    {
                        solid2 = SolidUtils.CreateTransformed(solid2, transformSecondElementToFirst);
                    }

                    bool check = false;
                    try
                    {
                        check = CheckSolidsIsIntersect(solid1, solid2);
                    }
                    catch
                    {
                        Autodesk.Revit.UI.TaskDialog.Show("Предупреждение", $"Некорректное размещение элементов: {elem1.Name} id: {elem1.Id} , {elem2.Name} id: {elem2.Id}");
                        return IntersectionResult.Incorrect;
                    }
                    if (check) return IntersectionResult.Intersection;
                }
            }
            return IntersectionResult.NoIntersection;
        }


        public static bool CheckSolidsIsIntersect(Solid solid1, Solid solid2)
        {
            Solid interSolid = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
            double volume = Math.Abs(interSolid.Volume);
            if (volume > 0.000001)
            {
                return true;
            }
            return false;
        }


        private static List<Solid> GetSolidsOfElement(GeometryElement geoElem)
        {
            List<Solid> solids = new List<Solid>();

            foreach (GeometryObject geoObj in geoElem)
            {
                if (geoObj is Solid)
                {
                    Solid solid = geoObj as Solid;
                    if (solid == null) continue;
                    if (solid.Volume == 0) continue;
                    solids.Add(solid);
                    continue;
                }
                if (geoObj is GeometryInstance)
                {
                    GeometryInstance geomIns = geoObj as GeometryInstance;
                    GeometryElement instGeoElement = geomIns.GetInstanceGeometry();
                    List<Solid> solids2 = GetSolidsOfElement(instGeoElement);
                    solids.AddRange(solids2);
                }
            }
            return solids;
        }



        public static bool ContainsSolids(Element elem)
        {
            GeometryElement geoElem = elem.get_Geometry(new Options());
            if (geoElem == null) return false;

            bool check = ContainsSolids(geoElem);
            return check;
        }


        public static List<Element> GetAllIntersectionElements(Document doc, View view, Element elem, List<Element> elems, Transform transformToFirstElem)
        {
            List<Element> elems2 = new List<Element>();

            foreach (Element curElem in elems)
            {
                if (curElem.Id == elem.Id) continue; //один и тот же элемент

                IntersectionResult check = CheckElementsIsIntersect(doc, view, curElem, elem, transformToFirstElem);
                if (check == IntersectionResult.Intersection) elems2.Add(curElem);
            }

            if (elems2.Count == 0)
            {
                return null;
            }

            return elems2;
        }

        /// <summary>
        /// Проверить, содержит ли данный элемент объемную 3D-геометрию
        /// </summary>
        public static bool ContainsSolids(GeometryElement geoElem)
        {
            if (geoElem == null) return false;

            foreach (GeometryObject geoObj in geoElem)
            {
                if (geoObj is Solid)
                {
                    return true;
                }
                if (geoObj is GeometryInstance)
                {
                    GeometryInstance geomIns = geoObj as GeometryInstance;
                    GeometryElement instGeoElement = geomIns.GetInstanceGeometry();
                    bool check = ContainsSolids(instGeoElement);
                    if (check) return true;
                }
            }
            return false;
        }

        public static bool CheckBoxIsIntersect(BoundingBoxXYZ box1, BoundingBoxXYZ box2)
        {
            XYZ center1 = new XYZ((box1.Min.X + box1.Max.X) / 2, (box1.Min.Y + box1.Max.Y) / 2, (box1.Min.Z + box1.Max.Z) / 2);
            XYZ halfwidth1 = new XYZ((box1.Max.X - box1.Min.X) / 2, (box1.Max.Y - box1.Min.Y) / 2, (box1.Max.Z - box1.Min.Z) / 2);

            XYZ center2 = new XYZ((box2.Min.X + box2.Max.X) / 2, (box2.Min.Y + box2.Max.Y) / 2, (box2.Min.Z + box2.Max.Z) / 2);
            XYZ halfwidth2 = new XYZ((box2.Max.X - box2.Min.X) / 2, (box2.Max.Y - box2.Min.Y) / 2, (box2.Max.Z - box2.Min.Z) / 2);


            if (Math.Abs(center1.X - center2.X) > (halfwidth1.X + halfwidth2.X)) return false;
            if (Math.Abs(center1.Y - center2.Y) > (halfwidth1.Y + halfwidth2.Y)) return false;
            if (Math.Abs(center1.Z - center2.Z) > (halfwidth1.Z + halfwidth2.Z)) return false;

            return true;
        }


        /// <summary>
        /// Проверяет, есть ли в элементе материал с классом "Бетон" и заполнен Мтрл.КодМатериала
        /// </summary>
        /// <param name="elem"></param>
        /// <returns></returns>
        public static bool CheckElementIsConcrete(Element elem)
        {
            List<ElementId> materialsIds = elem.GetMaterialIds(false).ToList();

            foreach (ElementId matId in materialsIds)
            {
                Material mat = elem.Document.GetElement(matId) as Material;
                string materialClass = mat.MaterialClass;
                if (materialClass != "Бетон") continue;
                Parameter matCode = mat.get_Parameter(new Guid("b5675d33-fade-46b1-921b-0cab8eec101e")); //Мтрл.КодМатериала
                if (matCode == null) continue;
                if (matCode.AsInteger() == 0) continue;

                double volume = elem.GetMaterialVolume(matId);
                if (volume != 0) return true;
            }

            return false;
        }
    }
}
