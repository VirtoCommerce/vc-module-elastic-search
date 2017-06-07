using System;
using Moq;
using VirtoCommerce.CoreModule.Search.Tests;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.Platform.Core.Settings;
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

        protected virtual ISettingsManager GetSettingsManager()
        {
            var mock = new Mock<ISettingsManager>();
            mock.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<int>())).Returns((string name, int defaultValue) => defaultValue);
            return mock.Object;
        }
    }
}
