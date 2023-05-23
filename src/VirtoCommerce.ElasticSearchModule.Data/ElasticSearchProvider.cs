using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    public class ElasticSearchProvider : ISearchProvider, ISupportIndexSwap, ISupportPartialUpdate, ISupportSuggestions
    {
        // prefixes for index aliases
        public const string ActiveIndexAlias = "active";
        public const string BackupIndexAlias = "backup";

        public const string SearchableFieldAnalyzerName = "searchable_field_analyzer";
        public const string NGramFilterName = "custom_ngram";
        public const string EdgeNGramFilterName = "custom_edge_ngram";

        private const string _exceptionTitle = "Elasticsearch Server";

        private readonly ConcurrentDictionary<string, IProperties> _mappings = new();
        private readonly SearchOptions _searchOptions;

        private readonly Regex _specialSymbols = new("[/+_=]", RegexOptions.Compiled);

        private readonly ILogger<ElasticSearchProvider> _logger;

        /// <summary>
        /// Added to a suggestable field to enable completion suggestion queries (IsSuggestable == true)
        /// </summary>
        private const string _completionSubFieldName = "completion";

        public ElasticSearchProvider(
            IOptions<SearchOptions> searchOptions,
            ISettingsManager settingsManager,
            IElasticClient client,
            ElasticSearchRequestBuilder requestBuilder,
            ILogger<ElasticSearchProvider> logger)
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
            _logger = logger;
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

            // swap start
            var activeIndexResponse = await Client.GetIndicesPointingToAliasAsync(activeIndexAlias);
            var activeIndexName = activeIndexResponse?.FirstOrDefault();
            if (string.IsNullOrEmpty(activeIndexName))
            {
                return;
            }

            var bulkAliasDescriptor = new BulkAliasDescriptor();

            bulkAliasDescriptor.Remove(x => x.Index(activeIndexName).Alias(activeIndexAlias));

            var backupIndexAlias = GetIndexAlias(BackupIndexAlias, documentType);
            var backupIndexResponse = await Client.GetIndicesPointingToAliasAsync(backupIndexAlias);
            var backupIndexName = backupIndexResponse?.FirstOrDefault();

            if (!string.IsNullOrEmpty(backupIndexName))
            {
                bulkAliasDescriptor.Remove(x => x.Index(backupIndexName).Alias(backupIndexAlias));
                bulkAliasDescriptor.Add(a => a.Index(backupIndexName).Alias(activeIndexAlias));
            }

            bulkAliasDescriptor.Add(a => a.Index(activeIndexName).Alias(backupIndexAlias));

            var swapResponse = await Client.Indices.BulkAliasAsync(bulkAliasDescriptor);

            if (!swapResponse.IsValid)
            {
                ThrowException($"Failed to swap indexes for the document type: {documentType}", swapResponse.OriginalException);
            }

            RemoveMappingFromCache(backupIndexAlias);
            RemoveMappingFromCache(activeIndexAlias);
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
                    if (!response.IsValid && response.ApiCall.HttpStatusCode != (int)HttpStatusCode.NotFound)
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
            try
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
            catch (SearchException ex)
            {
                _logger.LogError(ex, $"Error while putting an active alias on a default index at {nameof(AddActiveAlias)}. Possible fail on Elastic server side at IndexExists check.");
            }
        }

        public virtual async Task<SuggestionResponse> GetSuggestionsAsync(string documentType, SuggestionRequest request)
        {
            var alias = request.UseBackupIndex
                ? BackupIndexAlias
                : ActiveIndexAlias;
            var indexName = GetIndexAlias(alias, documentType);

            ISearchResponse<SearchDocument> providerResponse;

            var buckets = new Dictionary<string, ISuggestBucket>();

            var result = new SuggestionResponse();

            if (request.Fields.IsNullOrEmpty())
            {
                return result;
            }

            buckets = request.Fields.ToDictionary(x => x, x => (ISuggestBucket)new SuggestBucket
            {
                Text = request.Query,
                Completion = new CompletionSuggester
                {
                    // search completion by the special Completion type field, i.e. "name.completion"
                    Field = $"{x}.{_completionSubFieldName}",
                    Size = request.Size,
                    SkipDuplicates = true,
                }
            });

            try
            {
                var suggestRequest = new Nest.SearchRequest(indexName)
                {
                    Source = false,
                    Suggest = new SuggestContainer(buckets)
                };

                providerResponse = await Client.SearchAsync<SearchDocument>(suggestRequest);
            }
            catch (Exception ex)
            {
                throw new SearchException(ex.Message, ex);
            }

            if (!providerResponse.IsValid)
            {
                ThrowException(providerResponse.DebugInformation, null);
            }

            foreach (var field in request.Fields.Where(field => providerResponse.Suggest.ContainsKey(field)))
            {
                var options = providerResponse.Suggest[field].SelectMany(s => s.Options).Select(o => o.Text);
                result.Suggestions.AddRange(options);
            }

            if (result.Suggestions.Count > request.Size)
            {
                result.Suggestions = result.Suggestions.Take(request.Size).ToList();
            }

            return result;
        }

        protected virtual async Task<IndexingResult> InternalIndexAsync(string documentType, IList<IndexDocument> documents, IndexingParameters parameters)
        {
            var (indexName, providerDocuments) = await InternalCreateIndexAsync(documentType, documents, parameters);

            var bulkDescriptor = parameters.PartialUpdate
                ? new BulkDescriptor().Index(indexName).UpdateMany(providerDocuments, (descriptor, document) => descriptor.Doc(document))
                : new BulkDescriptor().Index(indexName).IndexMany(providerDocuments);

            var bulkResponse = await Client.BulkAsync(bulkDescriptor);
            await Client.Indices.RefreshAsync(indexName);

            var result = new IndexingResult { Items = new List<IndexingResultItem>() };

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

        protected virtual async Task<(string indexName, IList<SearchDocument> providerDocuments)> InternalCreateIndexAsync(
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
            var providerFields = new Properties<IProperties>(await GetMappingAsync(indexName)); // Make a copy
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

        protected virtual SearchDocument ConvertToProviderDocument(IndexDocument document, IProperties properties)
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
                    if (!properties.ContainsKey(fieldName))
                    {
                        // Create new property mapping
                        var providerField = CreateProviderFieldByType(field);
                        ConfigureProperty(providerField, field);
                        properties.Add(fieldName, providerField);
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

        protected virtual IProperty CreateProviderFieldByType(IndexDocumentField field)
        {
            return field.ValueType switch
            {
#pragma warning disable CS0618 // Type or member is obsolete
                IndexDocumentFieldValueType.Undefined => CreateProviderField(field),
#pragma warning restore CS0618 // Type or member is obsolete
                IndexDocumentFieldValueType.String when field.IsFilterable => new KeywordProperty(),
                IndexDocumentFieldValueType.String when !field.IsFilterable => new TextProperty(),
                IndexDocumentFieldValueType.Char => new KeywordProperty(),
                IndexDocumentFieldValueType.Guid => new KeywordProperty(),
                IndexDocumentFieldValueType.Complex => new NestedProperty(),
                IndexDocumentFieldValueType.Integer => new NumberProperty(NumberType.Integer),
                IndexDocumentFieldValueType.Short => new NumberProperty(NumberType.Short),
                IndexDocumentFieldValueType.Byte => new NumberProperty(NumberType.Byte),
                IndexDocumentFieldValueType.Long => new NumberProperty(NumberType.Long),
                IndexDocumentFieldValueType.Float => new NumberProperty(NumberType.Float),
                IndexDocumentFieldValueType.Decimal => new NumberProperty(NumberType.Double),
                IndexDocumentFieldValueType.Double => new NumberProperty(NumberType.Double),
                IndexDocumentFieldValueType.DateTime => new DateProperty(),
                IndexDocumentFieldValueType.Boolean => new BooleanProperty(),
                IndexDocumentFieldValueType.GeoPoint => new GeoPointProperty(),
                _ => throw new ArgumentException($"Field '{field.Name}' has unsupported type '{field.ValueType}'", nameof(field))
            };
        }

        [Obsolete("Left for backward compatibility.")]
        protected virtual IProperty CreateProviderField(IndexDocumentField field)
        {
            if (field.Value == null)
            {
                throw new ArgumentException($"Field '{field.Name}' has no value", nameof(field));
            }

            var fieldType = field.Value.GetType();

            if (IsComplexType(fieldType))
            {
                return new NestedProperty();
            }

            return fieldType.Name switch
            {
                "String" => field.IsFilterable ? new KeywordProperty() : new TextProperty(),
                "Int32" => new NumberProperty(NumberType.Integer),
                "UInt16" => new NumberProperty(NumberType.Integer),
                "Int16" => new NumberProperty(NumberType.Short),
                "Byte" => new NumberProperty(NumberType.Short),
                "SByte" => new NumberProperty(NumberType.Byte),
                "Int64" => new NumberProperty(NumberType.Long),
                "UInt32" => new NumberProperty(NumberType.Long),
                "TimeSpan" => new NumberProperty(NumberType.Long),
                "Single" => new NumberProperty(NumberType.Float),
                "Decimal" => new NumberProperty(NumberType.Double),
                "Double" => new NumberProperty(NumberType.Double),
                "UInt64" => new NumberProperty(NumberType.Double),
                "DateTime" => new DateProperty(),
                "DateTimeOffset" => new DateProperty(),
                "Boolean" => new BooleanProperty(),
                "Char" => new KeywordProperty(),
                "Guid" => new KeywordProperty(),
                "GeoPoint" => new GeoPointProperty(),
                _ => throw new ArgumentException($"Field '{field.Name}' has unsupported type '{fieldType}'", nameof(field))
            };
        }

        private static bool IsComplexType(Type type)
        {
            return
                type.IsAssignableTo(typeof(IEntity)) ||
                type.IsAssignableTo(typeof(IEnumerable<IEntity>));
        }

        protected virtual void ConfigureProperty(IProperty property, IndexDocumentField field)
        {
            if (property != null && property is not INestedProperty)
            {
                switch (property)
                {
                    case TextProperty textProperty:
                        ConfigureTextProperty(textProperty, field);
                        break;
                    case KeywordProperty keywordProperty:
                        ConfigureKeywordProperty(keywordProperty, field);
                        break;
                }

                if (property is CorePropertyBase baseProperty)
                {
                    baseProperty.Store = field.IsRetrievable;

                    // Add completion field
                    if (field.IsSuggestable)
                    {
                        baseProperty.Fields.Add(new PropertyName(_completionSubFieldName), new CompletionProperty()
                        {
                            Name = field.Name,
                            MaxInputLength = 256,
                        });
                    }
                }
            }
            else if (property is INestedProperty nestedProperty)
            {
                //VP-6107: need to index all objects with type 'Object' as 'Text'
                //There are Properties.Values.Value in Category/Product
                var objects = field.Value.GetPropertyNames<object>(deep: 7);
                nestedProperty.Properties = new Properties(objects.ToDictionary(x => new PropertyName(x), _ => (IProperty)new TextProperty()));
            }
        }

        protected virtual void ConfigureKeywordProperty(KeywordProperty keywordProperty, IndexDocumentField field)
        {
            if (keywordProperty != null)
            {
                keywordProperty.Index = field.IsFilterable;
                keywordProperty.Normalizer = "lowercase";

                keywordProperty.Fields = new Properties
                {
                    { "raw", new KeywordProperty() },
                };
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

        protected virtual async Task<IProperties> GetMappingAsync(string indexName)
        {
            if (GetMappingFromCache(indexName, out var properties))
            {
                return properties;
            }

            if (await IndexExistsAsync(indexName))
            {
                properties = await LoadMappingAsync(indexName);
            }

            properties ??= new Properties<IProperties>();
            AddMappingToCache(indexName, properties);

            return properties;
        }

        protected virtual async Task UpdateMappingAsync(string indexName, IProperties properties)
        {
            IProperties newProperties;
            IProperties allProperties;

            var existingProperties = await LoadMappingAsync(indexName);

            if (existingProperties == null)
            {
                newProperties = properties;
                allProperties = properties;
            }
            else
            {
                newProperties = new Properties<IProperties>();
                allProperties = existingProperties;

                foreach (var (name, value) in properties)
                {
                    if (!existingProperties.ContainsKey(name))
                    {
                        newProperties.Add(name, value);
                        allProperties.Add(name, value);
                    }
                }
            }

            if (newProperties.Any())
            {
                var request = new PutMappingRequest(indexName) { Properties = newProperties };
                var response = await Client.MapAsync(request);

                if (!response.IsValid)
                {
                    ThrowException("Failed to submit mapping. " + response.DebugInformation, response.OriginalException);
                }
            }

            AddMappingToCache(indexName, allProperties);
            await Client.Indices.RefreshAsync(indexName);
        }

        protected virtual async Task<IProperties> LoadMappingAsync(string indexName)
        {
            var mappingResponse = await Client.Indices.GetMappingAsync(new GetMappingRequest(indexName));

            var mapping = mappingResponse.GetMappingFor(indexName) ??
                          mappingResponse.Indices.Values.FirstOrDefault()?.Mappings;

            return mapping?.Properties;
        }

        protected virtual bool GetMappingFromCache(string indexName, out IProperties properties)
        {
            return _mappings.TryGetValue(indexName, out properties);
        }

        protected virtual void AddMappingToCache(string indexName, IProperties properties)
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
            if (response.ApiCall?.Success == false)
            {
                ThrowException($"Index check call failed for index: {indexName}", response.OriginalException);
            }

            return response.Exists;
        }

        protected virtual bool IndexExists(string indexName)
        {
            var response = Client.Indices.Exists(indexName);
            if (response.ApiCall?.Success == false)
            {
                ThrowException($"Index check call failed for index: {indexName}", response.OriginalException);
            }

            return response.Exists;
        }

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
                        .Custom("lowercase", cn => cn
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
