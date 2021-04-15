﻿#region License
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
using Autodesk.Revit.DB.Structure;
#endregion

namespace RebarParametrisation
{
    public static class LinksSupport
    {
        public static List<RevitLinkInstance> DeleteDuplicates(List<RevitLinkInstance> links)
        {
            HashSet<string> names = new HashSet<string>();
            List<RevitLinkInstance> newLinks = new List<RevitLinkInstance>();

            foreach(RevitLinkInstance rli in links)
            {
                string docName = GetDocumentTitleFromLinkInstance(rli);
                if (names.Contains(docName)) continue;

                names.Add(docName);
                newLinks.Add(rli);
            }

            return newLinks;
        }


        public static string GetDocumentTitleFromLinkInstance(RevitLinkInstance rli)
        {
            string docName = rli.Name.Split(':').First();
            return docName;
        }


        public static Element GetConcreteElementIsHostForLibLinkFile(Document mainDoc, View mainDocView, List<Element> mainDocAllConcreteElems, RevitLinkInstance linkInstance)
        {
            Element hostElem = null;

            //получаю из связи все арматурные стержни
            Document linkDoc = linkInstance.GetLinkDocument();
            FilteredElementCollector linkRebars = new FilteredElementCollector(linkDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_Rebar);
            Transform linkTransform = linkInstance.GetTransform();

            //проверяю, с какими элементами в основном файле они пересекаются
            List<Element> resultMainHostElements = new List<Element>();

            foreach(Element curHost in mainDocAllConcreteElems)
            {
                foreach (Element rebar in linkRebars)
                {
                    bool checkIntersection = false;
                    if (rebar is Rebar)
                    {
                        Rebar bar = rebar as Rebar;
                        checkIntersection = Intersection.CheckIsIntersectsRebarAndElement(mainDoc, bar, curHost, mainDocView, linkTransform);
                    }
                    else
                    {
                        Intersection.IntersectionResult ir = Intersection.CheckElementsIsIntersect(mainDoc, mainDocView, curHost, rebar, linkTransform);
                        if (ir == Intersection.IntersectionResult.Intersection)
                            checkIntersection = true;
                            
                    }
                    if(checkIntersection)
                        resultMainHostElements.Add(curHost);
                }
            }

            //получаю самый нижний элемент
            if (resultMainHostElements.Count == 0) return null;

            hostElem = Intersection.GetBottomElement(resultMainHostElements, mainDocView);

            return hostElem;
        }
    }
}
