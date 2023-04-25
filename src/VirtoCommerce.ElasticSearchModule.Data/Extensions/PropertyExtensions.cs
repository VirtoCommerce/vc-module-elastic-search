using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VirtoCommerce.ElasticSearchModule.Data.Extensions
{
    public static class PropertyExtensions
    {
        public static ISet<string> GetPropertyNames<T>(this object obj, int deep)
        {
            var result = new HashSet<string>();
            GetPropertyNamesInner<T>(obj, deep, parentName: null, result: result);

            return result;
        }

        private static void GetPropertyNamesInner<T>(this object obj, int deep, string parentName, ISet<string> result)
        {
            if (obj == null || deep <= 0)
            {
                return;
            }

            var type = obj.GetType();

            var properties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x =>
                    x.CanRead &&
                    x.CanWrite &&
                    (x.PropertyType == typeof(T) || HasNestedProperties(x.PropertyType)));

            foreach (var property in properties)
            {
                var propertyName = parentName == null
                    ? property.Name.ToCamelCase()
                    : $"{parentName}.{property.Name.ToCamelCase()}";

                if (property.PropertyType == typeof(T))
                {
                    result.Add(propertyName);
                }
                else
                {
                    var propValue = property.GetValue(obj, null);
                    if (propValue != null)
                    {
                        if (propValue is IEnumerable enumerable)
                        {
                            foreach (var child in enumerable)
                            {
                                GetPropertyNamesInner<T>(child, deep - 1, propertyName, result);
                            }
                        }
                        else if (property.PropertyType.Assembly == type.Assembly)
                        {
                            GetPropertyNamesInner<T>(propValue, deep - 1, propertyName, result);
                        }
                    }
                }
            }
        }

        private static bool HasNestedProperties(Type type)
        {
            return
                !type.IsPrimitive &&
                type != typeof(string) &&
                !type.IsAssignableTo(typeof(IEnumerable<string>));
        }

        private static string ToCamelCase(this string str) =>
            string.IsNullOrEmpty(str) || str.Length < 2
                ? str
                : char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}
