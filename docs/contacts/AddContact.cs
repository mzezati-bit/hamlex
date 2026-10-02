// این فایل را به پروژه اضافه نکنید.
// در DatabaseHelper فقط متد AddContact را با متد پایین عوض کنید
// و دو متد NormalizeContactText و ContactDisplayName را در همان کلاس اضافه کنید.

public int AddContact(
    string contactType,
    string firstName,
    string lastName,
    string companyName,
    string nationalCode,
    string activityType,
    int cityId,
    string phone,
    string mobile,
    string address,
    string description)
{
    firstName = NormalizeContactText(firstName);
    lastName = NormalizeContactText(lastName);
    companyName = NormalizeContactText(companyName);
    string displayName = ContactDisplayName(firstName, lastName, companyName);

    using (var connection = new SqlConnection(connectionString))
    {
        connection.Open();

        if (displayName.Length > 0)
        {
            using (var find = new SqlCommand(@"
                SELECT TOP 1 c.Id
                FROM dbo.Contacts c
                CROSS APPLY (
                    SELECT CASE
                        WHEN LTRIM(RTRIM(ISNULL(c.CompanyName, N''))) <> N''
                         AND LTRIM(RTRIM(c.CompanyName)) <> N'ندارد'
                        THEN LTRIM(RTRIM(c.CompanyName))
                        ELSE LTRIM(RTRIM(ISNULL(c.FirstName, N'') + N' ' + ISNULL(c.LastName, N'')))
                    END AS RawName
                ) n
                CROSS APPLY (
                    SELECT LTRIM(RTRIM(
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                            n.RawName, N'  ', N' '), N'  ', N' '), N'  ', N' '), N'  ', N' '),
                            N'  ', N' '), N'  ', N' '), N'  ', N' '), N'  ', N' ')
                    )) AS DisplayName
                ) d
                WHERE ISNULL(c.IsActive, 1) = 1
                  AND c.ContactType = @contactType
                  AND d.DisplayName = @displayName
                ORDER BY c.Id;", connection))
            {
                find.Parameters.AddWithValue("@contactType", contactType ?? "");
                find.Parameters.AddWithValue("@displayName", displayName);
                object existing = find.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                    return Convert.ToInt32(existing);
            }
        }

        string insertCommand = @"
            INSERT INTO Contacts
            (ContactType, FirstName, LastName, CompanyName, NationalCode, ActivityType, CityId, Phone, Mobile, Address, Description, IsActive)
            VALUES
            (@contactType, @firstName, @lastName, @companyName, @nationalCode, @activityType, @cityId, @phone, @mobile, @address, @description, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var command = new SqlCommand(insertCommand, connection);

        command.Parameters.AddWithValue("@contactType", contactType);
        command.Parameters.AddWithValue("@firstName", firstName);
        command.Parameters.AddWithValue("@lastName", lastName);
        command.Parameters.AddWithValue("@companyName", companyName);
        command.Parameters.AddWithValue("@nationalCode", nationalCode);
        command.Parameters.AddWithValue("@activityType", activityType);
        command.Parameters.AddWithValue("@cityId", cityId);
        command.Parameters.AddWithValue("@phone", phone);
        command.Parameters.AddWithValue("@mobile", mobile);
        command.Parameters.AddWithValue("@address", address);
        command.Parameters.AddWithValue("@description", description);

        int newId = (int)command.ExecuteScalar();
        return newId;
    }
}

private static string NormalizeContactText(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        return "";

    string[] parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    return string.Join(" ", parts);
}

private static string ContactDisplayName(string firstName, string lastName, string companyName)
{
    if (!string.IsNullOrWhiteSpace(companyName) && companyName != "ندارد")
        return companyName;

    if (string.IsNullOrEmpty(firstName))
        return lastName ?? "";
    if (string.IsNullOrEmpty(lastName))
        return firstName;
    return firstName + " " + lastName;
}
