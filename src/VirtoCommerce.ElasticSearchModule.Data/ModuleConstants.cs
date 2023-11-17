using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ElasticSearchModule.Data
{
    [ExcludeFromCodeCoverage]
    public static class ModuleConstants
    {
        public const string ProviderName = "ElasticSearch";

        public static class Settings
        {
            public static class Indexing
            {
#pragma warning disable S109
                public static SettingDescriptor IndexTotalFieldsLimit { get; } = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.IndexTotalFieldsLimit",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 1000,
                };

                public static SettingDescriptor TokenFilter { get; } = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.TokenFilter",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "custom_edge_ngram",
                };

                public static SettingDescriptor MinGram { get; } = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MinGram",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 1,
                };

                public static SettingDescriptor MaxGram { get; } = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MaxGram",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 20,
                };

                public static SettingDescriptor DeleteDuplicateIndexes { get; } = new()
                {
                    Name = "VirtoCommerce.Search.ElasticSearch.DeleteDuplicateIndexes",
                    GroupName = "Search|ElasticSearch",
                    ValueType = SettingValueType.Boolean,
                    DefaultValue = true,
                };
#pragma warning restore S109

                public static IEnumerable<SettingDescriptor> AllIndexingSettings
                {
                    get
                    {
                        yield return IndexTotalFieldsLimit;
                        yield return TokenFilter;
                        yield return MinGram;
                        yield return MaxGram;
                        yield return DeleteDuplicateIndexes;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings => Indexing.AllIndexingSettings;
        }
    }
}
