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
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace RebarParametrisation
{
    public class RebarDocumentWorker
    {
        public bool haveConcreteClass;
        public double defaultConcreteClass;

        //база - id родительского семейства и его вложенные семейства
        public Dictionary<int, ParentFamilyContainer> parentFamiliesBase = new Dictionary<int, ParentFamilyContainer>();
        public List<Element> rebarWithoutHost = new List<Element>();
        public Document doc;
        public Autodesk.Revit.ApplicationServices.Application revitApp;
        public CategorySet rebarCatSet;

        public List<Element> MainElementsForLibFile = null;

        public string Start(Document doc, Autodesk.Revit.ApplicationServices.Application revitApp, Transform linkTransform, out List<Element> concreteElements)
        {
            haveConcreteClass = true;
            defaultConcreteClass = 25;
            Category rebarCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rebar);
            rebarCatSet = revitApp.Create.NewCategorySet();
            rebarCatSet.Insert(rebarCat);
            ElementId rebarCategoryId = new ElementId(BuiltInCategory.OST_Rebar);

            //собираю арматуру
            List<Element> rebarsAll = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_Rebar)
                .ToElements()
                .ToList();

            List<Rebar> StandartRebars = new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .ToList();

            List<RebarInSystem> RebarsInSystem = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarInSystem))
                .Cast<RebarInSystem>()
                .ToList();

            List<FamilyInstance> rebarsIfcAll = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Rebar)
                .Cast<FamilyInstance>()
                .ToList();

            List<FamilyInstance> rebarsIfcSingle = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Rebar)
                .Cast<FamilyInstance>()
                .Where(i => i.SuperComponent == null)
                .ToList();

            List<FamilyInstance> rebarsIfcNested = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Rebar)
                .Cast<FamilyInstance>()
                .Where(i => i.SuperComponent != null)
                .ToList();

            List<Element> rebarsNotNested = new List<Element>();
            rebarsNotNested.AddRange(StandartRebars);
            rebarsNotNested.AddRange(RebarsInSystem);
            rebarsNotNested.AddRange(rebarsIfcSingle);


            //сгруппирую вложенные семейства по родительским
            foreach (FamilyInstance nestedRebar in rebarsIfcNested)
            {
                FamilyInstance parentFamily = ParentFamilyContainer.GetMainParentFamily(nestedRebar);
                int parentId = parentFamily.Id.GetElementId();
                if (parentFamiliesBase.ContainsKey(parentId))
                {
                    parentFamiliesBase[parentId].childFamilies.Add(nestedRebar);
                }
                else
                {
                    List<FamilyInstance> fams = new List<FamilyInstance>() { nestedRebar };
                    ParentFamilyContainer pfc = new ParentFamilyContainer(parentFamily, nestedRebar);
                    parentFamiliesBase.Add(parentId, pfc);
                }
            }



            //собираю бетонные элементы
            CategoriesSupport catSupport = new CategoriesSupport(revitApp, doc);
            List<ElementId> concreteCategodyIds = catSupport.GetConcreteCategodyIds();
            View defaultView3d = ViewSupport.GetDefaultView(doc);
            concreteElements = new FilteredElementCollector(doc, defaultView3d.Id)
                .WhereElementIsNotElementType()
                .Where(x => x.Category != null)
                .Where(x => x.Category.Id != rebarCategoryId)
                .Where(x => concreteCategodyIds.Contains(x.Category.Id))
                .Where(x => Intersection.ContainsSolids(x))
                .Where(x => Intersection.CheckElementIsConcrete(x))
                .ToList();

            if (Settings.UseHostMark && MainElementsForLibFile == null)
            {
                List<long> errorIds = new List<long>();
                foreach (Element elem in concreteElements)
                {
                    Parameter markParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                   
                    string tempMark = markParam.AsString();
                    if (string.IsNullOrEmpty(tempMark))
                    {
                        errorIds.Add(elem.Id.GetElementId());
                    }
                }
                if (errorIds.Count > 0)
                {
                    string msg = "Не заполнена Марка у конструкций: " + string.Join(", ", errorIds);
                    return msg;
                }
            }

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Rebar BDS");

                //заполняю массу, длину и диаметр
                foreach (Element elem in rebarsAll)
                {
                    Parameter p = ParametersSupport.GetParameter(elem, "Орг.ИзделиеТипПодсчета");
                    if (p == null) continue; //отсеиваем левую арматуру без параметров
                    int c = p.AsInteger();
                    if (c < 1 || c > 5) continue; //отсеиваем каркасы и закладные детали

                    MyRebar myrebar = new MyRebar(elem);

                    if (!myrebar.IsValid) continue;
#if R2017 || R2018 || R2019 || R2020 || R2021 || R2022 || R2023
                    BuiltInParameterGroup invalidParamGroup = BuiltInParameterGroup.INVALID;
#else
                    ForgeTypeId invalidParamGroup = new ForgeTypeId(string.Empty);
#endif

                    if (Settings.UseRebarWeight)
                    {
                        Parameter paramWeight = ParametersSupport.CheckAndAddSharedParameter(elem, revitApp, rebarCatSet, "BDS_Weight", invalidParamGroup, true);
                        paramWeight.Set(myrebar.weightFinal);
                    }

                    if (Settings.UseRebarLength)
                    {
                        Parameter paramLength = ParametersSupport.CheckAndAddSharedParameter(elem, revitApp, rebarCatSet, "BDS_Length", invalidParamGroup, true);
                        paramLength.Set(myrebar.lengthMm);

                    }

                    if (Settings.UseRebarDiameter)
                    {
                        Parameter diamParam = ParametersSupport.CheckAndAddSharedParameter(elem, revitApp, rebarCatSet, "BDS_Diameter", invalidParamGroup, true);
                        diamParam.Set(myrebar.diameterMm);
                    }
                }

                doc.Regenerate();


                //заполняю марку основы для не вложенных стержней
                foreach (Element rebarNotNested in rebarsNotNested)
                {
                    ElementId hostId = null;
                    List<Element> hostElems = null;

                    if (MainElementsForLibFile != null) //если основной бетонный элемент задан принудительно
                    {
                        hostElems = MainElementsForLibFile;
                    }
                    else
                    {
                        if (rebarNotNested is Rebar)
                        {
                            hostId = (rebarNotNested as Rebar).GetHostId();
                        }
                        if (rebarNotNested is RebarInSystem)
                        {
                            hostId = (rebarNotNested as RebarInSystem).GetHostId();
                        }
                        if (rebarNotNested is FamilyInstance)
                        {
                            Element tempHost = Intersection.GetHostElementForIfcRebar(doc, defaultView3d, rebarNotNested, concreteElements, linkTransform);
                            if (tempHost == null)
                            {
                                rebarWithoutHost.Add(rebarNotNested);
                                continue;
                            }
                            hostId = tempHost.Id;
                        }

                        if (hostId == null)
                        {
                            rebarWithoutHost.Add(rebarNotNested);
                            continue;
                        }
                        Element hostElem = doc.GetElement(hostId);
                        hostElems = new List<Element>() { hostElem };
                    }
                    
                    string msg = ParametersSupport.WriteHostInfoSingleRebar(revitApp, rebarCatSet, rebarNotNested, hostElems);
                    if (msg != string.Empty)
                    {
                        return msg;
                    }
                }

                //заполняю марку основы для стержней, вложенных в сложные семейства
                foreach (var kvp in parentFamiliesBase)
                {
                    ParentFamilyContainer pfc = kvp.Value;
                    FamilyInstance parentFamily = pfc.parentFamily;

                    Dictionary<long, Element> hostElemsBase = new Dictionary<long, Element>();

                    List<Element> mainHostElems = null;

                    if (MainElementsForLibFile != null)  //если основной бетонный элемент задан принудительно
                    {
                        mainHostElems = MainElementsForLibFile;
                    }
                    else
                    {
                        if (parentFamily.Host is Wall || parentFamily.Host is Floor) //родительское семейство - на основе стены или пола, можно легко получить основу
                        {
                            Element mainHostElem = parentFamily.Host;
                            mainHostElems = new List<Element>() { mainHostElem };
                        }
                        else
                        {
                            foreach (FamilyInstance nestedRebar in pfc.childFamilies)
                            {
                                Element hostElem = Intersection.GetHostElementForIfcRebar(doc, defaultView3d, nestedRebar, concreteElements, linkTransform);
                                if (hostElem == null) continue;
                                long hostElemId = hostElem.Id.GetElementId();
                                if (!hostElemsBase.ContainsKey(hostElemId))
                                {
                                    hostElemsBase.Add(hostElemId, hostElem);
                                }
                            }
                            if (hostElemsBase.Count == 0)
                            {
                                rebarWithoutHost.Add(parentFamily);
                                continue;
                            }

                            if (hostElemsBase.Count == 1)
                            {
                                Element mainHostElem = hostElemsBase.First().Value;
                                mainHostElems = new List<Element>() { mainHostElem };
                            }
                            if (hostElemsBase.Count > 1)
                            {
                                List<Element> hosts = hostElemsBase.Values.ToList();
                                View defaultView = ViewSupport.GetDefaultView(doc);
                                Element mainHostElem = Intersection.GetBottomElement(hosts, defaultView);
                                mainHostElems = new List<Element>() { mainHostElem };
                            }
                        }
                    }

                    if (Settings.UseHostMark)
                    {
                        string hostMark = "";
                        foreach (Element mainHostElem in mainHostElems)
                        {
                            Parameter hostMarkParam = mainHostElem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                            string tempHostMark = hostMarkParam.AsString();
                            if (string.IsNullOrEmpty(tempHostMark))
                            {
                                return $"Не заполнена марка у конструкции id: {mainHostElem.Id} в файле {mainHostElem.Document.Title}";
                            }

                            if (hostMark != "") hostMark += "|";
                            hostMark += tempHostMark;
                        }
                        ParametersSupport.TryWriteParameter(parentFamily, "Мрк.МаркаКонструкции", hostMark);
                    }

                    foreach (FamilyInstance nestedRebar in pfc.childFamilies)
                    {
                        ParametersSupport.WriteHostInfoSingleRebar(revitApp, rebarCatSet, nestedRebar, mainHostElems);
                    }
                }


                t.Commit();
            }
            return string.Empty;
        }
    }
}
