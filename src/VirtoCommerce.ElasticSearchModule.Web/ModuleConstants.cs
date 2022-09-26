using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ElasticSearchModule.Web
{
    [ExcludeFromCodeCoverage]
    public static class ModuleConstants
    {
        public static class Settings
        {
            public static class Indexing
            {
#pragma warning disable S109
                private static readonly SettingDescriptor IndexTotalFieldsLimit = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.IndexTotalFieldsLimit",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 1000
                };

                private static readonly SettingDescriptor TokenFilter = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.TokenFilter",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "custom_edge_ngram"
                };

                private static readonly SettingDescriptor MinGram = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MinGram",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 1
                };

                private static readonly SettingDescriptor MaxGram = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MaxGram",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 20
                };
#pragma warning restore S109
                public static IEnumerable<SettingDescriptor> AllSettings
                {
                    get
                    {
                        yield return IndexTotalFieldsLimit;
                        yield return TokenFilter;
                        yield return MinGram;
                        yield return MaxGram;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings => Indexing.AllSettings;
        }
    }
}
