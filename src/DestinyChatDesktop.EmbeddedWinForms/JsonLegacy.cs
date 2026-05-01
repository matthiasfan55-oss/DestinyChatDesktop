using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DestinyChatDesktop.Embedded
{
    /// <summary>
    /// Minimal stand-in for System.Web.Script.Serialization.JavaScriptSerializer using Newtonsoft.Json,
    /// preserving loose Dictionary / ArrayList shapes expected by the legacy UI host.
    /// </summary>
    internal sealed class JavaScriptSerializer
    {
        public T Deserialize<T>(string input)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(input);
        }

        public object DeserializeObject(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            JToken token = JToken.Parse(input);
            return ConvertToken(token);
        }

        public string Serialize(object obj)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        }

        private static object ConvertToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            switch (token.Type)
            {
                case JTokenType.Object:
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    foreach (JProperty prop in ((JObject)token).Properties())
                    {
                        dict[prop.Name] = ConvertToken(prop.Value);
                    }

                    return dict;

                case JTokenType.Array:
                    ArrayList list = new ArrayList();
                    foreach (JToken child in (JArray)token)
                    {
                        list.Add(ConvertToken(child));
                    }

                    return list;

                case JTokenType.Integer:
                    return token.Value<long>();

                case JTokenType.Float:
                    return token.Value<double>();

                case JTokenType.String:
                    return token.Value<string>();

                case JTokenType.Boolean:
                    return token.Value<bool>();

                case JTokenType.Date:
                    return token.Value<DateTime>();

                default:
                    return token.ToString();
            }
        }
    }
}
