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
                if (property.PropertyType == typeof(T))
                {
                    var propertyName = GetFullName(parentName, property.Name);
                    result.Add(propertyName);
                    continue;
                }

                var value = property.GetValue(obj, null);

                if (value is IEnumerable enumerable)
                {
                    var newParentName = GetFullName(parentName, property.Name);
                    foreach (var item in enumerable)
                    {
                        GetPropertyNamesInner<T>(item, deep - 1, newParentName, result);
                    }
                }
                else if (value != null && property.PropertyType.Assembly == type.Assembly)
                {
                    var newParentName = GetFullName(parentName, property.Name);
                    GetPropertyNamesInner<T>(value, deep - 1, newParentName, result);
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

        private static string GetFullName(string parentName, string name)
        {
            return string.IsNullOrEmpty(parentName)
                ? name.ToCamelCase()
                : $"{parentName}.{name.ToCamelCase()}";
        }

        private static string ToCamelCase(this string str) =>
            string.IsNullOrEmpty(str) || str.Length < 2
                ? str
                : char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}
