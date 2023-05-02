using System;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public ElasticSearchTests(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override ISearchProvider GetSearchProvider()
        {
            var searchOptions = Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch" });
            var elasticOptions = Options.Create(_configuration.GetSection("ElasticSearch").Get<ElasticSearchOptions>());
            elasticOptions.Value.Server ??= Environment.GetEnvironmentVariable("TestElasticsearchHost") ?? "localhost:9200";
            var connectionSettings = new ElasticSearchConnectionSettings(elasticOptions);
            var client = new ElasticSearchClient(connectionSettings);

            var provider = new ElasticSearchProvider(searchOptions, GetSettingsManager(), client, new ElasticSearchRequestBuilder());

            return provider;
        }
    }
}
