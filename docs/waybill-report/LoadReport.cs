// این فایل را به پروژه اضافه نکنید.
// در WaybillReportPage.xaml.cs فقط متد LoadReport را با متد پایین عوض کنید.

private void LoadReport()
{
    if (!TryGetPersianDate(FromDateTextBox, "از تاریخ", out DateTime? fromDate))
        return;

    if (!TryGetPersianDate(ToDateTextBox, "تا تاریخ", out DateTime? toDate))
        return;

    var list = new List<WaybillReportRow>();

    try
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        string senderMobileSql = ContactMobileSql(conn, "S", "SenderMobile");
        string receiverMobileSql = ContactMobileSql(conn, "R", "ReceiverMobile");

        string reportSql = @"
        SELECT
        W.Id,
        CASE WHEN Notes.WaybillId IS NOT NULL THEN N'دارد' ELSE N'' END AS HasNotes,
        W.WaybillNumber,
        ISNULL(W.IsEdited, 0) AS IsEdited,
        Inv.InvoiceNumber,
        CASE
        WHEN S.CompanyName IS NOT NULL AND S.CompanyName <> '' AND S.CompanyName <> N'ندارد'
        THEN S.CompanyName
        ELSE LTRIM(RTRIM(ISNULL(S.FirstName, '') + ' ' + ISNULL(S.LastName, '')))
        END AS SenderName,
        /*SENDER_MOBILE*/,
        CASE
        WHEN R.CompanyName IS NOT NULL AND R.CompanyName <> '' AND R.CompanyName <> N'ندارد'
        THEN R.CompanyName
        ELSE LTRIM(RTRIM(ISNULL(R.FirstName, '') + ' ' + ISNULL(R.LastName, '')))
        END AS ReceiverName,
        /*RECEIVER_MOBILE*/,
        W.ReceiverCode,
        Dest.CityName AS DestinationCity,
        Orig.CityName AS OriginCity,
        CT.CargoTypeName,
        CASE
            WHEN Pd.WaybillId IS NOT NULL THEN N'ارسال با همکار'
            ELSE W.Status
        END AS Status,
        W.TotalQuantity,
        W.Payable,
        ISNULL(ag.AgentTotalFreight, W.TotalFreight) AS AgentTotalFreight,
        ISNULL(ag.AgentPayable, W.Payable) AS AgentPayable,
        ISNULL(ag.AgentPrepaid, W.Prepaid) AS AgentPrepaid,
        ISNULL(ag.AgentRemaining, W.Remaining) AS AgentRemaining,
        W.Prepaid,
        W.Remaining,
        W.Weight,
        W.PackageCount,
        W.CargoValue,
        W.Insurance,
        W.TransitCity,
        W.Forklift,
        W.OtherCost,
        W.TotalFreight,
        W.Description,
        W.ShipmentDate,
        W.CreatedAt
        FROM dbo.Waybills W
        LEFT JOIN dbo.Invoices Inv ON Inv.Id = W.InvoiceId
        LEFT JOIN dbo.Contacts S ON W.SenderId = S.Id
        LEFT JOIN dbo.Contacts R ON W.ReceiverId = R.Id
        LEFT JOIN dbo.Cities Dest ON W.DestinationId = Dest.Id
        LEFT JOIN dbo.Cities Orig ON W.OriginId = Orig.Id
        LEFT JOIN dbo.CargoTypes CT ON W.CargoTypeId = CT.Id
        LEFT JOIN dbo.PartnerDispatches Pd ON Pd.WaybillId = W.Id
        LEFT JOIN (
            SELECT WaybillId
            FROM dbo.WaybillNotes
            GROUP BY WaybillId
        ) Notes ON Notes.WaybillId = W.Id
        LEFT JOIN (
            SELECT
                WaybillId,
                TotalFreight AS AgentTotalFreight,
                Payable AS AgentPayable,
                Prepaid AS AgentPrepaid,
                Remaining AS AgentRemaining
            FROM (
                SELECT
                    a.WaybillId,
                    a.TotalFreight,
                    a.Payable,
                    a.Prepaid,
                    a.Remaining,
                    ROW_NUMBER() OVER (PARTITION BY a.WaybillId ORDER BY a.InvoiceId DESC) AS rn
                FROM dbo.InvoiceWaybillAgentAmounts a
            ) ranked
            WHERE ranked.rn = 1
        ) ag ON ag.WaybillId = W.Id
        LEFT JOIN (
            SELECT WaybillNumber
            FROM dbo.Returns
            WHERE @StatusMode = N'مرجوع شده'
            GROUP BY WaybillNumber
        ) Ret ON Ret.WaybillNumber = W.WaybillNumber
        LEFT JOIN (
            SELECT WaybillNumber
            FROM dbo.Damages
            WHERE @StatusMode = N'خسارت دیده'
            GROUP BY WaybillNumber
        ) Dam ON Dam.WaybillNumber = W.WaybillNumber
        WHERE ISNULL(W.IsDeleted, 0) = 0
        AND (@FromDate IS NULL OR W.CreatedAt >= @FromDate)
        AND (@ToDateExclusive IS NULL OR W.CreatedAt < @ToDateExclusive)
        AND (@SenderId = 0 OR W.SenderId = @SenderId)
        AND (@SenderText = '' OR
        CASE
        WHEN S.CompanyName IS NOT NULL AND S.CompanyName <> '' AND S.CompanyName <> N'ندارد'
        THEN S.CompanyName
        ELSE LTRIM(RTRIM(ISNULL(S.FirstName, '') + ' ' + ISNULL(S.LastName, '')))
        END LIKE @SenderText)
        AND (@DestId = 0 OR W.DestinationId = @DestId)
        AND (@DestText = '' OR ISNULL(Dest.CityName, '') LIKE @DestText)
        AND (
        @Search = '' OR
        LTRIM(RTRIM(ISNULL(W.WaybillNumber, ''))) = @Search OR
        LTRIM(RTRIM(ISNULL(Inv.InvoiceNumber, ''))) = @Search OR
        LTRIM(RTRIM(ISNULL(W.ReceiverCode, ''))) = @Search OR
        LTRIM(RTRIM(
        CASE
        WHEN R.CompanyName IS NOT NULL AND R.CompanyName <> '' AND R.CompanyName <> N'ندارد'
        THEN R.CompanyName
        ELSE LTRIM(RTRIM(ISNULL(R.FirstName, '') + ' ' + ISNULL(R.LastName, '')))
        END
        )) = @Search
        )
        AND (
        @StatusMode = N'همه'
        OR (@StatusMode = N'ارسال نشده' AND W.Status = N'صادر شد' AND Pd.WaybillId IS NULL)
        OR (@StatusMode = N'ارسال شده' AND W.Status = N'در مسیر' AND Pd.WaybillId IS NULL)
        OR (@StatusMode = N'ارسال با همکار' AND Pd.WaybillId IS NOT NULL)
        OR (@StatusMode = N'همه ارسال شده‌ها' AND (
            (W.Status = N'در مسیر' AND Pd.WaybillId IS NULL)
            OR Pd.WaybillId IS NOT NULL))
        OR (@StatusMode = N'مرجوع شده' AND Ret.WaybillNumber IS NOT NULL)
        OR (@StatusMode = N'خسارت دیده' AND Dam.WaybillNumber IS NOT NULL)
        )
        ORDER BY W.Id DESC;";

        reportSql = reportSql.Replace("/*SENDER_MOBILE*/", senderMobileSql);
        reportSql = reportSql.Replace("/*RECEIVER_MOBILE*/", receiverMobileSql);
        using var cmd = new SqlCommand(reportSql, conn);

        cmd.CommandTimeout = 120;

        int senderId = SenderComboBox.SelectedItem is ReportComboItem senderItem ? senderItem.Id : 0;
        string senderText = senderId == 0 ? (SenderComboBox.Text ?? "").Trim() : "";

        int destId = DestinationComboBox.SelectedItem is ReportComboItem destItem ? destItem.Id : 0;
        string destText = destId == 0 ? (DestinationComboBox.Text ?? "").Trim() : "";

        string search = (SearchTextBox.Text ?? "").Trim();
        string statusMode = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ارسال نشده";

        cmd.Parameters.AddWithValue("@FromDate", (object?)fromDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ToDateExclusive", toDate.HasValue ? toDate.Value.Date.AddDays(1) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@SenderId", senderId);
        cmd.Parameters.AddWithValue("@SenderText", string.IsNullOrWhiteSpace(senderText) ? "" : $"%{senderText}%");
        cmd.Parameters.AddWithValue("@DestId", destId);
        cmd.Parameters.AddWithValue("@DestText", string.IsNullOrWhiteSpace(destText) ? "" : $"%{destText}%");
        cmd.Parameters.AddWithValue("@Search", search);
        cmd.Parameters.AddWithValue("@StatusMode", statusMode);

        using var reader = cmd.ExecuteReader();
        int rowNumber = 1;
        while (reader.Read())
        {
            list.Add(new WaybillReportRow
            {
                RowNumber = rowNumber++,
                WaybillNumber = reader["WaybillNumber"]?.ToString() ?? "",
                IsEdited = reader["IsEdited"] != DBNull.Value && Convert.ToBoolean(reader["IsEdited"]),
                InvoiceNumber = reader["InvoiceNumber"]?.ToString() ?? "",
                SenderName = reader["SenderName"]?.ToString() ?? "",
                SenderMobile = reader["SenderMobile"]?.ToString() ?? "",
                ReceiverName = reader["ReceiverName"]?.ToString() ?? "",
                ReceiverMobile = reader["ReceiverMobile"]?.ToString() ?? "",
                ReceiverCode = reader["ReceiverCode"]?.ToString() ?? "",
                DestinationCity = reader["DestinationCity"]?.ToString() ?? "",
                OriginCity = reader["OriginCity"]?.ToString() ?? "",
                CargoTypeName = reader["CargoTypeName"]?.ToString() ?? "",
                Status = reader["Status"]?.ToString() ?? "",
                TotalQuantity = ToInt(reader["TotalQuantity"]),
                Payable = ToInt(reader["Payable"]),
                Prepaid = ToInt(reader["Prepaid"]),
                Remaining = ToInt(reader["Remaining"]),
                Weight = ToInt(reader["Weight"]),
                PackageCount = ToInt(reader["PackageCount"]),
                CargoValue = ToInt(reader["CargoValue"]),
                Insurance = ToInt(reader["Insurance"]),
                TransitCity = ToInt(reader["TransitCity"]),
                Forklift = ToInt(reader["Forklift"]),
                OtherCost = ToInt(reader["OtherCost"]),
                TotalFreight = ToInt(reader["TotalFreight"]),
                AgentTotalFreight = ToInt(reader["AgentTotalFreight"]),
                AgentPayable = ToInt(reader["AgentPayable"]),
                AgentPrepaid = ToInt(reader["AgentPrepaid"]),
                AgentRemaining = ToInt(reader["AgentRemaining"]),
                Description = reader["Description"]?.ToString() ?? "",
                ShipmentDate = ToPersianDate(reader["ShipmentDate"]),
                CreatedAt = ToPersianDate(reader["CreatedAt"]),
                Id = ToInt(reader["Id"]),
                HasNotes = reader["HasNotes"]?.ToString() ?? "",
            });
        }

        WaybillsDataGrid.ItemsSource = null;
        WaybillsDataGrid.ItemsSource = list;
        UpdateColumnTotals();
    }
    catch (Exception ex)
    {
        MessageBox.Show("خطا در بارگذاری گزارش بارنامه\n" + ex.Message);
    }
}
