using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    [Trait("Category", "Unit")]
    public class PropertyConverterTests : SearchProviderTestsBase
    {
        public static IEnumerable<object[]> TestData
        {
            get
            {
                var entity = new TestEntity();
                var entities = new[] { entity };

                yield return new object[] { "entity", entity };
                yield return new object[] { "array", entities };
                yield return new object[] { "list", entities.ToList() };
                yield return new object[] { "enumerable", entities.Select(x => x) };
            }
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void CanConvertEntityToNestedProperty(string name, object value)
        {
            var provider = GetTestElasticsearchProvider();
            var result = provider.CreateProviderFieldByType(new IndexDocumentField(name, value, IndexDocumentFieldValueType.Complex));
            Assert.IsType<NestedProperty>(result);
        }

        protected override ISearchProvider GetSearchProvider()
        {
            return GetTestElasticsearchProvider();
        }

        protected TestElasticsearchProvider GetTestElasticsearchProvider()
        {
            var searchOptions = Options.Create(new SearchOptions());
            var elasticOptions = Options.Create(new ElasticSearchOptions { Server = "non-empty-string" });
            var connectionSettings = new ElasticSearchConnectionSettings(elasticOptions);
            var client = new ElasticSearchClient(connectionSettings);
            var loggerFactory = LoggerFactory.Create(builder => { builder.ClearProviders(); });
            var logger = loggerFactory.CreateLogger<TestElasticsearchProvider>();

            var provider = new TestElasticsearchProvider(searchOptions, GetSettingsManager(), client, new ElasticSearchRequestBuilder(), logger);

            return provider;
        }

        public class TestEntity : Entity
        {
        }

        public class TestElasticsearchProvider : ElasticSearchProvider
        {
            public TestElasticsearchProvider(
                IOptions<SearchOptions> searchOptions,
                ISettingsManager settingsManager,
                IElasticClient client,
                ElasticSearchRequestBuilder requestBuilder,
                ILogger<TestElasticsearchProvider> logger)
                : base(searchOptions, settingsManager, client, requestBuilder, logger)
            {
            }

            public new IProperty CreateProviderFieldByType(IndexDocumentField field)
            {
                return base.CreateProviderFieldByType(field);
            }
        }
    }
}
