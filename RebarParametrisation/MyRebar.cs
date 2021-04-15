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
    public class MyRebar
    {
        public Element elem { get; }
        public int diameterMm { get; }
        public double lengthMm { get; set; }
        public double count { get; }
        public double rebarClass { get; }
        public double WeightPerMeter { get; }
        public bool IsValid { get; }
        //public bool IsCancel { get; }
        public double overlapCoeff { get; set; }

        public bool calsAsSummLength { get; }

        public double weightFinal { get; }


        public MyRebar(Element Elem)
        {
            IsValid = false;
            //IsCancel = false;
            elem = Elem;

            Parameter rebarIsFamilyParam = ParametersSupport.GetParameter(elem, "Арм.ВыполненаСемейством");
            if (rebarIsFamilyParam == null) return;

            bool rebarIsFamily = rebarIsFamilyParam.AsInteger() == 1;

            Parameter rebarClassParam = ParametersSupport.GetParameter(elem, "Арм.КлассЧисло");
            if (rebarClassParam == null) return;
            rebarClass = rebarClassParam.AsDouble();

            


            this.SetLength(rebarIsFamily);


            if (rebarIsFamily)
            {
                Parameter countParam = ParametersSupport.GetParameter(elem, "О_Количество");
                if (countParam == null) return;
                count = countParam.AsDouble();

                if (rebarClass > 0)
                {
                    Parameter diamParam = ParametersSupport.GetParameter(elem, "Рзм.Диаметр");
                    if (diamParam == null) return;
                    double diameterFoots = diamParam.AsDouble();
                    diameterMm = (int)(diameterFoots * 304.8);
                    WeightPerMeter = GetWeightPerMeter(diameterMm);
                }
                else
                {
                    Parameter WeightPerMeterParam = ParametersSupport.GetParameter(elem, "О_МассаПогМетра");
                    if (WeightPerMeterParam == null) return;
                    WeightPerMeter = WeightPerMeterParam.AsDouble();
                }
            }
            else
            {
                count = elem.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS).AsInteger();

                Parameter diamParam = ParametersSupport.GetParameter(elem, "Диаметр стержня");
                if (diamParam == null) return;
                double diameterFoots = diamParam.AsDouble();
                double d2 = diameterFoots * 304.8;
                double d3 = Math.Round(d2);

                diameterMm = (int)d3;
                WeightPerMeter = GetWeightPerMeter(diameterMm);
            }

            double WeightOnePcs = 0;
            double countPM = 0;

            Parameter asSummLengthParam = ParametersSupport.GetParameter(elem, "Рзм.ПогМетрыВкл");
            if (asSummLengthParam == null) return;
            bool calcAsSummLength = (asSummLengthParam.AsInteger() == 1) && (!rebarIsFamily);
            if (calcAsSummLength)
            {
                WeightOnePcs = WeightPerMeter;
                overlapCoeff = 1;
                if (!rebarIsFamily && rebarClass > 0)
                {
                    double concreteClass = 0;
                    Parameter concreteClassParam = ParametersSupport.GetParameter(elem, "Арм.КлассБетона");
                    if (concreteClassParam == null) concreteClass = Settings.DefaultConcreteClass;
                    else concreteClass = concreteClassParam.AsDouble();

                    double Rs = GetRs();
                    double Rbt = GetRbt(concreteClass);

                    double mm32 = 1;
                    if (diameterMm > 32) mm32 = 0.9;

                    overlapCoeff = 1 + 0.001 * Math.Ceiling((1.2 * Rs * diameterMm) / (2.5 * mm32 * Rbt * 4 * 11.75));

                }

                countPM = 0.1 * Math.Round(lengthMm * count * overlapCoeff / 100, MidpointRounding.AwayFromZero);
            }
            else
            {
                double m1 = WeightPerMeter * lengthMm;
                double m2 = Math.Ceiling(m1);
                double m3 = 0.001 * m2;
                double m4 = Math.Round(m3, 3, MidpointRounding.AwayFromZero);
                WeightOnePcs = m4;
                countPM = count;
                overlapCoeff = 1;
            }

            double wf1 = countPM * WeightOnePcs;
            double wf2 = 10000 * wf1;
            double wf3 = Math.Round(wf2);
            double wf4 = wf3 * 0.01;
            double wf5 = Math.Round(wf4, MidpointRounding.AwayFromZero);
            double wf6 = 0.01 * wf5;
            weightFinal = wf6;

            IsValid = true;
        }


        private void SetLength(bool rebarIsFamily)
        {
            double l = 0;
            if (rebarIsFamily)
            {
                Parameter lengthParam = ParametersSupport.GetParameter(elem, "Рзм.Длина");
                if (lengthParam == null) return;
                l = lengthParam.AsDouble() * 304.8;
            }
            else
            {
                l = elem.LookupParameter("Длина стержня").AsDouble() * 304.8;
            }
            double roundLength = 5 * Math.Round(l / 5, MidpointRounding.AwayFromZero);
            lengthMm = roundLength;
        }

        //private void SetOverlapCoeff(double concreteClass)
        //{
        //}


        private double GetRs()
        {
            if (rebarClass == 240) return 215;
            if (rebarClass == 400) return 355;
            if (rebarClass == 500) return 435;
            throw new Exception("Некорректный класс арматуры " + rebarClass.ToString() + " у элемента id:" + elem.Id.IntegerValue.ToString());
        }

        private double GetRbt(double concreteClass)
        {
            //double concreteClass = 0;
            //if (!CommandRebarWeight.haveConcreteClass)
            //{
            //    concreteClass = CommandRebarWeight.defaultConcreteClass;
            //}
            //else
            //{
            //    Parameter p = ParametersSupport.GetParameter(elem, "Арм.КлассБетона");
            //    if (p == null)
            //    {
            //        FormConcreteClass form = new FormConcreteClass();
            //        form.ShowDialog();
            //        if (form.DialogResult != System.Windows.Forms.DialogResult.OK) return -1;
            //        CommandRebarWeight.defaultConcreteClass = form.concreteClass;
            //        concreteClass = form.concreteClass;
            //        CommandRebarWeight.haveConcreteClass = false;
            //    }
            //    else
            //    {
            //        concreteClass = p.AsDouble();
            //    }
            //}


            if (concreteClass == 10) return 0.56;
            if (concreteClass == 15) return 0.75;
            if (concreteClass == 20) return 0.9;
            if (concreteClass == 25) return 1.05;
            if (concreteClass == 30) return 1.15;
            if (concreteClass == 35) return 1.3;
            if (concreteClass == 40) return 1.4;

            return Settings.DefaultConcreteClass;
        }


        private double GetWeightPerMeter(int diameterMm)
        {
            switch (diameterMm)
            {
                case 3: return 0.055;
                case 4: return 0.098;
                case 5: return 0.153;
                case 6: return 0.222;
                case 8: return 0.395;
                case 10: return 0.617;
                case 12: return 0.888;
                case 14: return 1.208;
                case 16: return 1.578;
                case 18: return 1.998;
                case 20: return 2.465;
                case 22: return 2.984;
                case 25: return 3.85;
                case 28: return 4.83;
                case 32: return 6.31;
                case 36: return 7.99;
                case 40: return 9.865;
            }
            double WeightCalc = 7.85 * (Math.PI * (diameterMm ^ 2) / 4000);
            return WeightCalc;
        }
    }
}
