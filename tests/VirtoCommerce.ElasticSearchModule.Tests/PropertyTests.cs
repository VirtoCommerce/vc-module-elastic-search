using System.Linq;
using VirtoCommerce.ElasticSearchModule.Data.Extensions;
using Xunit;
using static VirtoCommerce.ElasticSearchModule.Tests.SearchProviderTestsBase;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    public class PropertyTests
    {
        [Fact]
        public void GetPropertyNames_GetAllNamesFromAnObjectInDeepSeven()
        {
            var objects = new[] { new TestObjectValue(true, "Boolean"), new TestObjectValue(99.99m, "Number") };

            var res = objects.SelectMany(o => o.GetPropertyNames<object>(7)).Distinct().ToArray();

            Assert.Equal(
                new[]
                {
                    "testProperties.values.value",
                    "testProperties.valueInProperty.value",
                    "testProperties.value"
                }, res);
        }
    }
}
