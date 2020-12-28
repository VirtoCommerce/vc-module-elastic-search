using System.Linq;
using VirtoCommerce.ElasticSearchModule.Data.Extensions;
using Xunit;
using static VirtoCommerce.ElasticSearchModule.Tests.SearchProviderTestsBase;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    public class PropertyTests
    {
        [Fact]
        public void GetFullPropertyNamesFromObject_GetAllNamesInDeepSix()
        {
            var obj = new TestObjectValue(true, "Boolean");

            var res = obj.GetFullPropertyNames<object>(7).ToArray();
            var paths = PropertyExtensions._path;

            Assert.Equal(
                new[]
                {
                    "properties.values.value",
                    "properties.valueInProperty.value",
                    "properties.values.property.values.property.valueInProperty.value"
                }, res);
        }
    }
}
