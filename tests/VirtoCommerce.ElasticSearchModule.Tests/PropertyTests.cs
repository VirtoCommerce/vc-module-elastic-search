using System;
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

            var res = obj.GetFullPropertyNamesFromObject<object>(6).ToArray();

            Assert.Equal(new[] { "Properties.Values.Value", "Properties.ValueInProperty.Value" }, res);
        }
    }
}
