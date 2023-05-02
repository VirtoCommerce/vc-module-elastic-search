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
            GetPropertyNamesInner<T>(obj, parentName: null, deep, result);

            return result;
        }

        private static void GetPropertyNamesInner<T>(this object obj, string parentName, int deep, ISet<string> result)
        {
            if (obj == null || deep <= 0 || obj.GetType().IsPrimitive)
            {
                return;
            }

            var type = obj.GetType();

            var properties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x =>
                    x.CanRead &&
                    x.CanWrite &&
                    (x.PropertyType == typeof(T) || HasNestedProperties(x.PropertyType)))
                .ToList();

            foreach (var propertyInfo in properties)
            {
                var propertyName = propertyInfo.Name.ToCamelCase();

                var fullName = parentName == null
                    ? propertyName
                    : $"{parentName}.{propertyName}";

                if (propertyInfo.PropertyType == typeof(T))
                {
                    result.Add(fullName);
                }
                else
                {
                    var propValue = propertyInfo.GetValue(obj, null);
                    if (propValue != null)
                    {
                        if (propValue is IEnumerable enumerable)
                        {
                            foreach (var child in enumerable)
                            {
                                GetPropertyNamesInner<T>(child, fullName, deep - 1, result);
                            }
                        }
                        else if (propertyInfo.PropertyType.Assembly == type.Assembly)
                        {
                            GetPropertyNamesInner<T>(propValue, fullName, deep - 1, result);
                        }
                    }
                }
            }
        }

        private static bool HasNestedProperties(Type type)
        {
            if (type == typeof(string) || type.IsAssignableTo(typeof(IEnumerable<string>)))
            {
                return false;
            }

            return !type.IsPrimitive;
        }

        private static string ToCamelCase(this string str) =>
            string.IsNullOrEmpty(str) || str.Length < 2
                ? str
                : char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}
