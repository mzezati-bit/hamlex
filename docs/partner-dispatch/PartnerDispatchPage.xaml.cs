using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TestApp.Views.Pages
{
    public partial class PartnerDispatchPage : Page
    {
        private int _waybillId;
        private int _dispatchId;
        private string _loadedWaybillNumber = "";
        private bool _loadingWaybill;

        private const string MobileCandidates = "Mobile,MobileNumber,Phone,PhoneNumber,CellPhone,Tel";

        public PartnerDispatchPage()
        {
            InitializeComponent();
            InitializeHeader();
            LoadNameSuggestions();
        }

        private void InitializeHeader()
        {
            CreatedDateTextBox.Text = GetPersianDate();
            CreatedTimeTextBox.Text = DateTime.Now.ToString("HH:mm");
        }

        private static string GetPersianDate()
        {
            var persianCalendar = new PersianCalendar();
            var now = DateTime.Now;
            int year = persianCalendar.GetYear(now);
            int month = persianCalendar.GetMonth(now);
            int day = persianCalendar.GetDayOfMonth(now);
            return $"{year:0000}/{month:00}/{day:00}";
        }

        private void WaybillNumberTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            string waybillNo = WaybillNumberTextBox.Text.Trim();
            if (string.IsNullOrEmpty(waybillNo))
                return;

            LoadWaybillInfo(waybillNo);
        }

        private void WaybillNumberTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string waybillNumber = WaybillNumberTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(waybillNumber))
                LoadWaybillInfo(waybillNumber);
        }

        private void LoadWaybillInfo(string waybillNumber)
        {
            if (_loadingWaybill)
                return;

            _loadingWaybill = true;
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.connectionString))
                {
                    connection.Open();
                    string mobileColumn = FindContactColumn(connection, MobileCandidates);

                    string query = @"
                    SELECT TOP 1
                        W.Id,
                        W.WaybillNumber,
                        I.InvoiceDate,
                        W.ShipmentDate,
                        W.TotalQuantity,
                        W.Payable,
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
                        C.CityName AS DestinationCity,
                        CT.CargoTypeName AS CargoType,
                        pack.Packaging,
                        Pd.Id AS DispatchId,
                        Pd.FreightCompanyName,
                        Pd.PartnerReceiptNumber,
                        Pd.PartnerReceiptDate,
                        Pd.TehranOperatorName,
                        Pd.UnloadName,
                        Pd.UnloadMobile,
                        Pd.PartnerFreight,
                        Pd.SettlementType,
                        Pd.SettlementStatus,
                        Pd.DeliveryStatus,
                        Pd.CreatedAt AS DispatchCreatedAt
                    FROM dbo.Waybills W
                    LEFT JOIN dbo.Invoices I ON W.InvoiceId = I.Id AND ISNULL(I.IsDeleted, 0) = 0
                    LEFT JOIN dbo.Contacts S ON W.SenderId = S.Id
                    LEFT JOIN dbo.Contacts R ON W.ReceiverId = R.Id
                    LEFT JOIN dbo.Cities C ON W.DestinationId = C.Id
                    LEFT JOIN dbo.CargoTypes CT ON W.CargoTypeId = CT.Id
                    LEFT JOIN dbo.PartnerDispatches Pd ON Pd.WaybillId = W.Id
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
                    WHERE W.WaybillNumber = @WaybillNumber
                      AND ISNULL(W.IsDeleted, 0) = 0;";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@WaybillNumber", waybillNumber);
                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                MessageBox.Show(
                                    "بارنامه یافت نشد.",
                                    "جستجو",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                                _dispatchId = 0;
                                _loadedWaybillNumber = "";
                                ClearWaybillFields();
                                ClearPartnerFields();
                                return;
                            }

                            bool hadLoadedWaybill = !string.IsNullOrEmpty(_loadedWaybillNumber);
                            bool sameWaybill = string.Equals(_loadedWaybillNumber, waybillNumber, StringComparison.OrdinalIgnoreCase);
                            _waybillId = Convert.ToInt32(reader["Id"]);
                            _loadedWaybillNumber = waybillNumber;

                            SenderTextBox.Text = reader["SenderName"]?.ToString()?.Trim();
                            ReceiverTextBox.Text = reader["ReceiverName"]?.ToString()?.Trim();
                            ReceiverMobileTextBox.Text = reader["ReceiverMobile"]?.ToString()?.Trim();
                            DestinationCityTextBox.Text = reader["DestinationCity"]?.ToString();
                            ReceiptDateTextBox.Text = ReadReceiptDate(reader);
                            CargoTypeTextBox.Text = reader["CargoType"]?.ToString();
                            QuantityTextBox.Text = reader["TotalQuantity"]?.ToString();
                            PackagingTextBox.Text = reader["Packaging"]?.ToString();
                            PayableTextBox.Text = reader["Payable"] == DBNull.Value
                                ? ""
                                : Convert.ToDecimal(reader["Payable"]).ToString("N0");

                            int dispatchId = reader["DispatchId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DispatchId"]);
                            if (dispatchId > 0 && (!sameWaybill || _dispatchId != dispatchId))
                            {
                                _dispatchId = dispatchId;
                                FreightCompanyComboBox.Text = reader["FreightCompanyName"]?.ToString() ?? "";
                                PartnerReceiptNumberTextBox.Text = reader["PartnerReceiptNumber"]?.ToString() ?? "";
                                PartnerReceiptDateTextBox.Text = reader["PartnerReceiptDate"]?.ToString() ?? "";
                                TehranOperatorComboBox.Text = reader["TehranOperatorName"]?.ToString() ?? "";
                                UnloadNameComboBox.Text = reader["UnloadName"]?.ToString() ?? "";
                                UnloadMobileTextBox.Text = reader["UnloadMobile"]?.ToString() ?? "";
                                PartnerFreightTextBox.Text = reader["PartnerFreight"] == DBNull.Value
                                    ? ""
                                    : Convert.ToDecimal(reader["PartnerFreight"]).ToString("N0");
                                SettlementTypeComboBox.Text = reader["SettlementType"]?.ToString() ?? "";
                                SettlementStatusComboBox.Text = string.IsNullOrWhiteSpace(reader["SettlementStatus"]?.ToString())
                                    ? "باز"
                                    : reader["SettlementStatus"].ToString();
                                DeliveryStatusComboBox.Text = string.IsNullOrWhiteSpace(reader["DeliveryStatus"]?.ToString())
                                    ? "تحویل نشده"
                                    : reader["DeliveryStatus"].ToString();

                                string createdAt = reader["DispatchCreatedAt"]?.ToString() ?? "";
                                var parts = createdAt.Split(' ');
                                if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                                    CreatedDateTextBox.Text = parts[0];
                                if (parts.Length >= 2)
                                    CreatedTimeTextBox.Text = parts[1];
                            }
                            else if (dispatchId == 0 && hadLoadedWaybill && !sameWaybill)
                            {
                                _dispatchId = 0;
                                ClearPartnerFields();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطا در واکشی اطلاعات: " + ex.Message);
            }
            finally
            {
                _loadingWaybill = false;
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

        private void LoadNameSuggestions()
        {
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.connectionString))
                {
                    connection.Open();
                    FillSuggestions(connection, FreightCompanyComboBox, "FreightCompanyName");
                    FillSuggestions(connection, TehranOperatorComboBox, "TehranOperatorName");
                    FillSuggestions(connection, UnloadNameComboBox, "UnloadName");
                }
            }
            catch
            {
                // جدول هنوز ساخته نشده؛ پیشنهاد نام خالی می‌ماند.
            }
        }

        private static void FillSuggestions(SqlConnection connection, ComboBox combo, string column)
        {
            if (column != "FreightCompanyName" && column != "TehranOperatorName" && column != "UnloadName")
                return;

            string current = combo.Text ?? "";
            combo.Items.Clear();

            using (var command = new SqlCommand(
                "SELECT DISTINCT " + column + @"
                 FROM dbo.PartnerDispatches
                 WHERE " + column + @" IS NOT NULL AND LTRIM(RTRIM(" + column + @")) <> N''
                 ORDER BY " + column + ";", connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string value = reader[0]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                            combo.Items.Add(value.Trim());
                    }
                }
            }

            combo.Text = current;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveDispatch();
        }

        private void SaveDispatch()
        {
            bool isEdit = _dispatchId > 0;
            string permission = isEdit ? "PartnerDispatch.Edit" : "PartnerDispatch.Create";
            if (!hamlex.Services.CurrentUserSession.HasPermission(permission))
            {
                MessageBox.Show(
                    isEdit ? "شما دسترسی ویرایش ارسال با همکار را ندارید." : "شما دسترسی ثبت ارسال با همکار را ندارید.",
                    "عدم دسترسی",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string waybillNumber = WaybillNumberTextBox.Text.Trim();
            if (string.IsNullOrEmpty(waybillNumber) || _waybillId <= 0)
            {
                MessageBox.Show("لطفاً ابتدا شماره بارنامه را وارد کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string settlementType = SettlementTypeComboBox.Text?.Trim() ?? "";
            if (settlementType != "تسویه در محل" && settlementType != "تسویه در شرکت" && settlementType != "پسکرایه")
            {
                MessageBox.Show(
                    "نحوه تسویه را انتخاب کنید: تسویه در محل، تسویه در شرکت، یا پسکرایه.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string settlementStatus = SettlementStatusComboBox.Text?.Trim();
            if (settlementStatus != "باز" && settlementStatus != "بسته")
                settlementStatus = "باز";

            string deliveryStatus = DeliveryStatusComboBox.Text?.Trim();
            if (deliveryStatus != "تحویل نشده" && deliveryStatus != "تحویل شده")
                deliveryStatus = "تحویل نشده";

            decimal partnerFreight = ParseMoney(PartnerFreightTextBox.Text);
            string createdAt = CreatedDateTextBox.Text.Trim() + " " + CreatedTimeTextBox.Text.Trim();

            using (var connection = new SqlConnection(DatabaseHelper.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (var command = new SqlCommand())
                        {
                            command.Connection = connection;
                            command.Transaction = transaction;

                            if (isEdit)
                            {
                                command.CommandText = @"
                                    UPDATE dbo.PartnerDispatches SET
                                        WaybillId = @WaybillId,
                                        FreightCompanyName = @FreightCompanyName,
                                        PartnerReceiptNumber = @PartnerReceiptNumber,
                                        PartnerReceiptDate = @PartnerReceiptDate,
                                        TehranOperatorName = @TehranOperatorName,
                                        UnloadName = @UnloadName,
                                        UnloadMobile = @UnloadMobile,
                                        PartnerFreight = @PartnerFreight,
                                        SettlementType = @SettlementType,
                                        SettlementStatus = @SettlementStatus,
                                        DeliveryStatus = @DeliveryStatus
                                    WHERE Id = @Id;";
                                command.Parameters.AddWithValue("@Id", _dispatchId);
                            }
                            else
                            {
                                command.CommandText = @"
                                    INSERT INTO dbo.PartnerDispatches (
                                        WaybillId, FreightCompanyName, PartnerReceiptNumber, PartnerReceiptDate,
                                        TehranOperatorName, UnloadName, UnloadMobile, PartnerFreight,
                                        SettlementType, SettlementStatus, DeliveryStatus, CreatedAt
                                    ) VALUES (
                                        @WaybillId, @FreightCompanyName, @PartnerReceiptNumber, @PartnerReceiptDate,
                                        @TehranOperatorName, @UnloadName, @UnloadMobile, @PartnerFreight,
                                        @SettlementType, @SettlementStatus, @DeliveryStatus, @CreatedAt
                                    );
                                    SELECT CAST(SCOPE_IDENTITY() AS int);";
                            }

                            command.Parameters.AddWithValue("@WaybillId", _waybillId);
                            command.Parameters.AddWithValue("@FreightCompanyName", NullIfEmpty(FreightCompanyComboBox.Text));
                            command.Parameters.AddWithValue("@PartnerReceiptNumber", NullIfEmpty(PartnerReceiptNumberTextBox.Text));
                            command.Parameters.AddWithValue("@PartnerReceiptDate", NullIfEmpty(PartnerReceiptDateTextBox.Text));
                            command.Parameters.AddWithValue("@TehranOperatorName", NullIfEmpty(TehranOperatorComboBox.Text));
                            command.Parameters.AddWithValue("@UnloadName", NullIfEmpty(UnloadNameComboBox.Text));
                            command.Parameters.AddWithValue("@UnloadMobile", NullIfEmpty(UnloadMobileTextBox.Text));
                            command.Parameters.AddWithValue("@PartnerFreight", partnerFreight);
                            command.Parameters.AddWithValue("@SettlementType", settlementType);
                            command.Parameters.AddWithValue("@SettlementStatus", settlementStatus);
                            command.Parameters.AddWithValue("@DeliveryStatus", deliveryStatus);
                            command.Parameters.AddWithValue("@CreatedAt", createdAt);

                            if (isEdit)
                            {
                                command.ExecuteNonQuery();
                            }
                            else
                            {
                                object id = command.ExecuteScalar();
                                if (id != null && id != DBNull.Value)
                                    _dispatchId = Convert.ToInt32(id);
                            }
                        }

                        transaction.Commit();
                        MessageBox.Show(
                            isEdit ? "ارسال با همکار ویرایش شد." : "ارسال با همکار ثبت شد.",
                            "ثبت موفق",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        LoadNameSuggestions();
                        if (!isEdit)
                            ClearForm();
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        transaction.Rollback();
                        MessageBox.Show(
                            "برای این بارنامه قبلاً ارسال با همکار ثبت شده است.",
                            "تکراری",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("خطا در ثبت اطلاعات: " + ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private static object NullIfEmpty(string text)
        {
            text = (text ?? "").Trim();
            return text.Length == 0 ? (object)DBNull.Value : text;
        }

        private static decimal ParseMoney(string text)
        {
            text = (text ?? "").Replace(",", "").Trim();
            if (decimal.TryParse(text, out decimal value))
                return decimal.Truncate(value);
            return 0;
        }

        private static string FormatMoney(string text)
        {
            text = (text ?? "").Replace(",", "").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return "";
            if (!long.TryParse(text, out long value))
                return text;
            return value.ToString("N0");
        }

        private void MoneyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.Text = FormatMoney(textBox.Text);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا از پاک کردن فرم اطمینان دارید؟",
                "تایید",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                ClearForm();
        }

        private void ClearForm()
        {
            _waybillId = 0;
            _dispatchId = 0;
            _loadedWaybillNumber = "";
            InitializeHeader();
            WaybillNumberTextBox.Text = "";
            ClearWaybillFields();
            ClearPartnerFields();
        }

        private void ClearWaybillFields()
        {
            _waybillId = 0;
            SenderTextBox.Text = "";
            ReceiverTextBox.Text = "";
            ReceiverMobileTextBox.Text = "";
            DestinationCityTextBox.Text = "";
            ReceiptDateTextBox.Text = "";
            CargoTypeTextBox.Text = "";
            QuantityTextBox.Text = "";
            PackagingTextBox.Text = "";
            PayableTextBox.Text = "";
        }

        private void ClearPartnerFields()
        {
            FreightCompanyComboBox.Text = "";
            PartnerReceiptNumberTextBox.Text = "";
            PartnerReceiptDateTextBox.Text = "";
            TehranOperatorComboBox.Text = "";
            UnloadNameComboBox.Text = "";
            UnloadMobileTextBox.Text = "";
            PartnerFreightTextBox.Text = "";
            SettlementTypeComboBox.SelectedIndex = -1;
            SettlementStatusComboBox.SelectedIndex = 0;
            DeliveryStatusComboBox.SelectedIndex = 0;
        }
    }
}
