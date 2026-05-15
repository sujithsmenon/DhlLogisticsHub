using System.Dynamic;
using System.Text.Json;

namespace DhlLogistics.Web.CommonFunctions
{
    public static class ObjectExtensions
    {
        public static T? CopyObject<T>(this T objSource)
        {
            var jsonString = JsonSerializer.Serialize(objSource);
            return JsonSerializer.Deserialize<T>(jsonString);
        }

        static object? GetValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.Object => ConvertToExpandoList(new List<JsonElement> { element }),
                JsonValueKind.Array  => element.EnumerateArray().Select(GetValue).ToList(),
                JsonValueKind.Null   => null,
                _ => element.GetRawText()
            };
        }

        public static List<ExpandoObject> ConvertToExpandoList(List<JsonElement> objectList)
        {
            var expandoList = new List<ExpandoObject>();

            foreach (var element in objectList)
            {
                var expandoObj = new ExpandoObject();
                var dict = (IDictionary<string, object?>)expandoObj;

                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = GetValue(property.Value);
                }

                expandoList.Add(expandoObj);
            }

            return expandoList;
        }
    }
}
