// این فایل را به پروژه اضافه نکنید.
// در WaybillReportPage.xaml.cs فقط متد ExcelButton_Click را با متد پایین عوض کنید.

private void ExcelButton_Click(object sender, RoutedEventArgs e)
{
    if (!hamlex.Services.CurrentUserSession.HasPermission("WaybillReport.Export"))
    {
        MessageBox.Show("شما دسترسی خروجی گزارش بارنامه را ندارید.", "عدم دسترسی", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    var rows = (WaybillsDataGrid.ItemsSource as IEnumerable<WaybillReportRow>)?.ToList();
    if (rows == null || rows.Count == 0)
    {
        MessageBox.Show("برای خروجی، اول گزارش را اعمال کنید تا ردیفی در جدول باشد.");
        return;
    }

    var ids = rows.Where(x => x.Id > 0).Select(x => x.Id).Distinct().ToList();
    if (ids.Count == 0)
    {
        MessageBox.Show("شناسه بارنامه‌ها برای خروجی آماده نیست. یک‌بار فیلتر را دوباره اعمال کنید.");
        return;
    }

    var dialog = new SaveFileDialog
    {
        Filter = "Excel (*.csv)|*.csv",
        FileName = "گزارش-بارنامه-" + DateTime.Now.ToString("yyyyMMdd-HHmm"),
        OverwritePrompt = true
    };

    if (dialog.ShowDialog() != true)
        return;

    try
    {
        var lines = new List<string>();
        lines.Add(string.Join(",",
            Csv("شماره بارنامه"),
            Csv("شماره صورتحساب"),
            Csv("تاریخ"),
            Csv("فرستنده"),
            Csv("گیرنده"),
            Csv("شناسه گیرنده"),
            Csv("مقصد"),
            Csv("نوع محموله"),
            Csv("نام کالا"),
            Csv("واحد"),
            Csv("تعداد"),
            Csv("مبلغ واحد"),
            Csv("جمع قلم"),
            Csv("قابل پرداخت"),
            Csv("قابل پرداخت نماینده"),
            Csv("پیشکرایه"),
            Csv("مانده/پسکرایه"),
            Csv("مانده/پسکرایه نماینده"),
            Csv("ارزش محموله"),
            Csv("بیمه"),
            Csv("وضعیت"),
            Csv("موبایل فرستنده"),
            Csv("موبایل گیرنده")));

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        string senderMobileSql = ContactMobileSql(conn, "S", "SenderMobile");
        string receiverMobileSql = ContactMobileSql(conn, "R", "ReceiverMobile");

        string exportSql = $@"
        SELECT
        W.WaybillNumber,
        Inv.InvoiceNumber,
        W.CreatedAt,
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
        CT.CargoTypeName,
        I.ItemName,
        I.UnitName,
        I.Quantity,
        I.Freight,
        I.RowTotal,
        W.Payable,
        ISNULL(ag.AgentPayable, W.Payable) AS AgentPayable,
        W.Prepaid,
        W.Remaining,
        ISNULL(ag.AgentRemaining, W.Remaining) AS AgentRemaining,
        W.CargoValue,
        W.Insurance,
        W.Status
        FROM dbo.Waybills W
        LEFT JOIN dbo.Invoices Inv ON Inv.Id = W.InvoiceId
        LEFT JOIN dbo.Contacts S ON W.SenderId = S.Id
        LEFT JOIN dbo.Contacts R ON W.ReceiverId = R.Id
        LEFT JOIN dbo.Cities Dest ON W.DestinationId = Dest.Id
        LEFT JOIN dbo.CargoTypes CT ON W.CargoTypeId = CT.Id
        LEFT JOIN dbo.WaybillItems I ON I.WaybillId = W.Id
        OUTER APPLY (
        SELECT TOP 1
        a.Payable AS AgentPayable,
        a.Remaining AS AgentRemaining
        FROM dbo.InvoiceWaybillAgentAmounts a
        WHERE a.WaybillId = W.Id
        ORDER BY a.InvoiceId DESC
        ) ag
        WHERE W.Id IN ({string.Join(",", ids)})
        ORDER BY W.Id DESC, I.RowNumber;";

        exportSql = exportSql.Replace("/*SENDER_MOBILE*/", senderMobileSql);
        exportSql = exportSql.Replace("/*RECEIVER_MOBILE*/", receiverMobileSql);
        using var cmd = new SqlCommand(exportSql, conn);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lines.Add(string.Join(",",
                Csv(reader["WaybillNumber"]),
                Csv(reader["InvoiceNumber"]),
                Csv(ToPersianDate(reader["CreatedAt"])),
                Csv(reader["SenderName"]),
                Csv(reader["ReceiverName"]),
                Csv(reader["ReceiverCode"]),
                Csv(reader["DestinationCity"]),
                Csv(reader["CargoTypeName"]),
                Csv(reader["ItemName"]),
                Csv(reader["UnitName"]),
                Csv(ToInt(reader["Quantity"]).ToString("N0")),
                Csv(ToInt(reader["Freight"]).ToString("N0")),
                Csv(ToInt(reader["RowTotal"]).ToString("N0")),
                Csv(ToInt(reader["Payable"]).ToString("N0")),
                Csv(ToInt(reader["AgentPayable"]).ToString("N0")),
                Csv(ToInt(reader["Prepaid"]).ToString("N0")),
                Csv(ToInt(reader["Remaining"]).ToString("N0")),
                Csv(ToInt(reader["AgentRemaining"]).ToString("N0")),
                Csv(ToInt(reader["CargoValue"]).ToString("N0")),
                Csv(ToInt(reader["Insurance"]).ToString("N0")),
                Csv(reader["Status"]),
                Csv(reader["SenderMobile"]),
                Csv(reader["ReceiverMobile"])));
        }

        File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));

        var open = MessageBox.Show("فایل ذخیره شد. با Excel باز شود؟", "خروجی Excel",
            MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (open == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        MessageBox.Show("خطا در خروجی Excel\n" + ex.Message);
    }
}
