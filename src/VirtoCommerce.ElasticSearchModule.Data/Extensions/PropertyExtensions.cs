using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ElasticSearchModule.Data.Extensions
{
    public static class PropertyExtensions
    {
        static ConcurrentDictionary<Type, IList<string>> _properties = new ConcurrentDictionary<Type, IList<string>>();

        public static IEnumerable<string> GetPropertyNames<T>(this object obj, int deep)
        {
            var baseType = obj.GetType();
            if (!_properties.ContainsKey(baseType))
            {
                _properties[baseType] = new List<string>();
            }
            else
            {
                return _properties[baseType];
            }

            var properties = GetPropertyNamesInner<T>(obj, deep);
            foreach (var item in properties)
            {
                if (!_properties[baseType].Contains(item))
                {
                    _properties[baseType].Add(item);
                }
            }

            return _properties[baseType];
        }

        static IEnumerable<string> GetPropertyNamesInner<T>(this object obj, int deep)
        {
            if (obj == null || obj.GetType().IsPrimitive || deep <= 0)
            {
                yield return string.Empty;
            }
            else
            {
                var t = obj.GetType();
                var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(x => x.CanRead && x.CanWrite && IsNested(x.PropertyType))
                                .ToList();
                foreach (var propertyInfo in props)
                {
                    var propertyName = propertyInfo.Name.ToCamelCase();
                    if (propertyInfo.PropertyType == typeof(T))
                    {
                        yield return propertyName;
                    }
                    else
                    {
                        var type = GetElementTypeOrSelf(propertyInfo.PropertyType);
                        if (type != null)
                        {
                            var propValue = propertyInfo.GetValue(obj, null);
                            if (propValue != null)
                            {
                                if (propValue is IEnumerable enumerable)
                                {
                                    foreach (var child in enumerable)
                                    {
                                        foreach (var item in GetPropertyNamesInner<T>(child, deep - 1).Where(i => !string.IsNullOrEmpty(i)))
                                        {
                                            yield return $"{propertyName}.{item}";
                                        }
                                    }
                                }
                                else if (propertyInfo.PropertyType.Assembly == t.Assembly)
                                {
                                    foreach (var item in GetPropertyNamesInner<T>(propValue, deep - 1).Where(i => !string.IsNullOrEmpty(i)))
                                    {
                                        yield return $"{propertyName}.{item}";
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static Type GetElementTypeOrSelf(Type type)
        {
            if (type.IsArray && typeof(IEntity).IsAssignableFrom(type.GetElementType()))
            {
                return type.GetElementType();
            }

            if (typeof(IEntity).IsAssignableFrom(type))
            {
                return type;
            }

            if (!type.IsGenericType)
            {
                return type;
            }

            if (type.GetGenericTypeDefinition() != typeof(IList<>))
            {
                return null;
            }

            return type.GetGenericArguments()[0];
        }

        private static bool IsNested(Type type)
        {
            if (type == typeof(string) || (type.IsArray && typeof(string).IsAssignableFrom(type.GetElementType())))
            {
                return false;
            }

            return (type.IsArray && typeof(IEntity).IsAssignableFrom(type.GetElementType()))
                || (typeof(IEntity).IsAssignableFrom(type))
                || !type.IsPrimitive
                || type == typeof(object);
        }

        private static string ToCamelCase(this string str) =>
            string.IsNullOrEmpty(str) || str.Length < 2
                ? str
                : char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}
