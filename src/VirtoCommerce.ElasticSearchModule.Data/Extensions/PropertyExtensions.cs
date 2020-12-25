using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VirtoCommerce.ElasticSearchModule.Data.Extensions
{
    public static class PropertyExtensions
    {
        public static IEnumerable<string> GetFullPropertyNamesFromObject<T>(this object obj, int deep)
        {
            if (obj == null || obj.GetType().IsPrimitive || deep <= 0)
            {
                yield return string.Empty;
            }
            else
            {
                Type t = obj.GetType();
                var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(x => x.CanRead && x.CanWrite)
                                .ToList();
                foreach (var propertyInfo in props)
                {
                    if (propertyInfo.PropertyType == typeof(T))
                    {
                        yield return propertyInfo.Name;
                    }
                    else
                    {
                        object propValue = propertyInfo.GetValue(obj, null);
                        if (propValue != null)
                        {
                            if (propValue is IList)
                            {
                                IEnumerable enumerable = (IEnumerable)propValue;
                                foreach (object child in enumerable)
                                {
                                    var nestedObjects = GetFullPropertyNamesFromObject<T>(child, deep - 1);
                                    foreach (var item in nestedObjects)
                                    {
                                        if (!string.IsNullOrEmpty(item))
                                        {
                                            yield return $"{propertyInfo.Name}.{item}";
                                        }
                                    }
                                }
                            }
                            else if (propertyInfo.PropertyType.Assembly == t.Assembly)
                            {
                                var nestedObjects = GetFullPropertyNamesFromObject<T>(propValue, deep - 1);
                                foreach (var item in nestedObjects)
                                {
                                    if (!string.IsNullOrEmpty(item))
                                    {
                                        yield return $"{propertyInfo.Name}.{item}";
                                    }
                                }
                            }
                                        
                        }
                    }
                }
            }
        }
    }
}
