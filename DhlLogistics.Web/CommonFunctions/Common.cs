using Syncfusion.Blazor.Grids;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DhlLogistics.Web.CommonFunctions;

public static class Common
{
    public static Dictionary<string, string> GetDisplayNames<T>()
    {
        Type type = typeof(T);
        PropertyInfo[] properties = type.GetProperties();
        Dictionary<string, string> displayNamesDictionary = new Dictionary<string, string>();

        foreach (var property in properties)
        {
            var displayNameAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
            if (displayNameAttribute != null)
            {
                displayNamesDictionary.Add(property.Name, displayNameAttribute.DisplayName);
            }
            else
            {
                displayNamesDictionary.Add(property.Name, property.Name);
            }
        }

        return displayNamesDictionary;
    }

    public static string ToIndianNumberFormat(this decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("en-IN"), "{0:N2}", amount);
    }

    public static string ReplaceSlugInvalidCharacters(string value)
    {
        return Regex.Replace(value, "[^a-zA-Z0-9]", "");
    }

    public static DialogSettings DialogParams = new DialogSettings
    {
        ZIndex = 50000
    };

    public static DialogSettings DialogParamsNext = new DialogSettings
    {
        ZIndex = 99999
    };
}
