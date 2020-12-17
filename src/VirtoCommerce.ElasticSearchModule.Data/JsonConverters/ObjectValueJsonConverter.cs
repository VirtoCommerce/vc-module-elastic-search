using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.Platform.Core.ObjectValue;

namespace VirtoCommerce.ElasticSearchModule.Data.JsonConverters
{
    public class ObjectValueJsonConverter : JsonConverter
    {
        public override bool CanWrite => true;
        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType.GetCustomAttributes(true).Any(a => a.GetType() == typeof(ObjectValueToStringAttribute));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var obj = JObject.FromObject(value, new JsonSerializer() { ContractResolver = serializer.ContractResolver });

            var attribute = value.GetType().GetCustomAttributes(true).OfType<ObjectValueToStringAttribute>().FirstOrDefault();

            if (attribute != null)
            {
                foreach (var name in attribute.Names)
                {
                    var objValue = obj.GetValue(name, StringComparison.InvariantCultureIgnoreCase);
                    if (objValue != null)
                    {
                        obj[objValue.Path] = ConvertValue(objValue.ToObject<object>());
                    }
                }
            }

            obj.WriteTo(writer);
        }

        private string ConvertValue(object value)
        {
            string result;
            var t = value.GetType();

            switch (t)
            {
                case var type when type == typeof(string):
                    result = Convert.ToString(value);
                    break;
                case var type when type == typeof(decimal):
                    result = Convert.ToString(value, CultureInfo.InstalledUICulture);
                    break;
                case var type when type == typeof(DateTime):
                    result = Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToString("O");
                    break;
                case var type when type == typeof(bool):
                    result = value.ToString().ToLower();
                    break;
                default:
                    throw new NotSupportedException();
            }

            return result;
        }
    }




}
