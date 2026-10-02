# ارسال با همکار

مجوزها را مثل مجوزهای مرجوعی در برنامه تعریف کنید و فقط به کسانی بدهید که باید داشته باشند:

- `PartnerDispatch.View` برای دیدن گزارش و تغییر وضعیت تسویه و وضعیت تحویل
- `PartnerDispatch.Create` برای ثبت ردیف جدید
- `PartnerDispatch.Edit` برای اصلاح بقیه فیلدها، مثل مبلغ. این مجوز برای همه نیست

فایل XAML فرم ثبت را عوض نکنید. فقط `PartnerDispatchPage.xaml.cs` را کامل جایگزین کنید.

برای گزارش، در ویژوال استودیو یک صفحهٔ WPF به نام `PartnerDispatchReportPage` بسازید. کلاسش باید `hamlex.Views.Pages.PartnerDispatchReportPage` باشد. بعد متن `PartnerDispatchReportPage.xaml` و `PartnerDispatchReportPage.xaml.cs` را کامل جایگزین کنید. این صفحه را به عنوان آیتم تکراری اضافه نکنید.

در `MainWindow.xaml` کنار دکمهٔ ثبت، دکمهٔ گزارش را بگذارید. در کد، مجوز دکمهٔ ثبت `PartnerDispatch.Create` و مجوز دکمهٔ گزارش `PartnerDispatch.View` است.

تاریخ گزارش اگر خالی باشد، همهٔ ردیف‌های همکار می‌آیند. دوبل‌کلیک یک ردیف، فرم همان بارنامه را باز می‌کند.
