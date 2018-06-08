using Newtonsoft.Json;

namespace KMeansAzure
{
    public class JsonUtils
    {
        /// <summary>
        /// Helper method to parse JSON text to C# Generic type T (Made it generic so every object can be used, to avoid code duplication and imporve reuseability
        /// </summary>
        /// <typeparam name="T">Type to convert to</typeparam>
        /// <param name="i_JsonString">JSON string to convert from</param>
        /// <returns></returns>
        public static T ParseDataFromJson<T>(string i_JsonString)
        {
            return (JsonConvert.DeserializeObject<T>(i_JsonString));
        }

        /// <summary>
        /// Helper method to parse obejct to JSON text
        /// </summary>
        /// <param name="i_DataToParse">Object to parse</param>
        /// <returns>JSON string</returns>
        public static string ParseDataToJson(object i_DataToParse)
        {
            return (JsonConvert.SerializeObject(i_DataToParse));
        }
    }
}
