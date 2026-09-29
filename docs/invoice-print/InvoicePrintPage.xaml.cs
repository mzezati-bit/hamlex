using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace hamlex.Views.Prints
{
    public partial class InvoicePrintPage : Page
    {
        private const double DesignWidth = 794;
        private const double DesignHeight = 1123;

        private string _destination = "";
        private string _agent = "";
        private string _driver = "";
        private string _vehicleType = "";
        private string _plate = "";

        public InvoicePrintPage()
        {
            InitializeComponent();
        }

        public InvoicePrintPage(
            string invoiceNumber,
            string dateTime,
            string destination,
            string agent,
            string driver,
            string vehicleType,
            string plateNumber)
        {
            InitializeComponent();
            Loaded += InvoicePrintPage_Loaded;

            _destination = destination ?? "";
            _agent = agent ?? "";
            _driver = driver ?? "";
            _vehicleType = vehicleType ?? "";
            _plate = plateNumber ?? "";

            var parts = (dateTime ?? "").Split(new[] { " - " }, StringSplitOptions.None);
            PrintInvoiceNumberTextBlock.Text = "شماره صورتحساب : " + invoiceNumber;
            PrintDateTextBlock.Text = "تاریخ : " + (parts.Length > 0 ? parts[0] : "");
            PrintTimeTextBlock.Text = "ساعت : " + (parts.Length > 1 ? parts[1] : "");
            PrintDestinationTextBlock.Text = "مقصد : " + _destination;
            PrintAgentTextBlock.Text = "نماینده : " + _agent;
            PrintDriverTextBlock.Text = "راننده : " + _driver;
            PrintVehicleTextBlock.Text = "خودرو : " + _vehicleType + " - " + _plate;
        }


        public void FillWaybillRows(IEnumerable items)
        {
            PrintItemsHost.Children.Clear();
            int row = 1;
            decimal totalQty = 0, totalPayable = 0, totalPrepaid = 0, totalRemaining = 0;

            foreach (var item in items)
            {
                if (item == null || item.ToString() == "{NewItemPlaceholder}")
                    continue;

                string number = GetProp(item, "WaybillNumber");
                if (string.IsNullOrWhiteSpace(number))
                    continue;

                string sender = GetProp(item, "SenderName");
                string receiver = GetProp(item, "ReceiverName");
                string dest = FirstProp(item, "DestinationCity", "DestinationCityName", "Destination");
                string cargo = FirstProp(item, "CargoTypeName", "CargoType");
                string qty = FirstProp(item, "TotalQuantity", "Quantity");
                string payable = FirstProp(item, "Payable");
                string prepaid = FirstProp(item, "Prepaid");
                string remaining = FirstProp(item, "Remaining");

                totalQty += ToDec(qty);
                totalPayable += ToDec(payable);
                totalPrepaid += ToDec(prepaid);
                totalRemaining += ToDec(remaining);

                PrintItemsHost.Children.Add(MakeRow(
                row.ToString(), number, sender, receiver, dest, cargo,
                N0(qty), N0(payable), N0(prepaid), N0(remaining)));
                row++;
            }

            PrintTotalQtyTextBlock.Text = "جمع تعداد : " + totalQty.ToString("#,##0");
            PrintTotalPayableTextBlock.Text = "جمع قابل پرداخت : " + totalPayable.ToString("#,##0");
            PrintTotalPrepaidTextBlock.Text = "جمع پیشکرایه : " + totalPrepaid.ToString("#,##0");
            PrintTotalRemainingTextBlock.Text = "جمع مانده/پسکرایه : " + totalRemaining.ToString("#,##0");

            PrintLegalTextBlock.Text =
                "اینجانب " + _driver + " راننده خودروی " + _vehicleType +
                " به شماره شهربانی " + _plate +
                " بدینوسیله گواهی می نمایم کالای مشروحه فوق را صحیح و سالم و به منظور حمل به مقصد " + _destination +
                " به انضمام مبلغ " + totalPrepaid.ToString("N0") +
                " ریال اصالتا از طرف خود و وکالتا از طرف مالک خودرو تحویل گرفتم. بدینوسیله متعهد و مستلزم می گردم که با در نظر گرفتن شرایط عمومی حمل و نقل مفاد 377 الی 394 قانون تجارت و شرایط خصوصی مندرج در بارنامه های مربوطه ظرف مدت ........ ساعت / روز در مقصد تحویل نماینده این شرکت " + _agent +
                " دهم و مبلغ " + totalRemaining.ToString("N0") +
                " ریال به انضمام رسید تحویل دریافت نمایم." +
                Environment.NewLine + Environment.NewLine +
                "بدیهی ست چنانچه در مدت تعیین شده نتوانم کالا را در مقصد به شرحی که داده شده تحویل نمایم متعهد می گردم هرگونه خسارت وارد شده به صاحب کالا و شرکت وایو را طبق اعلام ایشان و ظرف مدتی که شرکت تعیین می نماید جبران نمایم. همچنین هرگونه خسارت اعم از کسری ، مفقودی ، آب دیدگی ، خسارت فیزیکی و غیره بر عهده اینجانب بوده و متعهد می گردم بدون هیچ اعتراضی و در مدت زمانی که شرکت اعلام می نماید خسارت وارده را جبران نموده و مبلغ اعلام شده را پرداخت نمایم.";
        }

        private static decimal ToDec(string s)
        {
            decimal.TryParse((s ?? "").Replace(",", ""), out decimal n);
            return n;
        }

        private static Grid MakeRow(string row, string number, string sender, string receiver,
            string dest, string cargo, string qty, string payable, string prepaid, string remaining)
        {
            var g = new Grid { Height = 22 };
            double[] w = { 32, 88, 1, 1, 72, 72, 48, 78, 72, 78 };
            for (int i = 0; i < 10; i++)
                g.ColumnDefinitions.Add(i == 2 || i == 3
                    ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    : new ColumnDefinition { Width = new GridLength(w[i]) });

            string[] cells = { row, number, sender, receiver, dest, cargo, qty, payable, prepaid, remaining };
            for (int i = 0; i < 10; i++)
                AddCell(g, i, cells[i]);
            return g;
        }

        private static void AddCell(Grid g, int col, string text)
        {
            var border = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            border.Child = new TextBlock
            {
                Text = text ?? "",
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(border, col);
            g.Children.Add(border);
        }

        private static string FirstProp(object item, params string[] names)
        {
            foreach (var n in names)
            {
                var v = GetProp(item, n);
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
            return "";
        }

        private static string GetProp(object item, string name)
        {
            return item.GetType().GetProperty(name)?.GetValue(item)?.ToString() ?? "";
        }

        private void InvoicePrintPage_Loaded(object sender, RoutedEventArgs e)
        {
            PrintPage();
        }

        private void PrintPage()
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
                return;

            printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
            try
            {
                printDialog.PrintTicket.PageMediaSize =
                    new PageMediaSize(PageMediaSizeName.ISOA4);
            }
            catch
            {
            }

            double printWidth = printDialog.PrintableAreaWidth;
            double printHeight = printDialog.PrintableAreaHeight;
            if (printWidth < 1 || printHeight < 1)
                return;

            var rows = new List<UIElement>(PrintItemsHost.Children.Count);
            foreach (UIElement child in PrintItemsHost.Children)
                rows.Add(child);

            string invoiceTitle = PrintInvoiceNumberTextBlock.Text;
            try
            {
                int rowsPerPage = MeasureRowsPerPage(rows);
                if (rows.Count > rowsPerPage)
                {
                    // جا برای «صفحه n از m» از همان اندازه‌گیری کم می‌شود تا عنوان، ردیف آخر را به بیرون هل ندهد.
                    PrintInvoiceNumberTextBlock.Text = invoiceTitle + "    صفحه 888 از 888";
                    rowsPerPage = MeasureRowsPerPage(rows);
                    PrintInvoiceNumberTextBlock.Text = invoiceTitle;
                }
                IList<InvoicePagePlanner.Slice> pages = InvoicePagePlanner.Plan(rows.Count, rowsPerPage);
                var bitmaps = new List<ImageSource>(pages.Count);

                for (int i = 0; i < pages.Count; i++)
                {
                    ShowSlice(rows, pages[i]);
                    PrintInvoiceNumberTextBlock.Text = pages.Count == 1
                        ? invoiceTitle
                        : invoiceTitle + "    صفحه " + (i + 1).ToString() + " از " + pages.Count.ToString();
                    bitmaps.Add(SnapshotPage(printWidth, printHeight));
                }

                var paginator = new InvoiceBitmapPaginator(
                    bitmaps, new Size(printWidth, printHeight));
                printDialog.PrintDocument(paginator, "صورتحساب");
            }
            finally
            {
                PrintInvoiceNumberTextBlock.Text = invoiceTitle;
                PrintItemsHost.Children.Clear();
                foreach (UIElement row in rows)
                    PrintItemsHost.Children.Add(row);
            }
        }

        private int MeasureRowsPerPage(IList<UIElement> rows)
        {
            if (rows.Count == 0)
                return 1;

            double rowHeight = RowSlotHeight(rows[0]);
            double emptySlot = MeasureHostHeight(null, 0);
            double fullHost = MeasureHostHeight(rows, rows.Count);
            int bySlot = InvoicePagePlanner.RowsInSlot(emptySlot, rowHeight);
            // جای ردیف‌ها در XAML ارتفاع ثابت دارد. ردیف اضافه DesiredSize را بزرگ نمی‌کند
            // و اندازه‌گیری قبلی همه‌شان را در یک برگ قبول می‌کرد؛ روی کاغذ فقط بخشی دیده می‌شد.
            bool clips = emptySlot >= rowHeight && fullHost <= emptySlot + 2.0;
            if (clips && bySlot > 0)
            {
                int capacity = Math.Min(bySlot, rows.Count);
                while (capacity > 1 && !LayoutFits(rows, 0, capacity))
                    capacity--;
                return capacity;
            }

            if (LayoutFits(rows, 0, rows.Count))
                return rows.Count;

            int best = 1;
            int lo = 1;
            int hi = rows.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (LayoutFits(rows, 0, mid))
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (!LayoutFits(rows, 0, best))
            {
                while (best > 1 && !LayoutFits(rows, 0, best))
                    best--;
            }
            else
            {
                while (best < rows.Count && LayoutFits(rows, 0, best + 1))
                    best++;
            }

            return Math.Max(1, best);
        }

        private double MeasureHostHeight(IList<UIElement> rows, int count)
        {
            PrintItemsHost.Children.Clear();
            if (rows != null)
            {
                for (int i = 0; i < count; i++)
                    PrintItemsHost.Children.Add(rows[i]);
            }

            ArrangeDesignPage();
            FrameworkElement host = PrintItemsHost as FrameworkElement;
            return host == null ? 0 : host.ActualHeight;
        }

        private static double RowSlotHeight(UIElement row)
        {
            FrameworkElement element = row as FrameworkElement;
            if (element != null && element.Height > 0 && !double.IsNaN(element.Height))
                return element.Height;
            return 22;
        }

        private void ArrangeDesignPage()
        {
            PrintableArea.InvalidateMeasure();
            PrintableArea.Measure(new Size(DesignWidth, DesignHeight));
            PrintableArea.Arrange(new Rect(0, 0, DesignWidth, DesignHeight));
            PrintableArea.UpdateLayout();
        }

        private bool LayoutFits(IList<UIElement> rows, int start, int count)
        {
            PrintItemsHost.Children.Clear();
            for (int i = 0; i < count; i++)
                PrintItemsHost.Children.Add(rows[start + i]);

            ArrangeDesignPage();

            double hostDesired = 0;
            double hostActual = 0;
            double hostBottom = 0;
            double legalBottom = 0;
            double lastRowBottom = 0;
            FrameworkElement host = PrintItemsHost as FrameworkElement;
            if (host != null)
            {
                hostDesired = host.DesiredSize.Height;
                hostActual = host.ActualHeight;
                try
                {
                    hostBottom = BottomOf(host);
                    legalBottom = BottomOf(PrintLegalTextBlock);
                    if (count > 0)
                        lastRowBottom = BottomOf(rows[start + count - 1] as FrameworkElement);
                }
                catch (InvalidOperationException)
                {
                    hostBottom = 0;
                    legalBottom = 0;
                    lastRowBottom = 0;
                }
            }

            return InvoicePagePlanner.ContentFits(
                PrintableArea.DesiredSize.Height,
                DesignHeight,
                hostDesired,
                hostActual,
                hostBottom,
                legalBottom,
                lastRowBottom);
        }

        private double BottomOf(FrameworkElement element)
        {
            if (element == null || element.Visibility != Visibility.Visible || element.ActualHeight <= 0)
                return 0;
            return element.TranslatePoint(new Point(0, element.ActualHeight), PrintableArea).Y;
        }

        private void ShowSlice(IList<UIElement> rows, InvoicePagePlanner.Slice slice)
        {
            PrintItemsHost.Children.Clear();
            for (int i = 0; i < slice.Count; i++)
                PrintItemsHost.Children.Add(rows[slice.Start + i]);
        }

        private ImageSource SnapshotPage(double printWidth, double printHeight)
        {
            ArrangeDesignPage();

            double scale = Math.Min(printWidth / DesignWidth, printHeight / DesignHeight) * 0.92;
            double x = (printWidth - DesignWidth * scale) / 2;
            double y = 16;

            // مقیاس منفی همان اصلاح راست‌به‌چپ چاپ تک‌صفحه‌ای است و داخل تصویر هر صفحه ذخیره می‌شود.
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, printWidth, printHeight));
                dc.PushTransform(new TranslateTransform(x + DesignWidth * scale, y));
                dc.PushTransform(new ScaleTransform(-scale, scale));
                var brush = new VisualBrush(PrintableArea)
                {
                    Stretch = Stretch.Fill,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };
                dc.DrawRectangle(brush, null, new Rect(0, 0, DesignWidth, DesignHeight));
                dc.Pop();
                dc.Pop();
            }

            const double dpi = 300;
            int pxW = Math.Max(1, (int)Math.Ceiling(printWidth * dpi / 96.0));
            int pxH = Math.Max(1, (int)Math.Ceiling(printHeight * dpi / 96.0));
            var bitmap = new RenderTargetBitmap(pxW, pxH, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static string N0(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";
            if (decimal.TryParse(s.Replace(",", ""), out decimal n))
                return n.ToString("#,##0");
            return s;
        }

        private sealed class InvoiceBitmapPaginator : DocumentPaginator
        {
            private readonly IList<ImageSource> _pages;
            private Size _pageSize;

            public InvoiceBitmapPaginator(IList<ImageSource> pages, Size pageSize)
            {
                _pages = pages;
                _pageSize = pageSize;
            }

            public override DocumentPage GetPage(int pageNumber)
            {
                if (pageNumber < 0 || pageNumber >= _pages.Count)
                    throw new ArgumentOutOfRangeException("pageNumber");

                var visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                    dc.DrawImage(_pages[pageNumber], new Rect(new Point(0, 0), _pageSize));

                return new DocumentPage(visual, _pageSize, new Rect(_pageSize), new Rect(_pageSize));
            }

            public override bool IsPageCountValid
            {
                get { return true; }
            }

            public override int PageCount
            {
                get { return _pages.Count; }
            }

            public override Size PageSize
            {
                get { return _pageSize; }
                set { _pageSize = value; }
            }

            public override IDocumentPaginatorSource Source
            {
                get { return null; }
            }
        }
    }

    internal static class InvoicePagePlanner
    {
        internal struct Slice
        {
            public int Start;
            public int Count;

            public Slice(int start, int count)
            {
                Start = start;
                Count = count;
            }
        }

        public static int RowsInSlot(double slotHeight, double rowHeight)
        {
            if (slotHeight < 1 || rowHeight < 1)
                return 0;
            return (int)Math.Floor((slotHeight + 0.01) / rowHeight);
        }

        public static bool ContentFits(
            double rootDesiredHeight,
            double pageHeight,
            double hostDesiredHeight,
            double hostActualHeight,
            double hostBottom,
            double legalBottom,
            double lastRowBottom = 0)
        {
            const double tolerance = 2.0;
            if (rootDesiredHeight > pageHeight + tolerance)
                return false;
            // ردیف ستاره‌ای ارتفاع ثابت دارد و ردیف‌های اضافه را می‌برد.
            if (hostActualHeight > 0 && hostDesiredHeight > hostActualHeight + tolerance)
                return false;
            if (hostActualHeight <= 0 && hostDesiredHeight > pageHeight + tolerance)
                return false;
            if (hostBottom > pageHeight + tolerance)
                return false;
            if (legalBottom > pageHeight + tolerance)
                return false;
            if (lastRowBottom > pageHeight + tolerance)
                return false;
            if (lastRowBottom > 0 && hostBottom > 0 && lastRowBottom > hostBottom + tolerance)
                return false;
            return true;
        }

        public static IList<Slice> Plan(int rowCount, int rowsPerPage)
        {
            if (rowsPerPage < 1)
                rowsPerPage = 1;

            var pages = new List<Slice>();
            if (rowCount <= 0)
            {
                pages.Add(new Slice(0, 0));
                return pages;
            }

            for (int start = 0; start < rowCount; start += rowsPerPage)
            {
                int count = Math.Min(rowsPerPage, rowCount - start);
                pages.Add(new Slice(start, count));
            }

            return pages;
        }
    }
}
