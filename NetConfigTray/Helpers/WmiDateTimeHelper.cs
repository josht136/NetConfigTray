using System.Management;

namespace NetConfigTray.Helpers;

internal static class WmiDateTimeHelper
{
    public static string? Format(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return ManagementDateTimeConverter.ToDateTime(text).ToString("g");
        }
        catch
        {
            return null;
        }
    }
}
