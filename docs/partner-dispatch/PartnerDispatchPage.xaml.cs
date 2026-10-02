using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace hamlex.Views.Pages
{
    public partial class PartnerDispatchPage : Page
    {
        private int _waybillId;
        private int _dispatchId;
        private string _loadedWaybillNumber = "";
        private bool _loadingWaybill;
        private string _pendingWaybillNumber;

        private const string MobileCandidates = "Mobile,MobileNumber,Phone,PhoneNumber,CellPhone,Tel";

        public PartnerDispatchPage()
        {
            InitializeComponent();
            PersianDateMask.Attach(PartnerReceiptDateTextBox);
            InitializeHeader();
            LoadNameSuggestions();
            ApplyFieldAccess();
            Loaded += PartnerDispatchPage_Loaded;
        }

        public PartnerDispatchPage(string waybillNumber) : this()
        {
            _pendingWaybillNumber = waybillNumber;
            Loaded += PartnerDispatchPage_LoadedForEdit;
        }

        private void PartnerDispatchPage_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WaybillNumberTextBox.Focus();
                WaybillNumberTextBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void PartnerDispatchPage_LoadedForEdit(object sender, RoutedEventArgs e)
        {
            Loaded -= PartnerDispatchPage_LoadedForEdit;
            if (string.IsNullOrWhiteSpace(_pendingWaybillNumber))
                return;

            WaybillNumberTextBox.Text = _pendingWaybillNumber;
            LoadWaybillInfo(_pendingWaybillNumber);
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
            if (_waybillId > 0)
                FocusFirstPartnerField();
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
                        W.WaybillDate,
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
                        " + (mobileColumn == null ? "CAST(N'' AS nvarchar(50))" : "ISNULL(R.[" + mobileColumn + "], N'')") + @" AS ReceiverMobile,
                        C.CityName AS DestinationCity,
                        CT.CargoTypeName AS CargoType,
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
                            PayableTextBox.Text = reader["Payable"] == DBNull.Value
                                ? ""
                                : Convert.ToDecimal(reader["Payable"]).ToString("N0");
                            PackagingTextBox.Text = "";

                            int dispatchId = reader["DispatchId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DispatchId"]);
                            if (dispatchId > 0 && (!sameWaybill || _dispatchId != dispatchId))
                            {
                                _dispatchId = dispatchId;
                                FreightCompanyComboBox.Text = reader["FreightCompanyName"]?.ToString() ?? "";
                                PartnerReceiptNumberTextBox.Text = reader["PartnerReceiptNumber"]?.ToString() ?? "";
                                PersianDateMask.Set(PartnerReceiptDateTextBox, reader["PartnerReceiptDate"]?.ToString());
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
                    if (_waybillId > 0)
                    {
                        try
                        {
                            PackagingTextBox.Text = LoadPackaging(connection, _waybillId);
                        }
                        catch
                        {
                            PackagingTextBox.Text = "";
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
                ApplyFieldAccess();
            }
        }

        private void ApplyFieldAccess()
        {
            bool lockDetails = _dispatchId > 0 && !CanEditDetails();
            FreightCompanyComboBox.IsEnabled = !lockDetails;
            PartnerReceiptNumberTextBox.IsEnabled = !lockDetails;
            PartnerReceiptDateTextBox.IsEnabled = !lockDetails;
            TehranOperatorComboBox.IsEnabled = !lockDetails;
            UnloadNameComboBox.IsEnabled = !lockDetails;
            UnloadMobileTextBox.IsEnabled = !lockDetails;
            PartnerFreightTextBox.IsEnabled = !lockDetails;
            SettlementTypeComboBox.IsEnabled = !lockDetails;
        }

        private static bool CanEditDetails()
        {
            return hamlex.Services.CurrentUserSession.HasPermission("PartnerDispatch.Edit");
        }

        private static bool CanUpdateStatus()
        {
            return CanEditDetails()
                || hamlex.Services.CurrentUserSession.HasPermission("PartnerDispatch.View")
                || hamlex.Services.CurrentUserSession.HasPermission("PartnerDispatch.Create");
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


        private static string LoadPackaging(SqlConnection connection, int waybillId)
        {
            string column = FindNamedColumn(
                connection,
                "dbo.WaybillItems",
                "PackageTypeName,PackageType,PackagingType,Packaging,UnitName,ItemName");
            if (column == null || waybillId <= 0)
                return "";

            using (var command = new SqlCommand(@"
                SELECT STUFF((
                    SELECT N'، ' + line.Label
                    FROM (
                        SELECT DISTINCT LTRIM(RTRIM(ISNULL(CONVERT(nvarchar(200), [" + column + @"]), N''))) AS Label
                        FROM dbo.WaybillItems
                        WHERE WaybillId = @WaybillId
                    ) line
                    WHERE line.Label <> N''
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'), 1, 2, N'');", connection))
            {
                command.Parameters.AddWithValue("@WaybillId", waybillId);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? "" : value.ToString();
            }
        }

        private static string FindNamedColumn(SqlConnection connection, string objectName, string candidates)
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
            bool canEditDetails = CanEditDetails();
            if (isEdit && !CanUpdateStatus())
            {
                MessageBox.Show(
                    "شما دسترسی ویرایش ارسال با همکار را ندارید.",
                    "عدم دسترسی",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!isEdit && !hamlex.Services.CurrentUserSession.HasPermission("PartnerDispatch.Create"))
            {
                MessageBox.Show(
                    "شما دسترسی ثبت ارسال با همکار را ندارید.",
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

            if (!PersianDateMask.TryRead(PartnerReceiptDateTextBox.Text, out DateTime? partnerReceiptDate))
            {
                MessageBox.Show(
                    "تاریخ رسید همکار را کامل کنید یا خالی بگذارید. شکل درست: 1404/01/01",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                PartnerReceiptDateTextBox.Focus();
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

                            if (isEdit && !canEditDetails)
                            {
                                command.CommandText = @"
                                    UPDATE dbo.PartnerDispatches SET
                                        SettlementStatus = @SettlementStatus,
                                        DeliveryStatus = @DeliveryStatus
                                    WHERE Id = @Id;";
                                command.Parameters.AddWithValue("@Id", _dispatchId);
                            }
                            else if (isEdit)
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
                            command.Parameters.AddWithValue("@PartnerReceiptDate", partnerReceiptDate.HasValue
                                ? PersianDateMask.Format(partnerReceiptDate.Value)
                                : (object)DBNull.Value);
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
                        {
                            ClearForm();
                            WaybillNumberTextBox.Focus();
                        }
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

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService == null)
                return;

            NavigationService.Navigate(new PartnerDispatchReportPage());
        }

        private void FocusFirstPartnerField()
        {
            if (FreightCompanyComboBox.IsEnabled)
                FreightCompanyComboBox.Focus();
            else
                SettlementStatusComboBox.Focus();
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
            ApplyFieldAccess();
            WaybillNumberTextBox.Focus();
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
            PersianDateMask.Set(PartnerReceiptDateTextBox, "");
            TehranOperatorComboBox.Text = "";
            UnloadNameComboBox.Text = "";
            UnloadMobileTextBox.Text = "";
            PartnerFreightTextBox.Text = "";
            SettlementTypeComboBox.SelectedIndex = -1;
            SettlementStatusComboBox.SelectedIndex = 0;
            DeliveryStatusComboBox.SelectedIndex = 0;
        }
    }

    internal static class PersianDateMask
    {
        public const string Empty = "____/__/__";
        private static readonly int[] Slots = { 0, 1, 2, 3, 5, 6, 8, 9 };

        public static void Attach(TextBox box)
        {
            box.FlowDirection = FlowDirection.LeftToRight;
            box.Text = Empty;
            box.PreviewTextInput += Box_PreviewTextInput;
            box.PreviewKeyDown += Box_PreviewKeyDown;
            DataObject.AddPastingHandler(box, Box_Pasting);
            box.PreviewMouseUp += Box_PreviewMouseUp;
            box.LostFocus += Box_LostFocus;
        }

        public static void Set(TextBox box, string stored)
        {
            box.Text = ToMask(stored);
        }

        public static string Format(DateTime date)
        {
            var calendar = new PersianCalendar();
            return calendar.GetYear(date).ToString("0000")
                + "/"
                + calendar.GetMonth(date).ToString("00")
                + "/"
                + calendar.GetDayOfMonth(date).ToString("00");
        }

        public static bool TryRead(string text, out DateTime? date)
        {
            date = null;
            if (IsEmpty(text))
                return true;

            if (!TryParse(text, out DateTime value))
                return false;

            date = value;
            return true;
        }

        public static string ToMask(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || IsEmpty(text))
                return Empty;

            var parts = text.Split('/');
            if (parts.Length == 3
                && int.TryParse(parts[0], out int year)
                && int.TryParse(parts[1], out int month)
                && int.TryParse(parts[2], out int day))
                return year.ToString("0000") + "/" + month.ToString("00") + "/" + day.ToString("00");

            return PlaceDigits(text);
        }

        private static void Box_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (!(sender is TextBox box))
                return;

            char? digit = ToEnglishDigit(e.Text);
            if (digit.HasValue)
                InsertDigit(box, digit.Value);
        }

        private static void Box_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox box))
                return;

            if (e.Key == Key.Back)
            {
                e.Handled = true;
                ClearSlot(box, PreviousSlot(box.CaretIndex));
                return;
            }

            if (e.Key == Key.Delete)
            {
                e.Handled = true;
                int slot = SlotAtOrAfter(box.CaretIndex);
                if (slot >= 0 && slot < 10)
                    ClearSlot(box, slot);
                return;
            }

            if (e.Key == Key.Left)
            {
                e.Handled = true;
                int slot = PreviousSlot(box.CaretIndex);
                box.CaretIndex = slot < 0 ? 0 : slot;
                return;
            }

            if (e.Key == Key.Right)
            {
                e.Handled = true;
                int slot = SlotAtOrAfter(box.CaretIndex + 1);
                box.CaretIndex = slot < 0 ? 10 : slot;
            }
        }

        private static void Box_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            if (!(sender is TextBox box))
                return;
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
                return;

            string pasted = e.DataObject.GetData(DataFormats.Text) as string;
            box.Text = ToMask(pasted);
            box.CaretIndex = 10;
        }

        private static void Box_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
                box.Text = ToMask(box.Text);
        }

        private static void Box_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is TextBox box))
                return;

            box.Dispatcher.BeginInvoke(new Action(() =>
            {
                int slot = SlotAtOrAfter(box.CaretIndex);
                if (slot >= 0)
                    box.CaretIndex = slot;
            }), DispatcherPriority.Input);
        }

        private static void InsertDigit(TextBox box, char digit)
        {
            if (box.SelectionLength > 0)
            {
                box.Text = Empty;
                box.CaretIndex = 0;
            }

            var chars = ToMask(box.Text).ToCharArray();
            int slot = SlotAtOrAfter(box.CaretIndex);
            if (slot < 0)
                return;

            chars[slot] = digit;
            box.Text = new string(chars);
            int next = SlotAtOrAfter(slot + 1);
            box.CaretIndex = next < 0 ? 10 : next;
        }

        private static void ClearSlot(TextBox box, int slot)
        {
            if (slot < 0)
            {
                box.CaretIndex = 0;
                return;
            }

            var chars = ToMask(box.Text).ToCharArray();
            chars[slot] = '_';
            box.Text = new string(chars);
            box.CaretIndex = slot;
        }

        private static string PlaceDigits(string text)
        {
            var chars = Empty.ToCharArray();
            int slot = 0;
            if (text != null)
            {
                for (int i = 0; i < text.Length && slot < Slots.Length; i++)
                {
                    char? digit = ToEnglishDigit(text[i].ToString());
                    if (digit.HasValue)
                        chars[Slots[slot++]] = digit.Value;
                }
            }

            return new string(chars);
        }

        private static bool IsEmpty(string text)
        {
            text = ToRaw(text);
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = Slots[i];
                if (index < text.Length && text[index] != '_')
                    return false;
            }

            return true;
        }

        private static string ToRaw(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length != 10 || text[4] != '/' || text[7] != '/')
                return Empty;
            return text;
        }

        private static bool TryParse(string text, out DateTime date)
        {
            date = DateTime.MinValue;
            string mask = ToMask(text);
            if (IsEmpty(mask))
                return false;

            for (int i = 0; i < Slots.Length; i++)
            {
                if (mask[Slots[i]] == '_')
                    return false;
            }

            if (!int.TryParse(mask.Substring(0, 4), out int year)
                || !int.TryParse(mask.Substring(5, 2), out int month)
                || !int.TryParse(mask.Substring(8, 2), out int day))
                return false;

            try
            {
                var calendar = new PersianCalendar();
                date = calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static int SlotAtOrAfter(int index)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] >= index)
                    return Slots[i];
            }

            return -1;
        }

        private static int PreviousSlot(int caretIndex)
        {
            int found = -1;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] < caretIndex)
                    found = Slots[i];
            }

            return found;
        }

        private static char? ToEnglishDigit(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            char c = text[0];
            if (c >= '0' && c <= '9')
                return c;
            if (c >= '۰' && c <= '۹')
                return (char)('0' + (c - '۰'));
            if (c >= '٠' && c <= '٩')
                return (char)('0' + (c - '٠'));
            return null;
        }
    }
}
