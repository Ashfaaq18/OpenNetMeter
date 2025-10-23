using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json; 

namespace OpenNetMeter.Utilities
{
    public static class JsonHelper
    {
        /// <summary>
        /// Serializes any C# object into a pretty-printed (indented) JSON string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="settings">Optional custom JSON settings.</param>
        /// <returns>An indented JSON string representation of the object.</returns>
        public static string PrettyPrint(object obj, JsonSerializerSettings? settings = null)
        {
            if (obj == null)
            {
                return "null"; // Standard JSON representation for null
            }

            try
            {
                // Use default settings if none are provided
                var defaultSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    // Add other common settings here if you like, e.g.:
                    // ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    // NullValueHandling = NullValueHandling.Ignore
                };

                return JsonConvert.SerializeObject(obj, settings ?? defaultSettings);
            }
            catch (JsonException ex)
            {
                // Handle serialization errors (e.g., self-referencing loops)
                return $"Error serializing object: {ex.Message}";
            }
        }
    }
}
