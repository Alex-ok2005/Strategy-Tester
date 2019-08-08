using System;
using System.IO;
using System.Collections.Generic;
//using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
//using System.Collections.Concurrent;
//using System.Threading;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private BarDict myDict = new BarDict();     // Объект словаря с барами
        private BarDict myDictErr = new BarDict();  // Объект словаря с барами меньшего таймфрейма для исправления ошибок
        private DataTable trades = new DataTable();
        private DataTable results = new DataTable();
        private DataTable ColorResults = new DataTable();
        private int blockSize = 20;                 // Видимое количество свечей


        // Конструктор класса формы
        public Form1()
        {
            InitializeComponent();
            openFileDialog1.Filter = "Text files(*.txt)|*.txt|All files(*.*)|*.*";
            saveFileDialog1.Filter = "Text files(*.txt)|*.txt|All files(*.*)|*.*";
        }

        // Структура бара
        public struct SBar
        {
            public double open, high, low, close, vol;
        }

        private Color[,] ColorsCells = new Color[10, 2]    // Массив цветов, второй индекс = 0 (оттенки зеленого), = 1 (оттенки красного) 
            {{Color.FromArgb(0xE2, 0xF5, 0xE2),Color.FromArgb(0xF2, 0xD9, 0xD9)},
             {Color.FromArgb(0xBD, 0xE6, 0xBD),Color.FromArgb(0xE3, 0xAE, 0xAE)},
             {Color.FromArgb(0x9E, 0xD9, 0x9E),Color.FromArgb(0xD6, 0x8D, 0x8D)},
             {Color.FromArgb(0x7F, 0xC9, 0x7F),Color.FromArgb(0xC9, 0x71, 0x71)},
             {Color.FromArgb(0x67, 0xBF, 0x67),Color.FromArgb(0xBD, 0x59, 0x59)},
             {Color.FromArgb(0x52, 0xB3, 0x52),Color.FromArgb(0xB0, 0x43, 0x43)},
             {Color.FromArgb(0x3D, 0xA8, 0x3D),Color.FromArgb(0xA6, 0x2D, 0x2D)},
             {Color.FromArgb(0x1C, 0x96, 0x1C),Color.FromArgb(0x9C, 0x1D, 0x1D)},
             {Color.FromArgb(0x0C, 0x8A, 0x0C),Color.FromArgb(0x8F, 0x0E, 0x0E)},
             {Color.FromArgb(0x00, 0x80, 0x00),Color.FromArgb(0x80, 0x00, 0x00)}};

        // Класс - поиск входов в сделку
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
            public int FalseBar;      // Размер ложного бара в ед. шага цены
            public double SL_TP;      // Отношение стоп-лосса к тейк-профиту в % 
            public double MaxLoss;    // Максимальная просадка
            public Boolean ModeMaxLoss;// Режим выставления стоп-лосса по максимальной просадке
            public int Lot;           // Число лот для торговли
            public int MaxLot;        // Максимально допустимое число лот

            // Инициализация класса
            public SearchEntrance() { LongTrade = true; SRHigh = 0; SRLow = 0; Recoil = 33; SL = 0; TP = 0; NBarLong = 0;
                NBarShort = 0; NB = 3; PriceStep = 10; FalseBar = 1; SL_TP = 25; MaxLoss = 0; ModeMaxLoss = false; Lot = 1; MaxLot = 1; }
            
            // Метод для поиска входов в сделку
            // В качестве параметров принимает время начала бара и структуру бара
            public void NewBar(DateTime DTbar,SBar Bar, out Boolean NewTrade)
            {
                NewTrade = false;
                if (Math.Abs(Bar.open - Bar.close) > (PriceStep * FalseBar)) // Не обрабатываем ложный бар
                {
                    if (Bar.open < Bar.close) // Если бар в лонг
                    {
                        NBarLong++;           // Добавляем счетчик лонг
                        if (NBarShort >= NB && NBarLong == 1) // Если была серия баров в шорт и один бар в лонг
                        {
                            TP = SRLow + (Math.Round(((SRHigh - SRLow) * Recoil / 100 ) / PriceStep) * PriceStep); // Вычисляем тейк-профит с приведением к шагу цены
                            SL = Bar.low - 10;                                                              // Вычисляем стоп-лосс
                            if (ModeMaxLoss)
                            {
                                Lot = (int)Math.Round(MaxLoss / (Bar.close - SL));                          // В режиме торговли по макс.просадке вычисляем число лот для торговли
                                if (Lot > MaxLot) { Lot = MaxLot; }
                            }
                            if ( (TP > Bar.close) &&                                                        // Проверяем условие непересечения ценой тейк-профита 
                                 ( (TP - Bar.close)/(Bar.close-SL)*100 > SL_TP) &&                          // Проверяем непревышение отношения стоп-лосса к тейк-профиту
                                 (Lot >= 1))                                                                // Проверяем соответсвие мин числу лот
                            {
                                NewTrade = true;  // Даем сигнал на сделку в лонг
                                LongTrade = true;
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
                            TP = SRHigh - (Math.Round(((SRHigh - SRLow) * Recoil / 100) / PriceStep) * PriceStep);// Вычисляем тейк-профит с приведением к шагу цены
                            SL = Bar.high + 10;                                                             // Вычисляем стоп-лосс
                            if (ModeMaxLoss)
                            {
                                Lot = (int)Math.Round(MaxLoss / (SL- Bar.close));                            // В режиме торговли по макс.просадке вычисляем число лот для торговли
                                if (Lot > MaxLot) { Lot = MaxLot; }
                            }
                            if ( (TP < Bar.close) &&                                                        // Проверяем условия не пересечения ценой тейк-профита
                                 ((Bar.close - TP) / (SL - Bar.close) * 100 > SL_TP) &&                     // Проверяем непревышение отношения стоп-лосса к тейк-профиту
                                 (Lot >= 1))                                                                // Проверяем соответсвие мин числу лот
                            {                                                                                
                                NewTrade = true;  // Даем сигнал на сделку в шорт
                                LongTrade = false;
                            }
                        }
                        if (NBarShort == 1) { SRHigh = Bar.open; }
                        SRLow = Bar.close;
                        NBarLong = 0;     // Обнуляем счетчик лонг
                    }
                }
            }

        }

        // Класс - сделка
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

        // Класс словаря с дополнительными методами
        class BarDict : Dictionary<DateTime, SBar>
        {

            // Метод, реализующий чтение данных из файла в словарь
            public Boolean ReadFromFile(string filename)
            {
                Boolean FirstLine = true;
                var content = File.ReadAllLines(filename);
                CultureInfo enUS = new CultureInfo("en-US");
                SBar bar = new SBar();
                try
                {
                    foreach (var line in content)
                    {
                        if (filename.IndexOf("MFD")>0)
                        {
                            if (FirstLine)                          // Первую строку пропускаем
                            {
                                FirstLine = false;
                                continue;
                            }
                            var spl = line.Split('\t');
                            DateTime.TryParseExact(spl[0], "dd.MM.yyyy HH:mm:ss", enUS, DateTimeStyles.None, out DateTime dt);
                            bar.open = double.Parse(spl[1]);
                            bar.high = double.Parse(spl[2]);
                            bar.low = double.Parse(spl[3]);
                            bar.close = double.Parse(spl[4]);
                            bar.vol = double.Parse(spl[5]);
                            this.Add(dt, bar);

                        }
                        else
                        {
                            if (FirstLine)
                            {
                                FirstLine = false;
                                continue;
                            }
                            var spl = line.Split(',');
                            DateTime.TryParseExact(spl[2] + " " + spl[3], "yyyyMMdd HHmmss", enUS, DateTimeStyles.None, out DateTime dt);
                            bar.open = double.Parse(spl[4]);
                            bar.high = double.Parse(spl[5]);
                            bar.low = double.Parse(spl[6]);
                            bar.close = double.Parse(spl[7]);
                            bar.vol = double.Parse(spl[8]);
                            this.Add(dt, bar);
                        }
                    }
                    return true;
                }
                catch
                {
                    MessageBox.Show(
                    "Неверный формат файла",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.DefaultDesktopOnly);
                    return false;
                }
            }

            // Метод, реализующий чтение данных из файла в диапазоне дат 
            public Boolean ReadDateRangeFromFile(string filename, int bsize, out DateTime FirstDate, out DateTime EndDate, out double HighPrice, out double LowPrice )
            {
                Boolean FirstLine = true;
                FirstDate = DateTime.MinValue;
                EndDate = DateTime.MinValue;
                HighPrice = 0;
                LowPrice = 0;
                int Nbar = 0; 
                var content = File.ReadAllLines(filename);
                CultureInfo enUS = new CultureInfo("en-US");
                try
                {
                    foreach (var line in content)
                    {
                        if (filename.IndexOf("MFD") > 0)
                        {
                            if (FirstLine)                          // Первую строку пропускаем
                            {
                                FirstLine = false;
                                continue;
                            }
                            var spl = line.Split('\t');
                            DateTime.TryParseExact(spl[0], "dd.MM.yyyy HH:mm:ss", enUS, DateTimeStyles.None, out DateTime dt);
                            if (FirstDate == DateTime.MinValue)
                            {
                                FirstDate = dt;
                                EndDate = dt;
                                LowPrice = double.Parse(spl[3]);
                            }
                            else
                            {
                                EndDate = dt;
                                if (Nbar < bsize)
                                {
                                    if (HighPrice < double.Parse(spl[2])) { HighPrice = double.Parse(spl[2]); }
                                    if (LowPrice > double.Parse(spl[3])) { LowPrice = double.Parse(spl[3]); }
                                    Nbar++;
                                }
                                continue;
                            }
                        }
                        else
                        {
                            if (FirstLine)
                            {
                                FirstLine = false;
                                continue;
                            }
                            var spl = line.Split(',');
                            DateTime.TryParseExact(spl[2] + " " + spl[3], "yyyyMMdd HHmmss", enUS, DateTimeStyles.None, out DateTime dt);
                            if (FirstDate == DateTime.MinValue)
                            {
                                FirstDate = dt;
                                EndDate = dt;
                                LowPrice = double.Parse(spl[6]);
                            }
                            else
                            {
                                EndDate = dt;
                                if (Nbar < bsize)
                                {
                                    if (HighPrice < double.Parse(spl[5])) { HighPrice = double.Parse(spl[5]); }
                                    if (LowPrice > double.Parse(spl[6])) { LowPrice = double.Parse(spl[6]); }
                                    Nbar++;
                                }
                                continue;
                            }
                        }
                    }
                    return true;
                }
                catch
                {
                    MessageBox.Show(
                    "Неверный формат файла",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.DefaultDesktopOnly);
                    return false;
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
        public void Iterator(DateTime FirstDate, DateTime EndDate)
        {
            Boolean FlagTrade = false;                                        // Флаг возникновения условия новой сделки или закрытия сделки
            Boolean OnTrade = false;                                          // Позиция открыта
            Boolean OnTradeLong = true;                                       // Позиция открыта в лонг
            double CurrentSL = 0;                                             // Стоп-лосс текущей открытой сделки
            double CurrentTP = 0;                                             // Тейк-профит текущей открытой сделки
            double PriceOpenTrade = 0;                                        // Цена входа в сделку
            int NBarTrade = 0;                                                // Количество баров с начала сделки
            double ProfitTrade = 0;                                           // Профит по сделке                      
            double Profit = 0;                                                // Общий профит
            int CurrentLot = (int)numericUpDown2.Value;                       // Количество лот для торговли
            int NProfitTradesTotal = 0;                                       // Количество прибыльных сделок
            int NLossTradesTotal = 0;                                         // Количество убыточных сделок
            int NProfitTrades = 0;                                            // Счетчик количества прибыльных сделок подряд
            int NLossTrades = 0;                                              // Счетчик количества убыточных сделок подряд
            int MaxNProfitTrades = 0;                                         // Макс. количество прибыльных сделок подряд
            int MaxNLossTrades = 0;                                           // Макс. количество убыточных сделок подряд
            double MaxNProfit = 0;                                            // Максимальная непрерывная прибыль
            double MaxNLoss = 0;                                              // Максимальный непрерывный убыток
            double NProfit = 0;                                               // Счетчик непрерывной прибыли
            double NLoss = 0;                                                 // Счетчик непрерывного убытка
            DateTime LastDateTime = EndDate;                                  // Дата/время последнего бара
            double TotalProfit = 0;                                           // Общая прибыль
            double TotalLoss = 0;                                             // Общий убыток
            double AverageProfitTrade = 0;                                    // Средняя прибыльная сделка
            double AverageLossTrade = 0;                                      // Средняя убыточная сделка
            double ProfitFactor = 0;                                          // Профит-фактор
            double ProfitDecline = 0;                                         // Просадка
            double MaxProfitTrade = 0;                                        // Сохраненное значение локального максимума профита
            double MinProfitTrade = 0;                                        // Сохраненное значение локального минимума профита
            Boolean FixErr = false;                                           // Флаг исправления ошибок (неопределенностей рез-та сделки) для текущего бара основного таймфрейма
            Boolean ModeFixErr = checkBox2.Checked;                           // Режим исправления ошибок
            int Timeframe = (int)numericUpDown9.Value;                        // Основной таймфрейм

            int[,,] ProfitTradesArr = new int[24, 7, 2]                 // Массив для хранения результатов сделок по часам и дням недели
               { { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} },
                 { { 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0},{ 0,0} } };

            int TradeNDay = 0;                                                // Переменная для хранения номера дня текущей сделки
            int TradeNHour = 0;                                               // Переменная для хранения номера часа текущей сделки

            SearchEntrance SearchEntranceObj = new SearchEntrance             // Создаем объект для поиска сделки
            {                                                                 // Считываем из формы параметры стратегии
                PriceStep = (double)numericUpDown1.Value,
                ModeMaxLoss = radioButton2.Checked,
                Lot = (int)numericUpDown2.Value,
                MaxLoss = (int)numericUpDown7.Value,
                MaxLot = (int)numericUpDown2.Value,

                Recoil = (int)numericUpDown8.Value,
                SL_TP = (double)numericUpDown5.Value,
                FalseBar = (int)numericUpDown4.Value,
                NB = (int)numericUpDown3.Value                                // Задаем мин.количество баров в серии
            };          

            Trade TradeObj = new Trade();

            int J = 0;                                                        // Количество обработанных баров
            int I = 0;                                                        // Количество сделок
            int NErr = 0;                                                     // Количество ошибок "Неопределен результат сделки"
            String DirectionTrade = "";
            chart1.Series[0].Points.Clear();
            chart1.Series[1].Points.Clear();
            chart1.Series[2].Points.Clear();
            chart2.Series[0].Points.Clear();
            chart3.Series[0].Points.Clear();
            chart4.Series[0].Points.Clear();
            chart3.Series[1].Points.Clear();
            chart4.Series[1].Points.Clear();
            chart3.Series[2].Points.Clear();
            chart4.Series[2].Points.Clear();


            //            chart2.ChartAreas[0].AxisX.Minimum = FirstDate;
            //            chart2.ChartAreas[0].AxisX.Maximum = EndDate;

            chart2.ChartAreas[0].CursorX.AutoScroll = true;
            chart2.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            chart2.ChartAreas[0].CursorY.AutoScroll = true;
            chart2.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            //            chart2.ChartAreas[0].AxisX.ScaleView.SizeType = DateTimeIntervalType.Number;
            chart2.ChartAreas[0].AxisX.ScaleView.Zoom(FirstDate.ToOADate(), FirstDate.AddDays(2).ToOADate());

            // disable zoom-reset button (only scrollbar arrows are available)
            //            chart2.ChartAreas[0].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;

            // set scrollbar small change to blockSize (e.g. 100)
            //            chart2.ChartAreas[0].AxisX.ScaleView.SmallScrollSize = blockSize;

            trades.Rows.Clear();
            results.Rows.Clear();
            ColorResults.Rows.Clear();

            foreach (KeyValuePair<DateTime, SBar> kvp in myDict)              // Перечисляем все бары из словаря
            {
                if ((kvp.Key < FirstDate) || (kvp.Key > EndDate)) { continue; } // Если выход за заданный диапазон дат не обрабатываем этот бар
//                if ((EndDate > DateTime.MinValue) && ((kvp.Key < FirstDate) || (kvp.Key > EndDate))) { continue; } // Если выход за заданный диапазон дат не обрабатываем этот бар
                if (J == 0)
                { chart1.Series[2].Points.AddXY(kvp.Key, ProfitTrade); }      // Добавим на график точку с накопленным профитом
                chart1.Series[1].Points.AddXY(kvp.Key, 200);                  // Обозначим на графике пришедший бар
                chart2.Series[0].Points.AddXY(kvp.Key, kvp.Value.high);
                chart2.Series[0].Points[J].YValues[1] = kvp.Value.low;
                chart2.Series[0].Points[J].YValues[2] = kvp.Value.open;
                chart2.Series[0].Points[J].YValues[3] = kvp.Value.close;
                J++;                                                          // Прибавим счетчик баров

                // ----------------------------  Проверка открытой сделки ---------------------------------

                if (OnTrade)                                                  // Если сделка открыта передаем новый бар объекту "Сделка"
                {
                    TradeObj.SL = CurrentSL;                                  // Передаем текущие параметры стоп-лосса
                    TradeObj.TP = CurrentTP;                                  // и тейк-профита
                    TradeObj.LongTrade = OnTradeLong;                         // Укажем направление сделки
                    NBarTrade = TradeObj.NBar;                                // Запомним количество баров с начала сделки
                    TradeObj.NewBar(kvp.Key, kvp.Value, out FlagTrade);
                    FixErr = (ModeFixErr && TradeObj.UndefinedResult && myDictErr.ContainsKey(kvp.Key));  // Если установлен режим поиска ошибок,
                                                                                                          // результат сделки не определен (ошибка)
                                                                                                          // и словарь с барами меньшего таймфрейма содержит такое значение ключа
                                                                                                          // устанавливаем флаг исправления ошибок по меньшему таймфрейму       
                    if (FixErr)
                    {
                        var _myDictErr = myDictErr.Where(p => (p.Key >= kvp.Key && p.Key < (kvp.Key.AddMinutes(Timeframe))));
                        foreach (KeyValuePair<DateTime, SBar> _kvp in _myDictErr)  // Перечисляем все бары из словаря
                        {                                                          // перечисляем бары из словаря меньшего таймфрейма
                            TradeObj.NewBar(_kvp.Key, _kvp.Value, out FlagTrade);
                            if (FlagTrade) break;
                        }
                    }

                    if (FlagTrade)                                             // Флаг выставляется в случае закрытия сделки
                    {
                        OnTrade = !FlagTrade;                                  // Если сделка закроется оповестим об этом Итератор
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
                        if (TradeObj.UndefinedResult)
                        {
                            NErr++;
                            Profit = 0;
                        }
                        else { Profit = Profit * CurrentLot; }

                        // Расчет характеристик торговой системы
                        if (Profit > 0)                                       // Если сделка прибыльная
                        {
                            ProfitTradesArr[TradeNHour, TradeNDay-1, 0]++; 
                            NProfitTradesTotal++;                             // Прибавим счетчик общего количества прибыльных сделок
                            TotalProfit = TotalProfit + Profit;               // Общая прибыль

                            NProfitTrades++;                                  // Прибавим счетчик количества прибыльных сделок подряд
                            NLossTrades = 0;                                  // Обнулим счетчик количества убыточных сделок подряд
                            if (MaxNProfitTrades < NProfitTrades)
                            { MaxNProfitTrades = NProfitTrades; }

                            NProfit = NProfit + Profit;
                            NLoss = 0;
                            if (MaxNProfit < NProfit)
                            { MaxNProfit = NProfit; }
                        }
                        if (Profit < 0)                                       // Если сделка убыточная
                        {
                            ProfitTradesArr[TradeNHour, TradeNDay-1, 1]++;
                            NLossTradesTotal++;
                            TotalLoss = TotalLoss + Profit;                   // Общий убыток

                            NLossTrades++;                                    // Прибавим счетчик количества прибыльных сделок подряд
                            NProfitTrades = 0;                                // Обнулим счетчик количества убыточных сделок подряд
                            if (MaxNLossTrades < NLossTrades)
                            { MaxNLossTrades = NLossTrades; }

                            NLoss = NLoss + Profit;
                            NProfit = 0;
                            if (MaxNLoss > NLoss)
                            { MaxNLoss = NLoss; }
                        }
                        ProfitTrade = ProfitTrade + Profit;

                        if (ProfitTrade > MaxProfitTrade) { MaxProfitTrade = ProfitTrade; MinProfitTrade = ProfitTrade; } // Обновим максимум и минимум профита если профит вырос выше локального максимума
                        if (ProfitTrade < MinProfitTrade) { MinProfitTrade = ProfitTrade; } // Если профит упал ниже локального минимума, обновим его
                        if (ProfitDecline < (MaxProfitTrade - MinProfitTrade)) { ProfitDecline = (MaxProfitTrade - MinProfitTrade); } // Если новое значение просадки больше сохраненного - обновим его

                        trades.Rows.Add(I.ToString(), kvp.Key.Date.ToString("dd.MM.yyyy"), kvp.Key.ToString("HH:mm:ss"), DirectionTrade, TradeObj.PriceStop.ToString(), NBarTrade.ToString(), Profit.ToString(), ProfitTrade.ToString(), TradeObj.UndefinedResult.ToString());
                        chart1.Series[0].Points.AddXY(kvp.Key, ProfitTrade);  // Добавим на график медиану
                    }
                }

                // ----------------------------  Поиск новой сделки ---------------------------------
                #region Search new trade

                SearchEntranceObj.NewBar(kvp.Key, kvp.Value, out FlagTrade);  // Передаем новый бар объекту "Поиск входа в сделку"
                if (FlagTrade)                                                // Если от объекта поступил сигнал на новую сделку
                {
                    I++;
                    
                    if (!OnTrade)                                             // Если нет открытой сделки, если сигнал на сделку имеется
                    {                                                         // то текущие параметры не меняем, несмотря на сигнал на вход
                        CurrentSL = SearchEntranceObj.SL;                     // Запомним новые значения стоп-лосса
                        CurrentTP = SearchEntranceObj.TP;                     // тейк-профита
                        CurrentLot = SearchEntranceObj.Lot;                   // лота
                        OnTradeLong = SearchEntranceObj.LongTrade;            // Запомним направление сделки
                        OnTrade = FlagTrade;                                  // Запомним что теперь сделка 
                        TradeNDay = (int)kvp.Key.DayOfWeek;                   // Запомним номер дня текущей сделки
                        TradeNHour = kvp.Key.Hour;                            // Запомним номер часа текущей сделки

                        if (OnTradeLong)
                        {
                            DirectionTrade = "Long";
                        }
                        else
                        {
                            DirectionTrade = "Short";
                        }
                        PriceOpenTrade = kvp.Value.close;                     // Запомним цену открытия сделки по цене закрытия бара, хотя правильнее надо по цене открытия след.бара
                        trades.Rows.Add(I.ToString(), kvp.Key.Date.ToString("dd.MM.yyyy"), kvp.Key.ToString("HH:mm:ss"), DirectionTrade, PriceOpenTrade.ToString(), " ", " ", " ", " ");
                    }
                }
                LastDateTime = kvp.Key;
            }
            #endregion

            // -------------------------  Выводим на график количества сделок по часам и дням недели --------------------------------
            #region Fill Chart3

            for (int i = 10; i < 24; i++)                            
            {
                NLossTrades = 0;
                NProfitTrades = 0;
                for (int j = 0; j < 7; j++)
                {
                    NLossTrades += ProfitTradesArr[i, j, 1];
                    NProfitTrades += ProfitTradesArr[i, j, 0];
                }
                chart3.Series[0].Points.AddXY(i, NLossTrades);
                chart3.Series[1].Points.AddXY(i, NProfitTrades);
                chart3.Series[2].Points.AddXY(i, NProfitTrades-NLossTrades);
            }

            for (int i = 0; i < 7; i++)                          
            {
                NLossTrades = 0;
                NProfitTrades = 0;
                for (int j = 10; j < 24; j++)
                {
                    NLossTrades += ProfitTradesArr[j, i, 1];
                    NProfitTrades += ProfitTradesArr[j, i, 0];
                }
                chart4.Series[0].Points.AddXY(i, NLossTrades);
                chart4.Series[1].Points.AddXY(i, NProfitTrades);
                chart4.Series[2].Points.AddXY(i, NProfitTrades - NLossTrades);
            }

            #endregion
            // ---------------------------------------------------------------------------------------------------------------------------


            chart1.Series[2].Points.AddXY(LastDateTime, ProfitTrade);

            AverageProfitTrade = Math.Round(TotalProfit / NProfitTradesTotal);
            AverageLossTrade = Math.Round(TotalLoss / NLossTradesTotal);
            ProfitFactor = Math.Round((TotalProfit / -TotalLoss)/0.01)*0.01;

            results.Rows.Add("Количество обработанных баров", J.ToString(),"","");
            results.Rows.Add("Количество сделок", I.ToString(), "", "");
            results.Rows.Add("Количество ошибок", NErr.ToString(), "", "");
            results.Rows.Add("Прибыль/Убыток", ProfitTrade.ToString(), "", "");
            results.Rows.Add("Общая прибыль", TotalProfit.ToString(), "", "");
            results.Rows.Add("Общий убыток", TotalLoss.ToString(), "", "");
            results.Rows.Add("Прибыльних сделок", NProfitTradesTotal.ToString(), "", "");
            results.Rows.Add("Убыточных сделок", NLossTradesTotal.ToString(), "", "");
            results.Rows.Add("Макс. серия прибыльных сделок", MaxNProfitTrades.ToString(), "", "");
            results.Rows.Add("Макс. серия убыточных сделок", MaxNLossTrades.ToString(), "", "");
            results.Rows.Add("Макс. непрерывная прибыль", MaxNProfit.ToString(), "", "");
            results.Rows.Add("Макс. неприрывный убыток", MaxNLoss.ToString(), "", "");
            results.Rows.Add("Средняя прибыльная сделка", AverageProfitTrade.ToString(), "", "");
            results.Rows.Add("Средняя убыточная сделка", AverageLossTrade.ToString(), "", "");
            results.Rows.Add("Профит-фактор", ProfitFactor.ToString(), "", "");
            results.Rows.Add("Максимальная просадка", ProfitDecline.ToString(), "", "");




            int MaxValue = 0;
            int MinValue = 0;
            for (int i = 0; i < 24; i++)                            // Находим максимальное и минимальное значение в массиве
            {
                for (int j = 0; j < 7; j++)
                {
                    if (MaxValue < (ProfitTradesArr[i, j, 0] - ProfitTradesArr[i, j, 1])) { MaxValue = (ProfitTradesArr[i, j, 0] - ProfitTradesArr[i, j, 1]); }
                    if (MinValue > (ProfitTradesArr[i, j, 0] - ProfitTradesArr[i, j, 1])) { MinValue = (ProfitTradesArr[i, j, 0] - ProfitTradesArr[i, j, 1]); }
                }
            }

            //ColorResults.Rows.Add("14", "", "", "", "", "", "");
//            dataGridView2.Rows[1].Cells[3].Style.BackColor = ColorsCells[(int)Math.Ceiling(Convert.ToDouble(4 * 10 / 10)) - 1, 0];

            for (int i = 10; i < 24; i++)                            // Цикл по номеру часа. Выводим только время работы сессии 
            {
                ColorResults.Rows.Add(i.ToString(), "", "", "", "", "", "");
                for (int j = 0; j < 7; j++)                          // Цикл по номеру дня (начинаем с нуля)
                {
                    double DeltaNTrades = ProfitTradesArr[i, j, 0] - ProfitTradesArr[i, j, 1];
                    dataGridView2.Rows[i - 10].Cells[j].Value = ProfitTradesArr[i, j, 0].ToString() + " : " + ProfitTradesArr[i, j, 1].ToString() + " = " + DeltaNTrades.ToString();
                    if (DeltaNTrades > 0)
                    {
                        dataGridView2.Rows[i - 10].Cells[j].Style.BackColor = ColorsCells[(int)Math.Ceiling(Convert.ToDouble(DeltaNTrades * 10 / MaxValue)) - 1, 0];
                    }
                    if (DeltaNTrades < 0)
                    {
                        dataGridView2.Rows[i-10].Cells[j].Style.BackColor = ColorsCells[(int)Math.Ceiling(Convert.ToDouble(DeltaNTrades * 10 / MinValue)) - 1, 1];
                    }
                }
            }



            TradeObj = null;
            SearchEntranceObj = null;
        }




        // Старт тестирования стратегии
        private void Button_start_Click(object sender, EventArgs e)
        {
            FileInfo fi = new FileInfo(comboBox1.Text);
            this.Text = "Тестер стратегий Recoil: " + fi.Name;
            fi = null;
            DateTime StartProcess = DateTime.Now;
            if (myDict.ReadFromFile(comboBox1.Text))
            {
                if (checkBox2.Checked) { myDictErr.ReadFromFile(comboBox2.Text); }
                Iterator(dateTimePicker1.Value, dateTimePicker2.Value); 
            }
            myDict.Clear();
            myDictErr.Clear();
            //            if (radioButton1.Checked)
            //            {
            //                myDict.CalcMaxVolume((int)numericUpDown3.Value, dateTimePicker1.Value, out int MaxVolume, out DateTime DateTimeVolume);
            //                textBox1.Text += DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " Максимальный объем " + MaxVolume + " Время начала таймфрейма " + DateTimeVolume.ToString("dd.MM.yyyy HH:mm:ss") + "\r\n";
            //            }
            TimeSpan TimeProcess = DateTime.Now - StartProcess;
            results.Rows.Add("Затраченное время", TimeProcess.TotalMilliseconds + " мс\r\n","","");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dataGridView1.DataSource = trades;
            trades.Columns.Add("№");
            trades.Columns.Add("Дата");
            trades.Columns.Add("Время");
            trades.Columns.Add("Направление");
            trades.Columns.Add("Цена");
            trades.Columns.Add("Кол. баров в сделке");
            trades.Columns.Add("Прибыль/Убыток");
            trades.Columns.Add("Общ.прибыль/убыток");
            trades.Columns.Add("Ошибка");

            dataGridView3.DataSource = results;
            results.Columns.Add("Показатель");
            results.Columns.Add("Всего");
            results.Columns.Add("Long");
            results.Columns.Add("Short");
            dataGridView3.Columns[0].Width = 250;
            dataGridView3.Columns[1].Width = 70;
            dataGridView3.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridView3.Columns[2].Width = 70;
            dataGridView3.Columns[3].Width = 70;

//            .EnableHeadersVisualStyles = false;


            dataGridView2.DataSource = ColorResults;
            ColorResults.Columns.Add("Пн.");
            ColorResults.Columns.Add("Вт.");
            ColorResults.Columns.Add("Ср.");
            ColorResults.Columns.Add("Чт.");
            ColorResults.Columns.Add("Пт.");
            ColorResults.Columns.Add("Сб.");
            ColorResults.Columns.Add("Вс.");

            chart1.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Area;
            chart1.Series[1].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Column;
            chart1.Series[2].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "dd.MM.yy";
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.Cancel)
            {
                return;
            }
            comboBox1.Items.Add(openFileDialog1.FileName);
            comboBox1.Text = openFileDialog1.FileName;

            if (myDict.ReadDateRangeFromFile(comboBox1.Text, blockSize, out DateTime FDate, out DateTime EDate, out double HPrice, out double LPrice))
            {
                label5.Text = FDate.ToString() + " - " + EDate.ToString();
                dateTimePicker1.Value = FDate;
                dateTimePicker2.Value = EDate;
                button_start.Enabled = true;
                chart2.ChartAreas[0].AxisY.ScaleView.Zoom((HPrice - LPrice) * -0.1 + LPrice, (HPrice - LPrice) * 0.1+ HPrice);
            }
        }

        private void RadioButton2_CheckedChanged(object sender, EventArgs e)
        {
            numericUpDown7.Enabled = radioButton2.Checked;
        }

        private void RadioButton1_CheckedChanged(object sender, EventArgs e)
        {
            numericUpDown2.Enabled = radioButton1.Checked;
        }

        private void ComboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            if (myDict.ReadDateRangeFromFile(comboBox1.Text, blockSize, out DateTime FDate, out DateTime EDate, out double HPrice, out double LPrice))
            {
                label5.Text = FDate.ToString() + " - " + EDate.ToString();
                dateTimePicker1.Value = FDate;
                dateTimePicker2.Value = EDate;
                button_start.Enabled = true;
                chart2.ChartAreas[0].AxisY.ScaleView.Zoom((HPrice - LPrice) * -0.1 + LPrice, (HPrice - LPrice) * 0.1 + HPrice);
            }
        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            dateTimePicker1.Enabled = checkBox1.Checked;
            dateTimePicker2.Enabled = checkBox1.Checked;
        }

        // Выбор файла с данными меньшего таймфрейма для поиска ошибок (неопределенный результат сделки)
        // Не работает для когда младший таймфрейм не кратен основному, например основной: 10 мин., а младший: 3 мин.
        private void Button4_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.Cancel)
            {
                return;
            }
            comboBox2.Items.Add(openFileDialog1.FileName);
            comboBox2.Text = openFileDialog1.FileName;
        }

        private void CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            button4.Enabled = checkBox2.Checked;
            comboBox2.Enabled = checkBox2.Checked;
        }
    }
}
