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
using Autodesk.Revit.ApplicationServices;
#endregion

namespace RebarParametrisation
{
    public static class ParametersSupport
    {
        public enum BindSharedParamResult
        {
            eAlreadyBound,
            eSuccessfullyBound,
            eSuccesfullyReBound,
            eWrongParamType,
            eWrongBindingType,
            eFailed
        }

        /// <summary>
        /// Добавить привязку к категории для параметра проекта
        /// </summary>
        public static BindSharedParamResult BindSharedParam(Document doc, Element elem, string paramName, Definition definition)
        {
            try
            {
                Application app = doc.Application;

                //собираю уже добавленные категории для повторной вставки и добавления новой категории
                CategorySet catSet = app.Create.NewCategorySet();

                // Loop all Binding Definitions
                // IMPORTANT NOTE: Categories.Size is ALWAYS 1 !?
                // For multiple categories, there is really one 
                // pair per each category, even though the 
                // Definitions are the same...

                DefinitionBindingMapIterator iter
                  = doc.ParameterBindings.ForwardIterator();

                while (iter.MoveNext())
                {
                    Definition def = iter.Key;
                    if (!paramName.Equals(def.Name)) continue;

                    ElementBinding elemBind
                      = (ElementBinding)iter.Current;

                    // Check for category match - Size is always 1!


                    // If here, no category match, hence must 
                    // store "other" cats for re-inserting

                    foreach (Category catOld in elemBind.Categories)
                        catSet.Insert(catOld); // 1 only, but no index...

                }

                // If here, there is no Binding Definition for 
                // it, so make sure Param defined and then bind it!


                Category cat = elem.Category;
                catSet.Insert(cat);

                InstanceBinding bind = app.Create.NewInstanceBinding(catSet);

                //используем Insert или ReInsert, что сработает
                if (doc.ParameterBindings.Insert(definition, bind))
                {
                    return BindSharedParamResult.eSuccessfullyBound;
                }
                else
                {
                    if (doc.ParameterBindings.ReInsert(definition, bind))
                    {
                        return BindSharedParamResult.eSuccessfullyBound;
                    }
                    else
                    {
                        return BindSharedParamResult.eFailed;
                    }
                }
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", string.Format(
                  "Error in Binding Shared Param: {0}",
                  ex.Message));

                return BindSharedParamResult.eFailed;
            }
        }










        /// <summary>
        /// Проверяет налиxие общего параметра у элемента. Если параметр есть - возвращает его. Иначе добавляет параметр из файла общих параметров.
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="app"></param>
        /// <param name="catset"></param>
        /// <param name="ParameterName"></param>
        /// <param name="paramGroup"></param>
        /// <param name="SetVaryByGroups"></param>
        /// <returns></returns>
        public static Parameter CheckAndAddSharedParameter(Element elem, Application app, CategorySet catset, string ParameterName,
#if R2017 || R2018 || R2019 || R2020 || R2021 || R2022 || R2023
            BuiltInParameterGroup paramGroup, 
#else
            ForgeTypeId paramGroup,
#endif
            bool SetVaryByGroups)
        {
            Document doc = elem.Document;
            Parameter param = elem.LookupParameter(ParameterName);
            if (param != null) return param;


            ExternalDefinition exDef = null;
            string sharedFile = app.SharedParametersFilename;
            DefinitionFile sharedParamFile = app.OpenSharedParameterFile();
            foreach (DefinitionGroup defgroup in sharedParamFile.Groups)
            {
                foreach (Definition def in defgroup.Definitions)
                {
                    if (def.Name == ParameterName)
                    {
                        exDef = def as ExternalDefinition;
                    }
                }
            }
            if (exDef == null) throw new Exception("В файл общих параметров не найден общий параметр " + ParameterName);

            bool checkContains = doc.ParameterBindings.Contains(exDef);
            if (checkContains)
            {
                var res = BindSharedParam(doc, elem, ParameterName, exDef);
            }

            InstanceBinding newIB = app.Create.NewInstanceBinding(catset);

            doc.ParameterBindings.Insert(exDef, newIB, paramGroup);

            if (SetVaryByGroups)
            {
                doc.Regenerate();

                SharedParameterElement spe = SharedParameterElement.Lookup(doc, exDef.GUID);
                InternalDefinition intDef = spe.GetDefinition();
                intDef.SetAllowVaryBetweenGroups(doc, true);
            }
            doc.Regenerate();


            param = elem.LookupParameter(ParameterName);
            if (param == null) throw new Exception("Не удалось добавить обший параметр " + ParameterName);

            return param;
        }




        /// <summary>
        /// Получает параметр из экземпляра или типа элемента
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="ParameterName"></param>
        /// <returns></returns>
        public static Parameter GetParameter(Element elem, string ParameterName)
        {
            Parameter param = elem.LookupParameter(ParameterName);
            if (param != null) return param;

            ElementId typeId = elem.GetTypeId();
            if (typeId == null) return null;

            Element type = elem.Document.GetElement(typeId);

            param = type.LookupParameter(ParameterName);
            return param;
        }

        /// <summary>
        /// Записывает значение параметра вэлемент, если параметр присутствует и не заблокирован
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="paramName"></param>
        /// <param name="value"></param>
        /// <returns>true если параметр успешно записан, иначе false</returns>
        public static bool TryWriteParameter(Element elem, string paramName, string value)
        {
            Parameter param = elem.LookupParameter(paramName);
            if (param != null)
            {
                if (!param.IsReadOnly)
                {
                    param.Set(value);
                    return true;
                }
            }
            return false;
        }

        public static string ConvertHashsetToString(HashSet<string> values)
        {
            List<string> vals = values.ToList();
            string result = vals[0];
            for (int i = 1; i < vals.Count; i++)
            {
                result += "|" + vals[i];
            }
            return result;
        }

        /// <summary>
        /// Заполняет параметры BDS_RebarHostId, BDS_RebarUniqueHostId, Мрк.МаркаКонструкции, Рзм.ТолщинаОсновы. В случае нескольких элемеетов знчения записываются через разделитель "|"
        /// </summary>
        /// <param name="rebar"></param>
        /// <param name="hostElem"></param>
        public static string WriteHostInfoSingleRebar(Autodesk.Revit.ApplicationServices.Application revitApp, CategorySet rebarCatSet, Element rebar, List<Element> hostElements)
        {
#if R2017 || R2018 || R2019 || R2020 || R2021 || R2022 || R2023
            BuiltInParameterGroup invalidParamGroup = BuiltInParameterGroup.INVALID;
#else
            ForgeTypeId invalidParamGroup = new ForgeTypeId(string.Empty);
#endif

            if (Settings.UseHostId)
            {
                HashSet<string> hostIds = new HashSet<string>();
                foreach (Element hostElem in hostElements)
                {
                    string tempid = hostElem.Id.GetElementId().ToString();
                    hostIds.Add(tempid);
                }
                Parameter rebarHostIdParam = RebarParametrisation.ParametersSupport.CheckAndAddSharedParameter(rebar, revitApp, rebarCatSet, "BDS_RebarHostId", invalidParamGroup, true);
                string hostIdString = ConvertHashsetToString(hostIds);
                rebarHostIdParam.Set(hostIdString);
            }
            if (Settings.UseUniqueHostId)
            {
                HashSet<string> hostUniqIds = new HashSet<string>();
                foreach (Element hostElem in hostElements)
                {
                    hostUniqIds.Add(hostElem.UniqueId);
                }

                Parameter rebarHostUniqueIdParam = RebarParametrisation.ParametersSupport.CheckAndAddSharedParameter(rebar, revitApp, rebarCatSet, "BDS_RebarUniqueHostId", invalidParamGroup, true);
                string hostUniqueId = ConvertHashsetToString(hostUniqIds);
                rebarHostUniqueIdParam.Set(hostUniqueId);
            }

            if (Settings.UseHostMark)
            {
                HashSet<string> hostMarks = new HashSet<string>();
                foreach (Element hostElem in hostElements)
                {
                    Parameter hostMarkParam = hostElem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    string tempMark = hostMarkParam.AsString();
                    if (string.IsNullOrEmpty(tempMark))
                    {
                        return $"Не заполнена марка у конструкции: {hostElem.Id} в файле {hostElem.Document.Title}";
                    }
                    else
                    {
                        hostMarks.Add(tempMark);
                    }
                }

                string hostMark = ConvertHashsetToString(hostMarks);
                ParametersSupport.TryWriteParameter(rebar, "Мрк.МаркаКонструкции", hostMark);
            }

            if (Settings.UseHostThickness)
            {
                Parameter thicknessRebarParam = rebar.LookupParameter("Рзм.ТолщинаОсновы");
                if (thicknessRebarParam != null)
                {
                    if (!thicknessRebarParam.IsReadOnly)
                    {
                        HashSet<double> hostWidths = new HashSet<double>();

                        foreach (Element hostElem in hostElements)
                        {
                            if (hostElem is Wall)
                            {
                                Wall hostWall = hostElem as Wall;
                                hostWidths.Add(hostWall.Width);
                            }
                            if (hostElem is Floor)
                            {
                                Floor hostFloor = hostElem as Floor;
                                Parameter thicknessParam = hostFloor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                                if (thicknessParam == null)
                                {
                                    thicknessParam = hostFloor.get_Parameter(BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM);
                                    if (thicknessParam == null) hostWidths.Add(0);
                                }
                                if (thicknessParam != null)
                                {
                                    hostWidths.Add(thicknessParam.AsDouble());
                                }
                            }
                        }

                        double hostWidth = 0;
                        if (hostWidths.Count == 1)
                            hostWidth = hostWidths.First();
                        thicknessRebarParam.Set(hostWidth);
                    }
                }
            }
            return string.Empty;
        }
    }
}
