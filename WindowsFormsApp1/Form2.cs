using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.Collections.Concurrent;
using System.Threading;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private BarDict myDict = new BarDict();  // Объект словаря с барами
        private DataTable dt = new DataTable();
        
        // Конструктор класса формы
        public Form1()
        {
            InitializeComponent();
            openFileDialog1.Filter = "Text files(*.txt)|*.txt|All files(*.*)|*.*";
            saveFileDialog1.Filter = "Text files(*.txt)|*.txt|All files(*.*)|*.*";
        }

        public struct SBar
        {
            public double open, high, low, close, vol;
        }

        // Класс для поиска входов в сделку
        class SearchEntrance
        {
            public Boolean LongTrade; // Сделка будет в лонг или в шорт
            public double SRHigh;     // Цена открытия серии шорт или закрытия серии лонг
            public double SRLow;      // Цена закрытия серии шорт или открытия серии лонг
            public double Recoil;     // Коэффициент отскока - доля от размера серии SR
            public double SL;         // Цена стоп-лосса
            public double TP;         // Цена тейк-профита
            private int NBarLong;     // Подсчитанное количество баров в серии лонг
            private int NBarShort;    // Подсчитанное количество баров в серии шорт
            public int NB;            // Количество однонаправленных баров в серии
            public double PriceStep;  // Шаг цены  
            // Инициализация класса
            public SearchEntrance() { LongTrade = true; SRHigh = 0; SRLow = 0; Recoil = 0.3330F; SL = 0; TP = 0; NBarLong = 0; NBarShort = 0; NB = 3; PriceStep = 10; }
            
            // Метод для поиска входов в сделку
            // В качестве параметров принимает время начала бара и структуру бара
            public void NewBar(DateTime DTbar,SBar Bar, out Boolean NewTrade)
            {
                NewTrade = false;
                if (Bar.open < Bar.close) // Если бар в лонг
                {
                    NBarLong++;           // Добавляем счетчик лонг
                    if (NBarShort >= NB && NBarLong == 1) // Если была серия баров в шорт и один бар в лонг
                    {
                        TP = SRLow + (Math.Round(((SRHigh - SRLow) * Recoil) / PriceStep) * PriceStep); // Вычисляем тейк-профит с приведением к шагу цены
                        if (TP > Bar.close)
                        {
                            NewTrade = true;  // Даем сигнал на сделку в лонг
                            LongTrade = true;
                            SL = Bar.low-10;
                        }
                    }
                    if (NBarLong == 1) { SRLow = Bar.open; }
                    SRHigh = Bar.close;
                    NBarShort = 0;    // Обнуляем счетчик шорт
                }
                if (Bar.open > Bar.close) // Если бар в шорт
                {
                    NBarShort++;          // Добавим счетчик баров шорт
                    if (NBarLong >= NB && NBarShort == 1) // Если была серия баров в лонг
                    {
                        TP = SRHigh - (Math.Round(((SRHigh - SRLow) * Recoil) / PriceStep) * PriceStep); // Вычисляем тейк-профит с приведением к шагу цены
                        if (TP < Bar.close)
                        {
                            NewTrade = true;  // Даем сигнал на сделку в шорт
                            LongTrade = false;
                            SL = Bar.high+10;
                        }
                    }
                    if (NBarShort == 1) { SRHigh = Bar.open; }
                    SRLow = Bar.close;
                    NBarLong = 0;     // Обнуляем счетчик лонг
                }
            }

        }

        // Класс сделка
        class Trade
        {
            public Boolean LongTrade;        // Сделка в лонг или в шорт
            public Boolean SLEndTrade;       // Тип выхода по стоп-лоссу или тейк-профиту
            public double SL;                // Цена стоп-лосса
            public double TP;                // Цена тейк-профита
            public double PriceStop;         // Цена выхода из сделки
            public int NBar;                 // Количество баров, прошедших с начала сделки
            public Boolean UndefinedResult;  // Неопределенный результат сделки 
            // Инициализация класса
            public Trade() { LongTrade = true; SLEndTrade = true; SL = 0; TP = 0; PriceStop = 0; NBar = 0; UndefinedResult = false; }
            
            // Метод для поиска выходов из сделки
            // В качестве параметров принимает время начала бара и структуру бара
            // Выходной параметр - флаг закрытия сделки на текущем баре
            public void NewBar(DateTime DTbar, SBar Bar, out Boolean EndTrade)
            {
                UndefinedResult = false;
                EndTrade = false;
                NBar++;
                if (LongTrade)
                {
                    UndefinedResult = ((Bar.high >= TP) && (Bar.low <= SL)); // Если на текущем таймфрейме бар одновременно пересекает стоп-лосс и тейк-профит
                }                                                         // результат сделки неопределен, выставляем флаг
                else
                {
                    UndefinedResult = ((Bar.high <= TP) && (Bar.low >= SL)); // 
                }
                if (LongTrade)                           // Если сделка открыта в лонг
                {
                    if (Bar.low <= SL)
                    {
                        EndTrade = true;                 // Выставляем флаг закрытия сделки
                        PriceStop = SL;                  // Указываем цену закрытия
                        SLEndTrade = true;
                        NBar = 0;
                    }
                    if (Bar.high >= TP)
                    {
                        EndTrade = true;                 // Выставляем флаг закрытия сделки
                        PriceStop = TP;                  // Указываем цену закрытия
                        SLEndTrade = false;
                        NBar = 0;
                    }
                }
                else                                     // Иначе, если сделка открыта в шорт
                {
                    if (Bar.high >= SL)
                    {
                        EndTrade = true;                 // Выставляем флаг закрытия сделки
                        PriceStop = SL;                  // Указываем цену закрытия
                        SLEndTrade = true;
                        NBar = 0;
                    }
                    if (Bar.low <= TP)
                    {
                        EndTrade = true;                 // Выставляем флаг закрытия сделки
                        PriceStop = TP;                  // Указываем цену закрытия
                        SLEndTrade = false;
                        NBar = 0;
                    }

                }
            }

        }


        private void Button2_Click(object sender, EventArgs e)
        {
            if (dateTimePicker1.Value > dateTimePicker2.Value)
            {
                MessageBox.Show("Начальная дата больше конечной");
                return;
            }
            if (saveFileDialog1.ShowDialog() == DialogResult.Cancel)
            {
                return;
            }
            // получаем выбранный файл
            string filename = saveFileDialog1.FileName;
            // сохраняем текст в файл
            TimeSpan timeSpan = dateTimePicker2.Value - dateTimePicker1.Value;
            Console.WriteLine(timeSpan.TotalSeconds);
            string[] DTStr = new string[(int)timeSpan.TotalSeconds];
            Random rnd = new Random();
            progressBar1.Value = 0;
            progressBar1.Maximum = (int)timeSpan.TotalSeconds;
            for (int i = 0; i < (int)timeSpan.TotalSeconds; i++)
            {
                int volume = rnd.Next(0, (int)numericUpDown1.Value);
                if (numericUpDown2.Value > 0 && (dateTimePicker3.Value == dateTimePicker1.Value.AddSeconds(i))) // организуем выброс объема
                {
                    volume = volume + (int)numericUpDown2.Value;
                }

                DTStr[i] = dateTimePicker1.Value.AddSeconds(i).ToString("dd/MM/yyyy HH:mm:ss")+"="+ volume.ToString();
//                DTStr[i] = dateTimePicker1.Value.AddSeconds(i).ToString("G", CultureInfo.CreateSpecificCulture("en-us")) + "=" + volume.ToString();
                progressBar1.Value = i;
            }
            System.IO.File.WriteAllLines(filename, DTStr);
            textBox1.Text += DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " Записано " + timeSpan.TotalSeconds + " значений" + "\r\n";
            progressBar1.Value = 0;
        }

        // Класс словаря с дополнительными методами
        class BarDict : Dictionary<DateTime, SBar>
        {

            // Метод, реализующий чтение данных из файла в словарь
            // Формат файла - строки вида: "01.01.2018 00:00:00=83",
            // где первые 19 символов включая пробел - дата и время,
            // далее сепаратор "=", затем значение объема Int
            // Внимание - проверка формата файла отсутствует!
            public void ReadFromFile(string filename)
            {
                var content = File.ReadAllLines(filename);
                CultureInfo enUS = new CultureInfo("en-US");
                SBar bar = new SBar();
                foreach (var line in content)
                {
                    var spl = line.Split(',');
                    if (spl[0] == "<TICKER>") continue;
  //                Console.WriteLine(spl[2] + " " + spl[3]);
                    DateTime.TryParseExact(spl[2]+" "+spl[3], "yyyyMMdd HHmmss", enUS, DateTimeStyles.None, out DateTime dt);
  //                Console.WriteLine(dt);
                    bar.open = double.Parse(spl[4]);
                    bar.high = double.Parse(spl[5]);
                    bar.low = double.Parse(spl[6]);
                    bar.close = double.Parse(spl[7]);
                    bar.vol = double.Parse(spl[8]);
                    this.Add(dt, bar);
                }
            }

            // Метод, реализующий подсчет максимального объема в таймфрейме в одном потоке
            // Входные параметры:
            // timeframe - размер таймфрейма в секундах по которому будет суммироваться объем
            // firsttick - первая секунда всего диапазона обработки
            // Выходные параметры:
            // maxvol - расчитанный максимальный объем
            // dtvol - начальная дата и время таймфрейма, в котором найден максимальный объем
            public void CalcMaxVolume(int timeframe, DateTime firsttick, out int maxvol, out DateTime dtvol)
            {
                int tempvol; // Переменная для расчета объема в таймфрейме
                maxvol = 0;  // Максимальный объем в таймфрейме
                dtvol = firsttick;

                for (int i = 0; i < Math.Ceiling(Convert.ToDouble(this.Count) / timeframe); i++)
                {
                    tempvol = 0;
                    DateTime j = firsttick.AddSeconds(i * timeframe);
                    DateTime jm = firsttick.AddSeconds((i + 1) * timeframe);
  //                  MyThread Th = new MyThread(j, jm);
                    while (j < jm)
                    {
//                        tempvol = tempvol + this[j];
                        j = j.AddSeconds(1);
                    }
                    if (maxvol < tempvol)
                    {
                        maxvol = tempvol;
                        dtvol = firsttick.AddSeconds(i * timeframe);
                    }
                }
            }

        }

        // Метод Итератор - последовательно проходит по всем барам словаря, передавая их в класс поиска сделки
        // Если сделка открыта - передает бар также в класс сделки, для ее проверки на закрытие 
        public void Iterator()
        {
            Boolean FlagTrade = false;                                        // Флаг возникновения условия новой сделки или закрытия сделки
            Boolean OnTrade = false;                                          // Позиция открыта
            Boolean OnTradeLong = true;                                       // Позиция открыта в лонг
            double CurrentSL = 0;                                             // Стоп-лосс текущей открытой сделки
            double CurrentTP = 0;                                             // Тейк-профит текущей открытой сделки
            double PriceOpenTrade = 0;                                        // Цена входа в сделку
            int NBarTrade = 0;                                                // Количество баров с начала сделки
            double Profit = 0;
            double Equity = 0;
            SearchEntrance SearchEntranceObj = new SearchEntrance();
            Trade TradeObj = new Trade();
            int J = 0;                                                        // Количество обработанных баров
            int I = 0;                                                        // Количество сделок
            String DirectionTrade = "";

            foreach (KeyValuePair<DateTime, SBar> kvp in myDict)              // Перечисляем все бары из словаря
            {
                J++;
                if (OnTrade)                                                  // Если сделка открыта передаем новый бар объекту "Сделка"
                {
                    TradeObj.SL = CurrentSL;                                  // Передаем текущие параметры стоп-лосса
                    TradeObj.TP = CurrentTP;                                  // и тейк-профита
                    TradeObj.LongTrade = OnTradeLong;                         // Укажем направление сделки
                    NBarTrade = TradeObj.NBar;
                    TradeObj.NewBar(kvp.Key, kvp.Value, out FlagTrade);
                    if (FlagTrade)                                            // Флаг выставляется в случае закрытия сделки
                    {
                        OnTrade = !FlagTrade;                                 // Если сделка закроется оповестим об этом Итератор
                        if (TradeObj.SLEndTrade)
                        {
                            DirectionTrade = "SL";
                        }
                        else
                        {
                            DirectionTrade = "TP";
                        }
                        if (TradeObj.LongTrade)
                        {
                            Profit = TradeObj.PriceStop - PriceOpenTrade;
                        }
                        else
                        {
                            Profit = PriceOpenTrade - TradeObj.PriceStop;
                        }
                        Equity = Equity + Profit;
                        dt.Rows.Add(I.ToString(), kvp.Key.Date.ToString("dd.MM.yyyy"), kvp.Key.ToString("HH:mm:ss"), DirectionTrade, TradeObj.PriceStop.ToString(), NBarTrade.ToString(), Profit.ToString(), Equity.ToString(), TradeObj.UndefinedResult.ToString());
                    }
                }
                SearchEntranceObj.NewBar(kvp.Key, kvp.Value, out FlagTrade);  // Передаем новый бар объекту "Поиск входа в сделку"
                if (FlagTrade)                                                // Если от объекта поступил сигнал на новую сделку
                {
                    I++;

                    if (!OnTrade)                                             // Если нет открытой сделки, если сигнал на сделку имеется
                    {                                                         // текущие параметры не меняем, несмотря на сигнал на вход
                        CurrentSL = SearchEntranceObj.SL;                     // Запомним новые значения стоп-лосса
                        CurrentTP = SearchEntranceObj.TP;                     // и тейк-профита
                        OnTradeLong = SearchEntranceObj.LongTrade;            // Запомним направление сделки
                        OnTrade = FlagTrade;                                  // Запомним что теперь сделка 
                        if (OnTradeLong)
                        {
                            DirectionTrade = "Long";
                        }
                        else
                        {
                            DirectionTrade = "Short";
                        }
                        PriceOpenTrade = kvp.Value.close;
                        dt.Rows.Add(I.ToString(), kvp.Key.Date.ToString("dd.MM.yyyy"), kvp.Key.ToString("HH:mm:ss"), DirectionTrade, PriceOpenTrade.ToString(), " ", " ", " ", " ");
                    }
                }
            }
        }



        private void Button3_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.Cancel)
            {
                return;
            }
            DateTime StartProcess = DateTime.Now;
            myDict.ReadFromFile(openFileDialog1.FileName);
            Iterator();
//            if (radioButton1.Checked)
//            {
//                myDict.CalcMaxVolume((int)numericUpDown3.Value, dateTimePicker1.Value, out int MaxVolume, out DateTime DateTimeVolume);
//                textBox1.Text += DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " Максимальный объем " + MaxVolume + " Время начала таймфрейма " + DateTimeVolume.ToString("dd.MM.yyyy HH:mm:ss") + "\r\n";
//            }
            TimeSpan TimeProcess = DateTime.Now - StartProcess;
            textBox1.Text += "Затраченное время " + TimeProcess.TotalMilliseconds + " мс\r\n";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dataGridView1.DataSource = dt;
            dt.Columns.Add("№");
            dt.Columns.Add("Дата");
            dt.Columns.Add("Время");
            dt.Columns.Add("Направление");
            dt.Columns.Add("Цена");
            dt.Columns.Add("Кол. баров в сделке");
            dt.Columns.Add("Профит");
            dt.Columns.Add("Эквити");
            dt.Columns.Add("Ошибка");

            chart1.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Column;
            double x = 0.001;
            const int N = 1000;
            for (int i = 1; i < N; i++)
            {
               
                    double y = (x - 0.3) * (x - 0.3);
                    chart1.Series[0].Points.AddXY(x, y);
                    x = x + 0.001;
            }
        }
    }
}
