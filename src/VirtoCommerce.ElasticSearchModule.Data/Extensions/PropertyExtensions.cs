using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VirtoCommerce.ElasticSearchModule.Data.Extensions
{
    public static class PropertyExtensions
    {
        public readonly static HashSet<string> _path = new HashSet<string>();
        public static IEnumerable<string> GetFullPropertyNames<T>(this object obj, int deep)
        {
            if (obj == null || obj.GetType().IsPrimitive || deep <= 0)
            {
                yield return string.Empty;
            }
            else
            {
                var t = obj.GetType();
                var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(x => x.CanRead && x.CanWrite)
                                .ToList();
                foreach (var propertyInfo in props)
                {
                    if (propertyInfo.PropertyType == typeof(T))
                    {
                        yield return propertyInfo.Name.ToCamelCase();
                    }
                    else
                    {
                        var propValue = propertyInfo.GetValue(obj, null);
                        if (propValue != null)
                        {
                            if (propValue is IList)
                            {
                                var enumerable = (IEnumerable)propValue;
                                foreach (var child in enumerable)
                                {
                                    foreach (var item in GetFullPropertyNames<T>(child, deep - 1).Where(i => !string.IsNullOrEmpty(i)))
                                    {
                                        var path = $"{propertyInfo.Name.ToCamelCase()}.{item}";
                                        if (!_path.Any(p => p.Equals(path)))
                                        {
                                            _path.Add(path);
                                            yield return path;
                                        }
                                    }
                                }
                            }
                            else if (propertyInfo.PropertyType.Assembly == t.Assembly)
                            {
                                foreach (var item in GetFullPropertyNames<T>(propValue, deep - 1).Where(i => !string.IsNullOrEmpty(i)))
                                {
                                    yield return $"{propertyInfo.Name.ToCamelCase()}.{item}";
                                }
                            }
                                        
                        }
                    }
                }
            }
        }

        public static string ToCamelCase(this string str) => string.IsNullOrEmpty(str) || str.Length< 2 ? str : char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}
