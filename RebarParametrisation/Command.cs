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
using Autodesk.Revit.UI; 
using Autodesk.Revit.ApplicationServices;
#endregion


namespace RebarParametrisation
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document mainDoc = commandData.Application.ActiveUIDocument.Document;
            Application revitApp = commandData.Application.Application;

            //открываю окно выбора параметров, которые буду заполняться
            FormSelectParams formSelectParams = new FormSelectParams();
            formSelectParams.ShowDialog();
            if (formSelectParams.DialogResult != System.Windows.Forms.DialogResult.OK) return Result.Cancelled;


            RebarDocumentWorker mainWorker = new RebarDocumentWorker();
            List<Element> mainDocConcreteElements = new List<Element>();

            

            string mainWorkerMessage = mainWorker.Start(mainDoc, revitApp, Transform.Identity, out mainDocConcreteElements);
            if (!string.IsNullOrEmpty(mainWorkerMessage))
            {
                message = mainWorkerMessage + ". Документ " + mainDoc.Title;
                return Result.Failed;
            }

            if (Settings.LinkFilesSetting == Settings.ProcessedLinkFiles.NoLinks)
                return Result.Succeeded;

            List<RevitLinkInstance> linksAll = new FilteredElementCollector(mainDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            List<RevitLinkInstance> linksLib = linksAll
                .Where(i => i.Name.Contains(".lib"))
                .ToList();

            // имя ссылки lib и список конструкций, которые она пересекает
            Dictionary<string, List<Element>> hostElemsForLibLinks = new Dictionary<string, List<Element>>();

            foreach (RevitLinkInstance rli in linksLib)
            {
                string linkInstanceTitle = LinksSupport.GetDocumentTitleFromLinkInstance(rli);
                Element hostElem = LinksSupport.GetConcreteElementIsHostForLibLinkFile(mainDoc, ViewSupport.GetDefaultView(mainDoc), mainDocConcreteElements, rli);
                if (hostElem == null) continue;
                if(hostElemsForLibLinks.ContainsKey(linkInstanceTitle))
                {
                    hostElemsForLibLinks[linkInstanceTitle].Add(hostElem);
                }
                else
                {
                    hostElemsForLibLinks.Add(linkInstanceTitle, new List<Element> { hostElem });
                }
            }
            List<RevitLinkInstance>  linksWithoutDuplicates = LinksSupport.DeleteDuplicates(linksAll);

            foreach (RevitLinkInstance rli in linksWithoutDuplicates)
            {
                RevitLinkType rlt = mainDoc.GetElement(rli.GetTypeId()) as RevitLinkType;
                Document linkDoc = rli.GetLinkDocument();
                if (linkDoc == null) continue;

                string linkDocTitle = LinksSupport.GetDocumentTitleFromLinkInstance(rli);
                if (!linkDocTitle.Contains("-КР-")) continue;
                if (!linkDocTitle.Contains("lib") && Settings.LinkFilesSetting == Settings.ProcessedLinkFiles.OnlyLibs) continue;

                

                ModelPath mPath = linkDoc.GetWorksharingCentralModelPath();

                rlt.Unload(new SaveCoordinates());

                OpenOptions oo = new OpenOptions();

                linkDoc = revitApp.OpenDocumentFile(mPath, oo);


                RebarDocumentWorker linkWorker = new RebarDocumentWorker();
                if (linkDocTitle.Contains("lib"))
                {
                    if (hostElemsForLibLinks.ContainsKey(linkDocTitle))
                    {
                        List<Element> mainElemsForLib = hostElemsForLibLinks[linkDocTitle];
                        if (mainElemsForLib.Count > 0)
                        {
                            linkWorker.MainElementsForLibFile = mainElemsForLib;
                        }
                    }
                }
                Transform linkTransform = rli.GetTransform();
                List<Element> linkConcreteElements = new List<Element>();
                string linkWorkerMessage = linkWorker.Start(linkDoc, revitApp, linkTransform, out linkConcreteElements);
                if (!string.IsNullOrEmpty(linkWorkerMessage))
                {
                    message = linkWorkerMessage + ". Связь " + linkDoc.Title;
                    return Result.Failed;
                }



                TransactWithCentralOptions transOpt = new TransactWithCentralOptions();
                SynchronizeWithCentralOptions syncOpt = new SynchronizeWithCentralOptions();

                RelinquishOptions relOpt = new RelinquishOptions(true);
                syncOpt.SetRelinquishOptions(relOpt);

                linkDoc.SynchronizeWithCentral(transOpt, syncOpt);

                linkDoc.Close();
#if R2017 
                RevitLinkLoadResult rllr = rlt.Reload();
#else
                LinkLoadResult llr = rlt.Reload();
#endif
            }

            return Result.Succeeded;
        }
    }

    public class SaveCoordinates : ISaveSharedCoordinatesCallback
    {
        public SaveModifiedLinksOptions GetSaveModifiedLinksOption(RevitLinkType link)
        {
            return SaveModifiedLinksOptions.DoNotSaveLinks;
        }
    }
}
