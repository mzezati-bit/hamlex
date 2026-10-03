using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace hamlex.Views.Pages
{
    public partial class PartnerDispatchReportPage : Page
    {
        private const string MobileCandidates = "Mobile,MobileNumber,Phone,PhoneNumber,CellPhone,Tel";
        private const string PackageCandidates = "PackageTypeName,PackageType,PackagingType,Packaging,UnitName,ItemName";

        public PartnerDispatchReportPage()
        {
            InitializeComponent();
            PersianDateMask.Attach(FromDateTextBox);
            PersianDateMask.Attach(ToDateTextBox);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadOperatorSuggestions();
            LoadReport();
        }

        private void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            LoadReport();
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            PersianDateMask.Set(FromDateTextBox, "");
            PersianDateMask.Set(ToDateTextBox, "");
            DestinationTextBox.Text = "";
            TehranOperatorComboBox.Text = "";
            DateKindComboBox.SelectedIndex = 0;
            SettlementTypeComboBox.SelectedIndex = 0;
            SettlementStatusComboBox.SelectedIndex = 0;
            DeliveryStatusComboBox.SelectedIndex = 0;
            SearchTextBox.Text = "";
            LoadReport();
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            if (!hamlex.Services.CurrentUserSession.HasPermission("PartnerDispatch.Create"))
            {
                MessageBox.Show(
                    "شما دسترسی ثبت ارسال با همکار را ندارید.",
                    "عدم دسترسی",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (NavigationService == null)
                return;

            NavigationService.Navigate(new PartnerDispatchPage());
        }

        private void ReportGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(ReportGrid.SelectedItem is PartnerReportRow row))
                return;
            if (string.IsNullOrWhiteSpace(row.WaybillNumber))
                return;
            if (NavigationService == null)
                return;

            NavigationService.Navigate(new PartnerDispatchPage(row.WaybillNumber));
        }

        private void LoadOperatorSuggestions()
        {
            string current = TehranOperatorComboBox.Text ?? "";
            TehranOperatorComboBox.Items.Clear();
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        SELECT DISTINCT TehranOperatorName
                        FROM dbo.PartnerDispatches
                        WHERE TehranOperatorName IS NOT NULL AND LTRIM(RTRIM(TehranOperatorName)) <> N''
                        ORDER BY TehranOperatorName;", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader[0]?.ToString();
                            if (!string.IsNullOrWhiteSpace(name))
                                TehranOperatorComboBox.Items.Add(name.Trim());
                        }
                    }
                }
            }
            catch
            {
            }

            TehranOperatorComboBox.Text = current;
        }

        private void LoadReport()
        {
            if (!PersianDateMask.TryRead(FromDateTextBox.Text, out DateTime? fromDate))
            {
                MessageBox.Show("تاریخ «از تاریخ» معتبر نیست. شکل درست: 1404/01/01");
                FromDateTextBox.Focus();
                return;
            }

            if (!PersianDateMask.TryRead(ToDateTextBox.Text, out DateTime? toDate))
            {
                MessageBox.Show("تاریخ «تا تاریخ» معتبر نیست. شکل درست: 1404/01/01");
                ToDateTextBox.Focus();
                return;
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                MessageBox.Show("تاریخ شروع نباید بعد از تاریخ پایان باشد.");
                return;
            }

            string settlementType = SelectedFilter(SettlementTypeComboBox);
            string settlementStatus = SelectedFilter(SettlementStatusComboBox);
            string deliveryStatus = SelectedFilter(DeliveryStatusComboBox);
            bool filterPartnerReceiptDate = DateKindComboBox.SelectedIndex == 1;

            var rows = new List<PartnerReportRow>();
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.connectionString))
                {
                    connection.Open();
                    string mobileColumn = FindListedColumn(connection, "dbo.Contacts", MobileCandidates);
                    string packageColumn = FindListedColumn(connection, "dbo.WaybillItems", PackageCandidates);
                    string mobileSql = mobileColumn == null
                        ? "CAST(N'' AS nvarchar(50))"
                        : "ISNULL(R.[" + mobileColumn + "], N'')";
                    string packageSql = packageColumn == null
                        ? "CAST(N'' AS nvarchar(200))"
                        : @"(SELECT STUFF((
                                SELECT N'، ' + line.Label
                                FROM (
                                    SELECT DISTINCT LTRIM(RTRIM(ISNULL(CONVERT(nvarchar(200), [" + packageColumn + @"]), N''))) AS Label
                                    FROM dbo.WaybillItems
                                    WHERE WaybillId = W.Id
                                ) line
                                WHERE line.Label <> N''
                                FOR XML PATH(''), TYPE
                            ).value('.', 'nvarchar(max)'), 1, 2, N''))";

                    string sql = @"
                        SELECT
                            Pd.Id,
                            W.WaybillNumber,
                            I.InvoiceDate,
                            W.WaybillDate,
                            CASE
                                WHEN S.CompanyName IS NOT NULL AND S.CompanyName <> '' AND S.CompanyName <> N'ندارد'
                                THEN S.CompanyName
                                ELSE LTRIM(RTRIM(ISNULL(S.FirstName, '') + ' ' + ISNULL(S.LastName, '')))
                            END AS SenderName,
                            CASE
                                WHEN R.CompanyName IS NOT NULL AND R.CompanyName <> '' AND R.CompanyName <> N'ندارد'
                                THEN R.CompanyName
                                ELSE LTRIM(RTRIM(ISNULL(R.FirstName, '') + ' ' + ISNULL(R.LastName, '')))
                            END AS ReceiverName,
                            " + mobileSql + @" AS ReceiverMobile,
                            C.CityName AS DestinationCity,
                            CT.CargoTypeName AS CargoType,
                            W.TotalQuantity,
                            " + packageSql + @" AS Packaging,
                            W.Payable,
                            Pd.FreightCompanyName,
                            Pd.PartnerReceiptNumber,
                            Pd.PartnerReceiptDate,
                            Pd.TehranOperatorName,
                            Pd.UnloadName,
                            Pd.UnloadMobile,
                            Pd.PartnerFreight,
                            Pd.SettlementType,
                            Pd.SettlementStatus,
                            Pd.DeliveryStatus
                        FROM dbo.PartnerDispatches Pd
                        INNER JOIN dbo.Waybills W ON W.Id = Pd.WaybillId AND ISNULL(W.IsDeleted, 0) = 0
                        LEFT JOIN dbo.Invoices I ON I.Id = W.InvoiceId AND ISNULL(I.IsDeleted, 0) = 0
                        LEFT JOIN dbo.Contacts S ON W.SenderId = S.Id
                        LEFT JOIN dbo.Contacts R ON W.ReceiverId = R.Id
                        LEFT JOIN dbo.Cities C ON W.DestinationId = C.Id
                        LEFT JOIN dbo.CargoTypes CT ON W.CargoTypeId = CT.Id
                        WHERE (@Dest = N'' OR ISNULL(C.CityName, N'') LIKE @Dest)
                          AND (@Operator = N'' OR ISNULL(Pd.TehranOperatorName, N'') = @Operator)
                          AND (@SettlementType = N'' OR Pd.SettlementType = @SettlementType)
                          AND (@SettlementStatus = N'' OR ISNULL(NULLIF(LTRIM(RTRIM(Pd.SettlementStatus)), N''), N'باز') = @SettlementStatus)
                          AND (@DeliveryStatus = N'' OR ISNULL(NULLIF(LTRIM(RTRIM(Pd.DeliveryStatus)), N''), N'تحویل نشده') = @DeliveryStatus)
                          AND (
                                @Search = N''
                                OR ISNULL(W.WaybillNumber, N'') LIKE @Search
                                OR ISNULL(Pd.PartnerReceiptNumber, N'') LIKE @Search
                                OR ISNULL(Pd.FreightCompanyName, N'') LIKE @Search
                                OR ISNULL(Pd.UnloadName, N'') LIKE @Search
                                OR ISNULL(Pd.TehranOperatorName, N'') LIKE @Search
                                OR ISNULL(C.CityName, N'') LIKE @Search
                          )
                        ORDER BY Pd.Id DESC;";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.CommandTimeout = 120;
                        string search = (SearchTextBox.Text ?? "").Trim();
                        string dest = (DestinationTextBox.Text ?? "").Trim();
                        string op = (TehranOperatorComboBox.Text ?? "").Trim();

                        command.Parameters.AddWithValue("@Dest", dest.Length == 0 ? "" : "%" + dest + "%");
                        command.Parameters.AddWithValue("@Operator", op);
                        command.Parameters.AddWithValue("@SettlementType", settlementType);
                        command.Parameters.AddWithValue("@SettlementStatus", settlementStatus);
                        command.Parameters.AddWithValue("@DeliveryStatus", deliveryStatus);
                        command.Parameters.AddWithValue("@Search", search.Length == 0 ? "" : "%" + search + "%");

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string rowSettlementStatus = reader["SettlementStatus"]?.ToString();
                                if (rowSettlementStatus != "باز" && rowSettlementStatus != "بسته")
                                    rowSettlementStatus = "باز";
                                string rowDeliveryStatus = reader["DeliveryStatus"]?.ToString();
                                if (rowDeliveryStatus != "تحویل نشده" && rowDeliveryStatus != "تحویل شده")
                                    rowDeliveryStatus = "تحویل نشده";

                                rows.Add(new PartnerReportRow
                                {
                                    WaybillNumber = reader["WaybillNumber"]?.ToString() ?? "",
                                    ReceiptDate = ReadReceiptDate(reader),
                                    SenderName = reader["SenderName"]?.ToString() ?? "",
                                    ReceiverName = reader["ReceiverName"]?.ToString() ?? "",
                                    ReceiverMobile = reader["ReceiverMobile"]?.ToString() ?? "",
                                    DestinationCity = reader["DestinationCity"]?.ToString() ?? "",
                                    CargoType = reader["CargoType"]?.ToString() ?? "",
                                    Quantity = reader["TotalQuantity"]?.ToString() ?? "",
                                    Packaging = reader["Packaging"]?.ToString() ?? "",
                                    Payable = reader["Payable"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Payable"]),
                                    FreightCompanyName = reader["FreightCompanyName"]?.ToString() ?? "",
                                    PartnerReceiptNumber = reader["PartnerReceiptNumber"]?.ToString() ?? "",
                                    PartnerReceiptDate = reader["PartnerReceiptDate"]?.ToString() ?? "",
                                    TehranOperatorName = reader["TehranOperatorName"]?.ToString() ?? "",
                                    UnloadName = reader["UnloadName"]?.ToString() ?? "",
                                    UnloadMobile = reader["UnloadMobile"]?.ToString() ?? "",
                                    PartnerFreight = reader["PartnerFreight"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["PartnerFreight"]),
                                    SettlementType = reader["SettlementType"]?.ToString() ?? "",
                                    SettlementStatus = rowSettlementStatus,
                                    DeliveryStatus = rowDeliveryStatus
                                });
                            }
                        }
                    }
                }

                rows = FilterByChosenDate(rows, filterPartnerReceiptDate, fromDate, toDate);
                ReportGrid.ItemsSource = rows;
                UpdateTotals(rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطا در بارگذاری گزارش ارسال با همکار\n" + ex.Message);
            }
        }

        private void UpdateTotals(IList<PartnerReportRow> rows)
        {
            decimal payable = 0;
            decimal partner = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                payable += rows[i].Payable;
                partner += rows[i].PartnerFreight;
            }

            PayableSumTextBox.Text = payable.ToString("N0");
            PartnerSumTextBox.Text = partner.ToString("N0");
            DifferenceTextBox.Text = (payable - partner).ToString("N0");
        }

        private static string ReadReceiptDate(SqlDataReader reader)
        {
            string invoiceDate = reader["InvoiceDate"]?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(invoiceDate))
                return invoiceDate;

            if (reader["WaybillDate"] == DBNull.Value || reader["WaybillDate"] == null)
                return "";

            if (reader["WaybillDate"] is DateTime shipmentDate)
            {
                var pc = new PersianCalendar();
                return $"{pc.GetYear(shipmentDate):0000}/{pc.GetMonth(shipmentDate):00}/{pc.GetDayOfMonth(shipmentDate):00}";
            }

            return reader["WaybillDate"].ToString();
        }

        private static string FindListedColumn(SqlConnection connection, string objectName, string candidates)
        {
            var names = new List<string>();
            foreach (string part in candidates.Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0)
                    names.Add(name);
            }

            if (names.Count == 0)
                return null;

            var inList = new List<string>();
            using (var command = new SqlCommand())
            {
                command.Connection = connection;
                for (int i = 0; i < names.Count; i++)
                {
                    string parameter = "@n" + i;
                    inList.Add(parameter);
                    command.Parameters.AddWithValue(parameter, names[i]);
                }

                command.CommandText = @"
                    SELECT TOP 1 c.name
                    FROM sys.columns c
                    WHERE c.object_id = OBJECT_ID(@objectName)
                      AND c.name IN (" + string.Join(", ", inList) + @")
                    ORDER BY c.column_id;";
                command.Parameters.AddWithValue("@objectName", objectName);
                object found = command.ExecuteScalar();
                if (found == null || found == DBNull.Value)
                    return null;

                string column = found.ToString();
                return names.Contains(column) ? column : null;
            }
        }

        private static string SelectedFilter(ComboBox box)
        {
            string text = (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            return text == "همه" ? "" : text;
        }

        private static List<PartnerReportRow> FilterByChosenDate(
            List<PartnerReportRow> rows,
            bool partnerReceiptDate,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!fromDate.HasValue && !toDate.HasValue)
                return rows;

            var filtered = new List<PartnerReportRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                string value = partnerReceiptDate ? rows[i].PartnerReceiptDate : rows[i].ReceiptDate;
                if (!PersianDateMask.TryRead(PersianDateMask.ToMask(value), out DateTime? rowDate) || !rowDate.HasValue)
                    continue;
                if (fromDate.HasValue && rowDate.Value.Date < fromDate.Value.Date)
                    continue;
                if (toDate.HasValue && rowDate.Value.Date > toDate.Value.Date)
                    continue;
                filtered.Add(rows[i]);
            }

            return filtered;
        }

        public sealed class PartnerReportRow
        {
            public string WaybillNumber { get; set; }
            public string ReceiptDate { get; set; }
            public string SenderName { get; set; }
            public string ReceiverName { get; set; }
            public string ReceiverMobile { get; set; }
            public string DestinationCity { get; set; }
            public string CargoType { get; set; }
            public string Quantity { get; set; }
            public string Packaging { get; set; }
            public decimal Payable { get; set; }
            public string PayableText => Payable.ToString("N0");
            public string FreightCompanyName { get; set; }
            public string PartnerReceiptNumber { get; set; }
            public string PartnerReceiptDate { get; set; }
            public string TehranOperatorName { get; set; }
            public string UnloadName { get; set; }
            public string UnloadMobile { get; set; }
            public decimal PartnerFreight { get; set; }
            public string PartnerFreightText => PartnerFreight.ToString("N0");
            public string SettlementType { get; set; }
            public string SettlementStatus { get; set; }
            public string DeliveryStatus { get; set; }
        }
    }
}
