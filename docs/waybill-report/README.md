# موبایل فرستنده و گیرنده در گزارش بارنامه

شماره موبایل روی خود بارنامه نیست. روی تماس فرستنده و تماس گیرنده در `dbo.Contacts` است. این راهنما دو ستون اختیاری به گزارش بارنامه اضافه می‌کند و همان شماره‌ها را به انتهای خروجی CSV هم می‌گذارد. ستون‌ها پیش‌فرض خاموش‌اند و از «انتخاب ستون‌ها» روشن می‌شوند.

فایل تازه‌ای به پروژه اضافه نکنید. فقط `WaybillReportPage.xaml` و `WaybillReportPage.xaml.cs` را در همان جاهایی که پایین آمده ویرایش کنید.

## ۱. ستون‌های جدول

در `WaybillReportPage.xaml`، دو ستون فرستنده و گیرنده را با این چهار خط عوض کنید:

```xml
<DataGridTextColumn x:Name="ColSender" Header="فرستنده" Binding="{Binding SenderName}" Width="180"/>
<DataGridTextColumn x:Name="ColSenderMobile" Header="موبایل فرستنده" Binding="{Binding SenderMobile}" Width="120" Visibility="Collapsed"/>
<DataGridTextColumn x:Name="ColReceiver" Header="گیرنده" Binding="{Binding ReceiverName}" Width="180"/>
<DataGridTextColumn x:Name="ColReceiverMobile" Header="موبایل گیرنده" Binding="{Binding ReceiverMobile}" Width="120" Visibility="Collapsed"/>
```

## ۲. انتخاب ستون

در `GetReportColumns`، بلافاصله بعد از فرستنده و گیرنده این دو سطر را اضافه کنید. به `DefaultColumnKeys` اضافه نکنید تا پیش‌فرض جدول عوض نشود.

```csharp
("ColSender", "فرستنده", ColSender),
("ColSenderMobile", "موبایل فرستنده", ColSenderMobile),
("ColReceiver", "گیرنده", ColReceiver),
("ColReceiverMobile", "موبایل گیرنده", ColReceiverMobile),
```

## ۳. مدل ردیف

کنار `SenderName` و `ReceiverName` در کلاس `WaybillReportRow`:

```csharp
public string SenderMobile { get; set; }
public string ReceiverMobile { get; set; }
```

## ۴. بارگذاری گزارش

در `LoadReport`، بلافاصله بعد از `conn.Open()`:

```csharp
string senderMobileSql = ContactMobileSql(conn, "S", "SenderMobile");
string receiverMobileSql = ContactMobileSql(conn, "R", "ReceiverMobile");
```

در همان دستور، این قطعه:

```csharp
        END AS SenderName,
        CASE
```

با این عوض شود:

```csharp
        END AS SenderName,
" + senderMobileSql + @",
        CASE
```

و این قطعه:

```csharp
        END AS ReceiverName,
        W.ReceiverCode,
```

با این عوض شود:

```csharp
        END AS ReceiverName,
" + receiverMobileSql + @",
        W.ReceiverCode,
```

در ساخت `WaybillReportRow`، کنار نام‌ها:

```csharp
SenderName = reader["SenderName"]?.ToString() ?? "",
SenderMobile = reader["SenderMobile"]?.ToString() ?? "",
ReceiverName = reader["ReceiverName"]?.ToString() ?? "",
ReceiverMobile = reader["ReceiverMobile"]?.ToString() ?? "",
```

متد زیر را در همان کلاس صفحه اضافه کنید:

```csharp
private static string ContactMobileSql(SqlConnection connection, string alias, string resultName)
{
    if (alias != "S" && alias != "R")
        throw new ArgumentException(alias);

    string column = null;
    using (var command = new SqlCommand(@"
        SELECT TOP 1 c.name
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Contacts')
          AND c.name IN (N'Mobile', N'MobileNumber', N'Phone', N'PhoneNumber', N'CellPhone', N'Tel')
        ORDER BY CASE c.name
            WHEN N'Mobile' THEN 0
            WHEN N'MobileNumber' THEN 1
            WHEN N'Phone' THEN 2
            WHEN N'PhoneNumber' THEN 3
            WHEN N'CellPhone' THEN 4
            ELSE 5
        END;", connection))
    {
        object found = command.ExecuteScalar();
        if (found != null && found != DBNull.Value)
            column = found.ToString();
    }

    string allowed = ",Mobile,MobileNumber,Phone,PhoneNumber,CellPhone,Tel,";
    string valueSql = column != null && allowed.Contains("," + column + ",")
        ? "ISNULL(" + alias + ".[" + column + "], N'')"
        : "CAST(N'' AS nvarchar(50))";

    return valueSql + " AS " + resultName;
}
```

## ۵. خروجی CSV

شماره‌ها همیشه در فایل می‌آیند، حتی اگر ستون در جدول خاموش باشد. دو ستون به آخر سرستون‌ها اضافه شده تا ترتیب ستون‌های قبلی عوض نشود.

در `lines.Add` اول، آخرین سرستون را این‌طور عوض کنید:

```csharp
Csv("وضعیت"),
Csv("موبایل فرستنده"),
Csv("موبایل گیرنده")));
```

در `ExcelButton_Click` هم بلافاصله بعد از `conn.Open()` همان دو خط `senderMobileSql` و `receiverMobileSql` را بگذارید.

در دستور خروجی، همان دو قطعهٔ `END AS SenderName` و `END AS ReceiverName` را مثل `LoadReport` بشکنید. فقط چون این دستور از قبل `$` دارد، قطعهٔ دوم را با `$` ببندید تا `IN ({string.Join...})` خراب نشود:

```csharp
        END AS SenderName,
" + senderMobileSql + @",
        CASE
```

```csharp
        END AS ReceiverName,
" + receiverMobileSql + $@",
        W.ReceiverCode,
```

در `lines.Add` هر ردیف، آخرین مقدار را این‌طور عوض کنید:

```csharp
Csv(reader["Status"]),
Csv(reader["SenderMobile"]),
Csv(reader["ReceiverMobile"])));
```

برای سامانهٔ وب، دو سرستون آخر فایل `موبایل فرستنده` و `موبایل گیرنده` هستند. اگر برای آن تماس شماره ثبت نشده باشد، همان خانه خالی است.
