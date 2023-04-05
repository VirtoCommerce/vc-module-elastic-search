using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Nest;
using VirtoCommerce.ElasticSearchModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using IndexingSettings = VirtoCommerce.ElasticSearchModule.Data.ModuleConstants.Settings.Indexing;
using SearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;

namespace VirtoCommerce.ElasticSearchModule.Data
{
    public class ElasticSearchProvider : ISearchProvider, ISupportIndexSwap, ISupportPartialUpdate, ISupportIndexCreate
    {
        // prefixes for index aliases
        public const string ActiveIndexAlias = "active";
        public const string BackupIndexAlias = "backup";

        public const string SearchableFieldAnalyzerName = "searchable_field_analyzer";
        public const string NGramFilterName = "custom_ngram";
        public const string EdgeNGramFilterName = "custom_edge_ngram";

        private const string _exceptionTitle = "Elasticsearch Server";

        private readonly ConcurrentDictionary<string, Properties<IProperties>> _mappings = new();
        private readonly SearchOptions _searchOptions;

        private readonly Regex _specialSymbols = new("[/+_=]", RegexOptions.Compiled);
        private readonly object _propertiesLockObject = new();

        public ElasticSearchProvider(
            IOptions<SearchOptions> searchOptions,
            ISettingsManager settingsManager,
            IElasticClient client,
            ElasticSearchRequestBuilder requestBuilder)
        {
            if (searchOptions == null)
            {
                throw new ArgumentNullException(nameof(searchOptions));
            }

            SettingsManager = settingsManager;
            Client = client;
            RequestBuilder = requestBuilder;
            ServerUrl = Client.ConnectionSettings.ConnectionPool.Nodes.First().Uri;
            _searchOptions = searchOptions.Value;
        }

        protected IElasticClient Client { get; }
        protected ElasticSearchRequestBuilder RequestBuilder { get; }
        protected Uri ServerUrl { get; }
        protected ISettingsManager SettingsManager { get; }

        /// <summary>
        /// Swap active and backup indexes
        /// </summary>
        public virtual async Task SwapIndexAsync(string documentType)
        {
            if (string.IsNullOrEmpty(documentType))
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            try
            {
                // get active index and alias
                var activeIndexAlias = GetIndexAlias(ActiveIndexAlias, documentType);

                // if no active index found - check that default (active) index, if not create, if does assign the alias to it
                var indexExists = await IndexExistsAsync(activeIndexAlias);
                if (!indexExists)
                {
                    var indexName = GetIndexName(documentType);
                    var indexExits = await IndexExistsAsync(indexName);
                    if (!indexExits)
                    {
                        // create new index with alias
                        await CreateIndexAsync(indexName, activeIndexAlias);
                    }
                    else
                    {
                        // attach alias to default index
                        await Client.Indices.PutAliasAsync(indexName, activeIndexAlias);
                    }

                }

                var activeIndexResponse = await Client.Indices.GetAliasAsync(activeIndexAlias);
                var activeIndexName = activeIndexResponse.Indices.FirstOrDefault().Key;

                // get backup index and alias
                var backupIndexAlias = GetIndexAlias(BackupIndexAlias, documentType);
                // swap
                await Client.Indices.DeleteAliasAsync(activeIndexName, activeIndexAlias);

                if (await IndexExistsAsync(backupIndexAlias))
                {
                    var backupIndexResponse = await Client.Indices.GetAliasAsync(backupIndexAlias);
                    var backupIndexName = backupIndexResponse.Indices.FirstOrDefault().Key;

                    await Client.Indices.DeleteAliasAsync(backupIndexName, backupIndexAlias);
                    await Client.Indices.PutAliasAsync(backupIndexName, activeIndexAlias);
                }

                await Client.Indices.PutAliasAsync(activeIndexName, backupIndexAlias);
            }
            catch (Exception ex)
            {
                ThrowException("Failed to swap indexes", ex);
            }
        }

        public async Task CreateIndexAsync(string documentType, IndexDocument schema)
        {
            await InternalCreateIndexAsync(documentType, new[] { schema }, new IndexingParameters { Reindex = true });
        }

        public virtual async Task<IndexingResult> IndexWithBackupAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = await InternalIndexAsync(documentType, documents, new IndexingParameters { Reindex = true });

            return result;
        }

        public virtual async Task<IndexingResult> IndexPartialAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = await InternalIndexAsync(documentType, documents, new IndexingParameters { PartialUpdate = true });

            return result;
        }

        /// <summary>
        /// Delete backup index
        /// </summary>
        public virtual async Task DeleteIndexAsync(string documentType)
        {
            if (string.IsNullOrEmpty(documentType))
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            try
            {
                //get backup index by alias and delete if present
                var indexAlias = GetIndexAlias(BackupIndexAlias, documentType);
                var indexResponse = await Client.Indices.GetAliasAsync(indexAlias);
                var indexName = indexResponse.Indices.FirstOrDefault().Key;

                if (indexName != null)
                {
                    var response = await Client.Indices.DeleteAsync(indexName);
                    if (!response.IsValid && response.ApiCall.HttpStatusCode != 404)
                    {
                        throw new SearchException(response.DebugInformation);
                    }
                }

                RemoveMappingFromCache(indexAlias);
            }
            catch (Exception ex)
            {
                ThrowException("Failed to delete index", ex);
            }
        }

        public virtual async Task<IndexingResult> IndexAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = await InternalIndexAsync(documentType, documents, new IndexingParameters());

            return result;
        }

        public virtual async Task<IndexingResult> RemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            var providerDocuments = documents.Select(d => new SearchDocument { Id = d.Id }).ToArray();
            var indexName = GetIndexAlias(ActiveIndexAlias, documentType);
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
            var alias = request.UseBackupIndex
                ? BackupIndexAlias
                : ActiveIndexAlias;
            var indexName = GetIndexAlias(alias, documentType);

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

        /// <summary>
        /// Puts an active alias on a default index (if exists)
        /// </summary>
        public void AddActiveAlias(IEnumerable<string> documentTypes)
        {
            foreach (var documentType in documentTypes)
            {
                var indexAlias = GetIndexAlias(ActiveIndexAlias, documentType);
                if (IndexExists(indexAlias))
                {
                    continue;
                }

                var indexName = GetIndexName(documentType);
                if (IndexExists(indexName))
                {
                    Client.Indices.PutAlias(indexName, indexAlias);
                }
            }
        }

        protected virtual async Task<IndexingResult> InternalIndexAsync(string documentType, IList<IndexDocument> documents, IndexingParameters parameters)
        {
            var (indexName, providerDocuments) = await InternalCreateIndexAsync(documentType, documents, parameters);

            var bulkDescriptor = parameters.PartialUpdate
                ? new BulkDescriptor().Index(indexName).UpdateMany(providerDocuments, (descriptor, document) => descriptor.Doc(document))
                : new BulkDescriptor().Index(indexName).IndexMany(providerDocuments);

            var bulkResponse = await Client.BulkAsync(bulkDescriptor);
            await Client.Indices.RefreshAsync(indexName);

            var result = new IndexingResult();

            if (!bulkResponse.IsValid)
            {
                result.Items.Add(new IndexingResultItem
                {
                    Id = _exceptionTitle,
                    ErrorMessage = bulkResponse.OriginalException?.Message,
                    Succeeded = false
                });
            }

            result.Items.AddRange(bulkResponse.Items.Select(i => new IndexingResultItem
            {
                Id = i.Id,
                Succeeded = i.IsValid,
                ErrorMessage = i.Error?.Reason
            }));

            return result;
        }

        protected virtual async Task<(string indexName, List<SearchDocument> providerDocuments)> InternalCreateIndexAsync(
            string documentType,
            IList<IndexDocument> documents,
            IndexingParameters parameters)
        {
            await DeleteDuplicateIndexes(documentType);

            // Use backup index in case of reindexing
            var alias = parameters.Reindex
                ? BackupIndexAlias
                : ActiveIndexAlias;

            var indexName = GetIndexAlias(alias, documentType);
            var providerFields = await GetMappingAsync(indexName);
            var oldFieldsCount = providerFields.Count();
            var providerDocuments = documents.Select(document => ConvertToProviderDocument(document, providerFields)).ToList();
            var updateMapping = !parameters.PartialUpdate && providerFields.Count() != oldFieldsCount;
            var indexExists = await IndexExistsAsync(indexName);

            if (!indexExists)
            {
                var newIndexName = GetIndexName(documentType, GetRandomIndexSuffix());
                await CreateIndexAsync(newIndexName, alias: indexName);
            }

            if (!indexExists || updateMapping)
            {
                await UpdateMappingAsync(indexName, providerFields);
            }

            return (indexName, providerDocuments);
        }

        protected virtual async Task DeleteDuplicateIndexes(string documentType)
        {
            if (!await SettingsManager.GetValueByDescriptorAsync<bool>(IndexingSettings.DeleteDuplicateIndexes))
            {
                return;
            }

            var activeIndexAlias = GetIndexAlias(ActiveIndexAlias, documentType);
            var activeIndexResponse = await Client.Indices.GetAliasAsync(activeIndexAlias);
            if (activeIndexResponse.Indices.Count > 1)
            {
                var indexNames = activeIndexResponse.Indices.Keys.Select(x => x.Name).ToList();
                var indices = string.Join(',', indexNames);

                var request = new SearchRequest
                {
                    Sorting = new[] { new SortingField { FieldName = KnownDocumentFields.IndexationDate, IsDescending = true } },
                    Take = 1,
                };

                var providerRequest = RequestBuilder.BuildRequest(request, indices, new Properties<IProperties>());
                var providerResponse = await Client.SearchAsync<SearchDocument>(providerRequest);

                var latestIndexName = providerResponse.Hits.FirstOrDefault()?.Index;
                if (!string.IsNullOrEmpty(latestIndexName))
                {
                    var indexesToDelete = string.Join(',', indexNames.Where(x => x != latestIndexName));
                    await Client.Indices.DeleteAsync(indexesToDelete);
                }
            }
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
                        // Scalable indexation can be multi-threaded
                        lock (_propertiesLockObject)
                        {
                            if (!dictionary.ContainsKey(fieldName))
                            {
                                // Create new property mapping
#pragma warning disable CS0618 // Type or member is obsolete
                                var providerField = field.ValueType == IndexDocumentFieldValueType.Undefined
                                    ? CreateProviderField(field)
                                    : CreateProviderFieldByType(field);
#pragma warning restore CS0618 // Type or member is obsolete

                                ConfigureProperty(providerField, field);
                                properties.Add(fieldName, providerField);
                            }
                        }
                    }

                    var isCollection = field.IsCollection || field.Values.Count > 1;
                    object value;

                    if (field.Value is GeoPoint point)
                    {
                        value = isCollection
                            ? field.Values.Select(v => ((GeoPoint)v).ToElasticValue()).ToArray()
                            : point.ToElasticValue();
                    }
                    else
                    {
                        value = isCollection
                            ? field.Values
                            : field.Value;
                    }

                    result.Add(fieldName, value);
                }
            }

            return result;
        }

        [Obsolete("Left for backward compatibility.")]
        protected virtual IProperty CreateProviderField(IndexDocumentField field)
        {
            var fieldType = field.Value?.GetType() ?? typeof(object);

            if (fieldType == typeof(string))
            {
                return field.IsFilterable
                    ? new KeywordProperty()
                    : new TextProperty();
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

        protected virtual IProperty CreateProviderFieldByType(IndexDocumentField field)
        {
            switch (field.ValueType)
            {
                case IndexDocumentFieldValueType.String when field.IsFilterable:
                    return new KeywordProperty();
                case IndexDocumentFieldValueType.String when !field.IsFilterable:
                    return new TextProperty();
                case IndexDocumentFieldValueType.Char:
                case IndexDocumentFieldValueType.Guid:
                    return new KeywordProperty();
                case IndexDocumentFieldValueType.Complex:
                    return new NestedProperty();
                case IndexDocumentFieldValueType.Integer:
                    return new NumberProperty(NumberType.Integer);
                case IndexDocumentFieldValueType.Short:
                    return new NumberProperty(NumberType.Short);
                case IndexDocumentFieldValueType.Byte:
                    return new NumberProperty(NumberType.Byte);
                case IndexDocumentFieldValueType.Long:
                    return new NumberProperty(NumberType.Long);
                case IndexDocumentFieldValueType.Float:
                    return new NumberProperty(NumberType.Float);
                case IndexDocumentFieldValueType.Decimal:
                    return new NumberProperty(NumberType.Double);
                case IndexDocumentFieldValueType.Double:
                    return new NumberProperty(NumberType.Double);
                case IndexDocumentFieldValueType.DateTime:
                    return new DateProperty();
                case IndexDocumentFieldValueType.Boolean:
                    return new BooleanProperty();
                case IndexDocumentFieldValueType.GeoPoint:
                    return new GeoPointProperty();
                default:
                    throw new ArgumentException($"Field {field.Name} has unsupported type {field.ValueType}", nameof(field));
            }
        }

        protected virtual void ConfigureProperty(IProperty property, IndexDocumentField field)
        {
            if (property != null && property is not INestedProperty)
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
                //VP-6107: need to index all objects with type 'Object' as 'Text'
                //There are Properties.Values.Value in Category/Product
                var objects = field.Value.GetPropertyNames<object>(deep: 7).Distinct().ToList();
                nestedProperty.Properties = new Properties(objects
                                .Select(v => new { Key = new PropertyName(v), Value = new TextProperty() })
                                .ToDictionary(o => o.Key, o => (IProperty)o.Value));
            }
        }

        protected virtual void ConfigureKeywordProperty(KeywordProperty keywordProperty, IndexDocumentField field)
        {
            if (keywordProperty != null)
            {
                keywordProperty.Index = field.IsFilterable;
                keywordProperty.Normalizer = "case_insensitive";
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

            properties ??= new Properties<IProperties>();
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
            return _mappings.TryGetValue(indexName, out var properties) ? properties : null;
        }

        protected virtual void AddMappingToCache(string indexName, Properties<IProperties> properties)
        {
            _mappings[indexName] = properties;
        }

        protected virtual void RemoveMappingFromCache(string indexName)
        {
            _mappings.TryRemove(indexName, out _);
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
            return string.Join("-", _searchOptions.GetScope(documentType), documentType).ToLowerInvariant();
        }

        protected virtual string GetIndexName(string documentType, string suffix)
        {
            return string.Join("-", _searchOptions.GetScope(documentType), documentType, suffix).ToLowerInvariant();
        }

        /// <summary>
        /// combine default index name and alias
        /// </summary>
        protected virtual string GetIndexAlias(string alias, string documentType)
        {
            return string.Join("-", GetIndexName(documentType), alias).ToLowerInvariant();
        }

        protected virtual async Task<bool> IndexExistsAsync(string indexName)
        {
            var response = await Client.Indices.ExistsAsync(indexName);
            return response.Exists;
        }

        protected virtual bool IndexExists(string indexName)
        {
            var response = Client.Indices.Exists(indexName);
            return response.Exists;
        }

        #region Create and configure index

        /// <summary>
        /// Creates an index with assigned alias
        /// </summary>
        protected virtual async Task CreateIndexAsync(string indexName, string alias)
        {
            var response = await Client.Indices.CreateAsync(indexName, i => i.Settings(ConfigureIndexSettings).Aliases(x => ConfigureAliases(x, alias)));

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
                    .Analyzers(ConfigureAnalyzers)
                        .Normalizers(n => n
                            .Custom("case_insensitive", cn => cn
                                .Filters("lowercase"))));
        }

        protected virtual AliasesDescriptor ConfigureAliases(AliasesDescriptor aliases, string alias)
        {
            return aliases.Alias(alias);
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
            return SettingsManager.GetValueByDescriptor<int>(IndexingSettings.IndexTotalFieldsLimit);
        }

        protected virtual string GetTokenFilterName()
        {
            return SettingsManager.GetValueByDescriptor<string>(IndexingSettings.TokenFilter);
        }

        protected virtual int GetMinGram()
        {
            return SettingsManager.GetValueByDescriptor<int>(IndexingSettings.MinGram);
        }

        protected virtual int GetMaxGram()
        {
            return SettingsManager.GetValueByDescriptor<int>(IndexingSettings.MaxGram);
        }

        #endregion

        /// <summary>
        /// Gets random name suffix to attach to index (for automatic creation of backup indices)
        /// </summary>
        protected string GetRandomIndexSuffix()
        {
            var result = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..10];
            result = _specialSymbols.Replace(result, string.Empty);

            return result;
        }
    }
}
