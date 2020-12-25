using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.Extensions.Options;
using Nest;
using Nest.JsonNetSerializer;
using VirtoCommerce.ElasticSearchModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using SearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;

namespace VirtoCommerce.ElasticSearchModule.Data
{
    public class ElasticSearchProvider : ISearchProvider
    {
        public const string SearchableFieldAnalyzerName = "searchable_field_analyzer";
        public const string NGramFilterName = "custom_ngram";
        public const string EdgeNGramFilterName = "custom_edge_ngram";

        private readonly ISettingsManager _settingsManager;
        private readonly ConcurrentDictionary<string, Properties<IProperties>> _mappings = new ConcurrentDictionary<string, Properties<IProperties>>();
        private readonly SearchOptions _searchOptions;
        private readonly ElasticSearchOptions _elasticSearchOptions;

        public ElasticSearchProvider(
            IOptions<ElasticSearchOptions> elasticSearchOptions,
            IOptions<SearchOptions> searchOptions,
            ISettingsManager settingsManager)
            : this(elasticSearchOptions, searchOptions, settingsManager, new ElasticClient(GetConnectionSettings(elasticSearchOptions.Value)))
        {
        }

        public ElasticSearchProvider(IOptions<ElasticSearchOptions> elasticSearchOptions,
            IOptions<SearchOptions> searchOptions, ISettingsManager settingsManager, IElasticClient client)
            : this(elasticSearchOptions, searchOptions, settingsManager, client, new ElasticSearchRequestBuilder())
        {
        }

        public ElasticSearchProvider(
            IOptions<ElasticSearchOptions> elasticSearchOptions, IOptions<SearchOptions> searchOptions,
            ISettingsManager settingsManager, IElasticClient client, ElasticSearchRequestBuilder requestBuilder)
        {
            if (searchOptions == null)
                throw new ArgumentNullException(nameof(searchOptions));

            if (elasticSearchOptions == null)
                throw new ArgumentNullException(nameof(elasticSearchOptions));

            _settingsManager = settingsManager;
            Client = client;
            RequestBuilder = requestBuilder;
            ServerUrl = client.ConnectionSettings.ConnectionPool.Nodes.First().Uri;
            _elasticSearchOptions = elasticSearchOptions.Value;
            _searchOptions = searchOptions.Value;
        }

        protected IElasticClient Client { get; }
        protected ElasticSearchRequestBuilder RequestBuilder { get; }
        protected Uri ServerUrl { get; }

        public virtual async Task DeleteIndexAsync(string documentType)
        {
            if (string.IsNullOrEmpty(documentType))
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            try
            {
                var indexName = GetIndexName(documentType);

                var response = await Client.Indices.DeleteAsync(indexName);
                if (!response.IsValid && response.ApiCall.HttpStatusCode != 404)
                {
                    throw new SearchException(response.DebugInformation);
                }

                RemoveMappingFromCache(indexName);
            }
            catch (Exception ex)
            {
                ThrowException("Failed to delete index", ex);
            }
        }

        public virtual async Task<IndexingResult> IndexAsync(string documentType, IList<IndexDocument> documents)
        {
            var indexName = GetIndexName(documentType);
            var providerFields = await GetMappingAsync(indexName);
            var oldFieldsCount = providerFields.Count();
            var providerDocuments = documents.Select(document => ConvertToProviderDocument(document, providerFields)).ToList();
            var updateMapping = providerFields.Count() != oldFieldsCount;
            var indexExits = await IndexExistsAsync(indexName);

            if (!indexExits)
            {
                await CreateIndexAsync(indexName);
            }

            if (!indexExits || updateMapping)
            {
                await UpdateMappingAsync(indexName, providerFields);
            }

            var bulkDefinition = new BulkDescriptor();
            bulkDefinition.IndexMany(providerDocuments).Index(indexName);

            var bulkResponse = await Client.BulkAsync(bulkDefinition);
            await Client.Indices.RefreshAsync(indexName);

            var result = new IndexingResult
            {
                Items = bulkResponse.Items.Select(i => new IndexingResultItem
                {
                    Id = i.Id,
                    Succeeded = i.IsValid,
                    ErrorMessage = i.Error?.Reason
                }).ToArray()
            };

            return result;
        }

        public virtual async Task<IndexingResult> RemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            var providerDocuments = documents.Select(d => new SearchDocument { Id = d.Id }).ToArray();
            var indexName = GetIndexName(documentType);
            var bulkDefinition = new BulkDescriptor();
            bulkDefinition.DeleteMany(providerDocuments).Index(indexName);

            var bulkResponse = await Client.BulkAsync(bulkDefinition);
            await Client.Indices.RefreshAsync(indexName);

            var result = new IndexingResult
            {
                Items = bulkResponse.Items.Select(i => new IndexingResultItem
                {
                    Id = i.Id,
                    Succeeded = i.IsValid,
                    ErrorMessage = i.Error?.Reason
                }).ToArray()
            };

            return result;
        }

        public virtual async Task<SearchResponse> SearchAsync(string documentType, SearchRequest request)
        {
            var indexName = GetIndexName(documentType);
            ISearchResponse<SearchDocument> providerResponse;

            try
            {
                var availableFields = await GetMappingAsync(indexName);
                var providerRequest = RequestBuilder.BuildRequest(request, indexName, availableFields);
                providerResponse = await Client.SearchAsync<SearchDocument>(providerRequest);
            }
            catch (Exception ex)
            {
                throw new SearchException(ex.Message, ex);
            }

            if (!providerResponse.IsValid)
            {
                ThrowException(providerResponse.DebugInformation, null);
            }

            var result = providerResponse.ToSearchResponse(request);
            return result;
        }


        protected virtual SearchDocument ConvertToProviderDocument(IndexDocument document, Properties<IProperties> properties)
        {
            var result = new SearchDocument { Id = document.Id };

            foreach (var field in document.Fields.OrderBy(f => f.Name))
            {
                var fieldName = ElasticSearchHelper.ToElasticFieldName(field.Name);

                if (result.ContainsKey(fieldName))
                {
                    var newValues = new List<object>();
                    var currentValue = result[fieldName];

                    if (currentValue is object[] currentValues)
                    {
                        newValues.AddRange(currentValues);
                    }
                    else
                    {
                        newValues.Add(currentValue);
                    }

                    newValues.AddRange(field.Values);
                    result[fieldName] = newValues.ToArray();
                }
                else
                {
                    if (properties is IDictionary<PropertyName, IProperty> dictionary
                        && !dictionary.ContainsKey(fieldName))
                    {
                        // Create new property mapping
                        var providerField = CreateProviderField(field);
                        ConfigureProperty(providerField, field);
                        properties.Add(fieldName, providerField);
                    }

                    var isCollection = field.IsCollection || field.Values.Count > 1;

                    var point = field.Value as GeoPoint;
                    var value = point != null
                        ? (isCollection ? field.Values.Select(v => ((GeoPoint)v).ToElasticValue()).ToArray() : point.ToElasticValue())
                        : (isCollection ? field.Values : field.Value);

                    result.Add(fieldName, value);
                }
            }

            return result;
        }
                
        protected virtual IProperty CreateProviderField(IndexDocumentField field)
        {
            var fieldType = field.Value?.GetType() ?? typeof(object);

            if (fieldType == typeof(string))
            {
                if (field.IsFilterable)
                    return new KeywordProperty();

                return new TextProperty();
            }
            if (typeof(IEntity).IsAssignableFrom(fieldType) || (fieldType.IsArray && typeof(IEntity).IsAssignableFrom(fieldType.GetElementType())))
            {
                return new NestedProperty();
            }
            switch (fieldType.Name)
            {
                case "Int32":
                case "UInt16":
                    return new NumberProperty(NumberType.Integer);
                case "Int16":
                case "Byte":
                    return new NumberProperty(NumberType.Short);
                case "SByte":
                    return new NumberProperty(NumberType.Byte);
                case "Int64":
                case "UInt32":
                case "TimeSpan":
                    return new NumberProperty(NumberType.Long);
                case "Single":
                    return new NumberProperty(NumberType.Float);
                case "Decimal":
                case "Double":
                case "UInt64":
                    return new NumberProperty(NumberType.Double);
                case "DateTime":
                case "DateTimeOffset":
                    return new DateProperty();
                case "Boolean":
                    return new BooleanProperty();
                case "Char":
                case "Guid":
                    return new KeywordProperty();
                case "GeoPoint":
                    return new GeoPointProperty();
            }

            throw new ArgumentException($"Field {field.Name} has unsupported type {fieldType}", nameof(field));
        }

        protected virtual void ConfigureProperty(IProperty property, IndexDocumentField field)
        {
            if (property != null && !(property is INestedProperty))
            {
                if (property is CorePropertyBase baseProperty)
                {
                    baseProperty.Store = field.IsRetrievable;
                }

                switch (property)
                {
                    case TextProperty textProperty:
                        ConfigureTextProperty(textProperty, field);
                        break;
                    case KeywordProperty keywordProperty:
                        ConfigureKeywordProperty(keywordProperty, field);
                        break;
                }
            }
            else if (property is NestedProperty nestedProperty)
            {
                //VP-6107: need to index all object with type 'Object' as 'Text' in NestedProperty
                //There are Properties.Values.Value in Category/Product
                var objects = field.Value.GetFullPropertyNamesFromObject<object>(deep: 7).Distinct().ToList();
                nestedProperty.Properties = new Properties(objects
                                .Select((v, i) => new { Key = new PropertyName(v.ToLower()), Value = new TextProperty() })
                                .ToDictionary(o => o.Key, o => (IProperty)o.Value));
            }
        }

        protected virtual void ConfigureKeywordProperty(KeywordProperty keywordProperty, IndexDocumentField field)
        {
            if (keywordProperty != null)
            {
                keywordProperty.Index = field.IsFilterable;
            }
        }

        protected virtual void ConfigureTextProperty(TextProperty textProperty, IndexDocumentField field)
        {
            if (textProperty != null)
            {
                textProperty.Index = field.IsSearchable;
                textProperty.Analyzer = field.IsSearchable ? SearchableFieldAnalyzerName : null;
            }
        }

        protected virtual async Task<Properties<IProperties>> GetMappingAsync(string indexName)
        {
            var properties = GetMappingFromCache(indexName);
            if (properties == null && await IndexExistsAsync(indexName))
            {
                var providerMapping = await Client.Indices.GetMappingAsync(new GetMappingRequest(indexName));
                var mapping = providerMapping.GetMappingFor(indexName);
                if (mapping != null)
                {
                    properties = new Properties<IProperties>(mapping.Properties);
                }
            }

            properties = properties ?? new Properties<IProperties>();
            AddMappingToCache(indexName, properties);
            return properties;
        }

        protected virtual async Task UpdateMappingAsync(string indexName, Properties<IProperties> properties)
        {
            var mappingRequest = new PutMappingRequest(indexName) { Properties = properties };
            var response = await Client.MapAsync(mappingRequest);

            if (!response.IsValid)
            {
                ThrowException("Failed to submit mapping. " + response.DebugInformation, response.OriginalException);
            }

            AddMappingToCache(indexName, properties);
            await Client.Indices.RefreshAsync(indexName);
        }

        protected virtual Properties<IProperties> GetMappingFromCache(string indexName)
        {
            return _mappings.ContainsKey(indexName) ? _mappings[indexName] : null;
        }

        protected virtual void AddMappingToCache(string indexName, Properties<IProperties> properties)
        {
            _mappings[indexName] = properties;
        }

        protected virtual void RemoveMappingFromCache(string indexName)
        {
            _mappings.TryRemove(indexName, out var _);
        }

        protected virtual string CreateCacheKey(params string[] parts)
        {
            return string.Join("/", parts);
        }

        protected virtual string[] ParseCacheKey(string key)
        {
            return key.Split('/');
        }

        protected virtual void ThrowException(string message, Exception innerException)
        {
            throw new SearchException($"{message}. URL:{ServerUrl}, Scope: {_searchOptions.Scope}", innerException);
        }

        protected virtual string GetIndexName(string documentType)
        {
            // Use different index for each document type
            return string.Join("-", _searchOptions.Scope, documentType).ToLowerInvariant();
        }

        protected virtual async Task<bool> IndexExistsAsync(string indexName)
        {
            var response = await Client.Indices.ExistsAsync(indexName);
            return response.Exists;
        }

        #region Create and configure index

        protected virtual async Task CreateIndexAsync(string indexName)
        {
            var response = await Client.Indices.CreateAsync(indexName, i => i.Settings(ConfigureIndexSettings));

            if (!response.IsValid)
            {
                ThrowException("Failed to create index. " + response.DebugInformation, response.OriginalException);
            }
        }

        protected virtual IndexSettingsDescriptor ConfigureIndexSettings(IndexSettingsDescriptor settings)
        {
            // https://www.elastic.co/guide/en/elasticsearch/reference/current/mapping.html#mapping-limit-settings
            var fieldsLimit = GetFieldsLimit();

            //new v7.3 index setting "max_ngram_diff"
            //https://www.elastic.co/guide/en/elasticsearch/reference/current/index-modules.html#index.max_ngram_diff
            var ngramDiff = GetMaxGram() - GetMinGram();

            return settings
                .Setting("index.mapping.total_fields.limit", fieldsLimit)
                .Setting("index.max_ngram_diff", ngramDiff)
                .Analysis(a => a
                    .TokenFilters(ConfigureTokenFilters)
                    .Analyzers(ConfigureAnalyzers));
        }

        protected virtual AnalyzersDescriptor ConfigureAnalyzers(AnalyzersDescriptor analyzers)
        {
            return analyzers
                .Custom(SearchableFieldAnalyzerName, ConfigureSearchableFieldAnalyzer);
        }

        protected virtual CustomAnalyzerDescriptor ConfigureSearchableFieldAnalyzer(CustomAnalyzerDescriptor customAnalyzer)
        {
            // Use ngrams analyzer for search in the middle of the word
            return customAnalyzer
                .Tokenizer("standard")
                .Filters("lowercase", GetTokenFilterName());
        }

        protected virtual TokenFiltersDescriptor ConfigureTokenFilters(TokenFiltersDescriptor tokenFilters)
        {
            return tokenFilters
                .NGram(NGramFilterName, ConfigureNGramFilter)
                .EdgeNGram(EdgeNGramFilterName, ConfigureEdgeNGramFilter);
        }

        protected virtual NGramTokenFilterDescriptor ConfigureNGramFilter(NGramTokenFilterDescriptor nGram)
        {
            return nGram.MinGram(GetMinGram()).MaxGram(GetMaxGram());
        }

        protected virtual EdgeNGramTokenFilterDescriptor ConfigureEdgeNGramFilter(EdgeNGramTokenFilterDescriptor edgeNGram)
        {
            return edgeNGram.MinGram(GetMinGram()).MaxGram(GetMaxGram());
        }

        protected virtual int GetFieldsLimit()
        {
            var fieldsLimit = _settingsManager.GetValue("VirtoCommerce.Search.ElasticSearch.IndexTotalFieldsLimit", 1000);
            return fieldsLimit;
        }

        protected virtual string GetTokenFilterName()
        {
            return _settingsManager.GetValue("VirtoCommerce.Search.ElasticSearch.TokenFilter", EdgeNGramFilterName);
        }

        protected virtual int GetMinGram()
        {
            return _settingsManager.GetValue("VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MinGram", 1);
        }

        protected virtual int GetMaxGram()
        {
            return _settingsManager.GetValue("VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MaxGram", 20);
        }

        #endregion

        protected static IConnectionSettingsValues GetConnectionSettings(ElasticSearchOptions options)
        {
            var serverUrl = GetServerUrl(options);
            var accessUser = options.User;
            var accessKey = options.Key;
            var pool = new SingleNodeConnectionPool(serverUrl);
            var connectionSettings = new ConnectionSettings(pool, sourceSerializer: JsonNetSerializer.Default);

            if (!string.IsNullOrEmpty(accessUser) && !string.IsNullOrEmpty(accessKey))
            {
                connectionSettings.BasicAuthentication(accessUser, accessKey);
            }
            else if (!string.IsNullOrEmpty(accessKey))
            {
                // elastic is default name for elastic cloud
                connectionSettings.BasicAuthentication("elastic", accessKey);
            }

            if (options.EnableHttpCompression.EqualsInvariant("true"))
            {
                connectionSettings.EnableHttpCompression();
            }

            return connectionSettings;
        }

        protected static Uri GetServerUrl(ElasticSearchOptions options)
        {
            var server = options.Server;

            if (string.IsNullOrEmpty(server))
            {
                throw new ArgumentException("'Server' parameter must not be empty");
            }

            if (!server.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                server = "http://" + server;
            }

            server = server.TrimEnd('/');
            return new Uri(server);
        }
    }
}
