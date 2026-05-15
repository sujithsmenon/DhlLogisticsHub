using System.Xml.Serialization;

namespace DhlLogistics.Web.CommonFunctions
{
    public class XmlService<T>
    {
        private readonly HttpClient _httpClient;

        public XmlService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<T?> LoadXmlAsync(string filePath)
        {
            var xmlContent = await _httpClient.GetStreamAsync(filePath);
            var serializer = new XmlSerializer(typeof(T));
            return (T?)serializer.Deserialize(xmlContent);
        }

        public async Task<T?> LoadXmlFromStreamAsync(Stream stream)
        {
            using StreamReader reader = new StreamReader(stream);
            string xmlContent = await reader.ReadToEndAsync();

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using StringReader stringReader = new StringReader(xmlContent);

            return (T?)serializer.Deserialize(stringReader);
        }
    }
}
