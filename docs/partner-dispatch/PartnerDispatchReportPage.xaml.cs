using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TestApp.Views.Pages
{
    public partial class PartnerDispatchReportPage : Page
    {
        private bool _savingStatus;
        private const string MobileCandidates = "Mobile,MobileNumber,Phone,PhoneNumber,CellPhone,Tel";

        public PartnerDispatchReportPage()
        {
            InitializeComponent();
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
                // پیشنهادها اختیاری‌اند.
            }

            TehranOperatorComboBox.Text = current;
        }

        private void LoadReport()
        {
            if (!TryGetPersianDate(FromDateTextBox, "از تاریخ", out DateTime? fromDate))
                return;
            if (!TryGetPersianDate(ToDateTextBox, "تا تاریخ", out DateTime? toDate))
                return;

            string settlementType = (SettlementTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "همه";
            if (settlementType == "همه")
                settlementType = "";

            var rows = new List<PartnerReportRow>();
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.connectionString))
                {
                    connection.Open();
                    string mobileColumn = FindContactColumn(connection, MobileCandidates);

                    string sql = @"
                        SELECT
                            Pd.Id,
                            W.WaybillNumber,
                            I.InvoiceDate,
                            W.ShipmentDate,
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
                            " + (mobileColumn == null ? "CAST(N'' AS nvarchar(50))" : "ISNULL(R." + mobileColumn + ", N'')") + @" AS ReceiverMobile,
                            Dest.CityName AS DestinationCity,
                            CT.CargoTypeName AS CargoType,
                            W.TotalQuantity,
                            pack.Packaging,
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
                        LEFT JOIN dbo.Cities Dest ON W.DestinationId = Dest.Id
                        LEFT JOIN dbo.CargoTypes CT ON W.CargoTypeId = CT.Id
                        OUTER APPLY (
                            SELECT STUFF((
                                SELECT N'، ' + line.Label
                                FROM (
                                    SELECT
                                        LTRIM(RTRIM(
                                            CASE
                                                WHEN ISNULL(I2.UnitName, N'') <> N'' THEN I2.UnitName
                                                ELSE ISNULL(I2.ItemName, N'')
                                            END
                                            + CASE
                                                WHEN I2.Quantity IS NULL THEN N''
                                                ELSE N' ' + CONVERT(nvarchar(20), I2.Quantity)
                                              END
                                        )) AS Label,
                                        I2.RowNumber
                                    FROM dbo.WaybillItems I2
                                    WHERE I2.WaybillId = W.Id
                                ) line
                                WHERE line.Label <> N''
                                ORDER BY line.RowNumber
                                FOR XML PATH(''), TYPE
                            ).value('.', 'nvarchar(max)'), 1, 2, N'') AS Packaging
                        ) pack
                        WHERE (@FromDate IS NULL OR W.CreatedAt >= @FromDate)
                          AND (@ToDateExclusive IS NULL OR W.CreatedAt < @ToDateExclusive)
                          AND (@Dest = N'' OR ISNULL(Dest.CityName, N'') LIKE @Dest)
                          AND (@Operator = N'' OR ISNULL(Pd.TehranOperatorName, N'') = @Operator)
                          AND (@SettlementType = N'' OR Pd.SettlementType = @SettlementType)
                          AND (
                                @Search = N''
                                OR ISNULL(W.WaybillNumber, N'') LIKE @Search
                                OR ISNULL(Pd.PartnerReceiptNumber, N'') LIKE @Search
                                OR ISNULL(Pd.FreightCompanyName, N'') LIKE @Search
                                OR ISNULL(Pd.UnloadName, N'') LIKE @Search
                                OR ISNULL(Pd.TehranOperatorName, N'') LIKE @Search
                                OR ISNULL(Dest.CityName, N'') LIKE @Search
                                OR (
                                    CASE
                                        WHEN S.CompanyName IS NOT NULL AND S.CompanyName <> '' AND S.CompanyName <> N'ندارد'
                                        THEN S.CompanyName
                                        ELSE LTRIM(RTRIM(ISNULL(S.FirstName, '') + ' ' + ISNULL(S.LastName, '')))
                                    END
                                ) LIKE @Search
                                OR (
                                    CASE
                                        WHEN R.CompanyName IS NOT NULL AND R.CompanyName <> '' AND R.CompanyName <> N'ندارد'
                                        THEN R.CompanyName
                                        ELSE LTRIM(RTRIM(ISNULL(R.FirstName, '') + ' ' + ISNULL(R.LastName, '')))
                                    END
                                ) LIKE @Search
                          )
                        ORDER BY Pd.TehranOperatorName, Pd.Id DESC;";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.CommandTimeout = 120;
                        string search = (SearchTextBox.Text ?? "").Trim();
                        string dest = (DestinationTextBox.Text ?? "").Trim();
                        string op = (TehranOperatorComboBox.Text ?? "").Trim();

                        command.Parameters.AddWithValue("@FromDate", (object)fromDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ToDateExclusive", toDate.HasValue ? toDate.Value.Date.AddDays(1) : (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Dest", dest.Length == 0 ? "" : "%" + dest + "%");
                        command.Parameters.AddWithValue("@Operator", op);
                        command.Parameters.AddWithValue("@SettlementType", settlementType);
                        command.Parameters.AddWithValue("@Search", search.Length == 0 ? "" : "%" + search + "%");

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string settlementStatus = reader["SettlementStatus"]?.ToString();
                                if (settlementStatus != "باز" && settlementStatus != "بسته")
                                    settlementStatus = "باز";
                                string deliveryStatus = reader["DeliveryStatus"]?.ToString();
                                if (deliveryStatus != "تحویل نشده" && deliveryStatus != "تحویل شده")
                                    deliveryStatus = "تحویل نشده";

                                rows.Add(new PartnerReportRow
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
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
                                    SettlementStatus = settlementStatus,
                                    PersistedSettlementStatus = settlementStatus,
                                    DeliveryStatus = deliveryStatus,
                                    PersistedDeliveryStatus = deliveryStatus
                                });
                            }
                        }
                    }
                }

                _savingStatus = true;
                ReportGrid.ItemsSource = null;
                ReportGrid.ItemsSource = rows;
                _savingStatus = false;
                UpdateTotals(rows);
            }
            catch (Exception ex)
            {
                _savingStatus = false;
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

        private void StatusCombo_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox combo)
                return;
            if (combo.DataContext is not PartnerReportRow row)
                return;

            bool settlement = (combo.Tag as string) == "Settlement";
            _savingStatus = true;
            combo.Items.Clear();
            if (settlement)
            {
                combo.Items.Add("باز");
                combo.Items.Add("بسته");
                combo.SelectedItem = row.SettlementStatus;
            }
            else
            {
                combo.Items.Add("تحویل نشده");
                combo.Items.Add("تحویل شده");
                combo.SelectedItem = row.DeliveryStatus;
            }
            _savingStatus = false;
        }

        private void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_savingStatus)
                return;
            if (sender is not ComboBox combo)
                return;
            if (combo.DataContext is not PartnerReportRow row)
                return;

            string selected = combo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
                return;

            bool settlement = (combo.Tag as string) == "Settlement";
            string persisted = settlement ? row.PersistedSettlementStatus : row.PersistedDeliveryStatus;
            if (selected == persisted)
                return;

            if (!hamlex.Services.CurrentUserSession.HasPermission("PartnerDispatch.Edit"))
            {
                MessageBox.Show("شما دسترسی ویرایش ارسال با همکار را ندارید.", "عدم دسترسی", MessageBoxButton.OK, MessageBoxImage.Warning);
                _savingStatus = true;
                combo.SelectedItem = persisted;
                _savingStatus = false;
                return;
            }

            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.connectionString))
                {
                    connection.Open();
                    string sql = settlement
                        ? "UPDATE dbo.PartnerDispatches SET SettlementStatus = @Value WHERE Id = @Id"
                        : "UPDATE dbo.PartnerDispatches SET DeliveryStatus = @Value WHERE Id = @Id";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@Value", selected);
                        command.Parameters.AddWithValue("@Id", row.Id);
                        command.ExecuteNonQuery();
                    }
                }

                if (settlement)
                {
                    row.SettlementStatus = selected;
                    row.PersistedSettlementStatus = selected;
                }
                else
                {
                    row.DeliveryStatus = selected;
                    row.PersistedDeliveryStatus = selected;
                }
            }
            catch (Exception ex)
            {
                _savingStatus = true;
                combo.SelectedItem = persisted;
                _savingStatus = false;
                MessageBox.Show("خطا در ثبت وضعیت: " + ex.Message);
            }
        }

        private static string ReadReceiptDate(SqlDataReader reader)
        {
            string invoiceDate = reader["InvoiceDate"]?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(invoiceDate))
                return invoiceDate;

            if (reader["ShipmentDate"] == DBNull.Value || reader["ShipmentDate"] == null)
                return "";

            if (reader["ShipmentDate"] is DateTime shipmentDate)
            {
                var pc = new PersianCalendar();
                return $"{pc.GetYear(shipmentDate):0000}/{pc.GetMonth(shipmentDate):00}/{pc.GetDayOfMonth(shipmentDate):00}";
            }

            return reader["ShipmentDate"].ToString();
        }

        private static string FindContactColumn(SqlConnection connection, string candidates)
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
                    string parameter = "@c" + i;
                    inList.Add(parameter);
                    command.Parameters.AddWithValue(parameter, names[i]);
                }

                command.CommandText = @"
                    SELECT TOP 1 c.name
                    FROM sys.columns c
                    WHERE c.object_id = OBJECT_ID(N'dbo.Contacts')
                      AND c.name IN (" + string.Join(", ", inList) + @")
                    ORDER BY CASE c.name
                        WHEN N'Mobile' THEN 0
                        WHEN N'MobileNumber' THEN 1
                        WHEN N'Phone' THEN 2
                        WHEN N'PhoneNumber' THEN 3
                        WHEN N'CellPhone' THEN 4
                        ELSE 5
                    END;";

                object found = command.ExecuteScalar();
                return found == null || found == DBNull.Value ? null : found.ToString();
            }
        }

        private static bool TryGetPersianDate(TextBox box, string title, out DateTime? date)
        {
            date = null;
            string text = (box.Text ?? "").Trim();
            if (text.Length == 0)
                return true;

            var parts = text.Split('/');
            if (parts.Length != 3
                || !int.TryParse(parts[0], out int year)
                || !int.TryParse(parts[1], out int month)
                || !int.TryParse(parts[2], out int day))
            {
                MessageBox.Show("تاریخ «" + title + "» معتبر نیست. شکل درست: 1404/01/01");
                return false;
            }

            try
            {
                var pc = new PersianCalendar();
                date = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch
            {
                MessageBox.Show("تاریخ «" + title + "» معتبر نیست.");
                return false;
            }
        }

        public sealed class PartnerReportRow
        {
            public int Id { get; set; }
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
            public string PersistedSettlementStatus { get; set; }
            public string DeliveryStatus { get; set; }
            public string PersistedDeliveryStatus { get; set; }
        }
    }
}
