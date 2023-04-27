using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    [Trait("Category", "CI")]
    [Trait("Category", "IntegrationTest")]
    public class ElasticSearchTests : SearchProviderTests
    {
        protected override ISearchProvider GetSearchProvider()
        {
            var host = Environment.GetEnvironmentVariable("TestElasticsearchHost") ?? "localhost:9200";
            var searchOptions = Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch" });
            var elasticOptions = Options.Create(new ElasticSearchOptions { Server = host });
            var connectionSettings = new ElasticSearchConnectionSettings(elasticOptions);
            var client = new ElasticSearchClient(connectionSettings);
            var loggerFactory = LoggerFactory.Create(bulder => { bulder.ClearProviders(); });
            var logger = loggerFactory.CreateLogger<ElasticSearchProvider>();

            var provider = new ElasticSearchProvider(searchOptions, GetSettingsManager(), client, new ElasticSearchRequestBuilder(), logger);

            return provider;
        }
    }
}
