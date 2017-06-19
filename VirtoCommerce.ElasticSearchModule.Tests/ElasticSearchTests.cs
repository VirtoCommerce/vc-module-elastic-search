using System;
using VirtoCommerce.CoreModule.Search.Tests;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.ElasticSearchModule.Data;
using Xunit;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    [Trait("Category", "CI")]
    public class ElasticSearchTests : SearchProviderTests
    {
        protected override ISearchProvider GetSearchProvider()
        {
            var host = Environment.GetEnvironmentVariable("TestElasticsearchHost") ?? "localhost:9200";
            var provider = new ElasticSearchProvider(new SearchConnection($"server=http://{host};scope=test"), GetSettingsManager());
            return provider;
        }
    }
}
