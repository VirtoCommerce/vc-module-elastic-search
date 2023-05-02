using FluentAssertions;
using VirtoCommerce.ElasticSearchModule.Data.Extensions;
using Xunit;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    [Trait("Category", "Unit")]
    public class PropertyTests
    {
        [Fact]
        public void GetPropertyNames_GetAllNamesFromAnObjectInDeepSeven()
        {
            var obj = new SearchProviderTestsBase.TestObjectValue();
            obj.AddProperty(true, "Boolean");
            obj.AddProperty(99.99m, "Number");

            var names1 = obj.GetPropertyNames<object>(7);

            names1.Should().BeEquivalentTo(
                "testProperties.array.value",
                "testProperties.list.value",
                "testProperties.valueInProperty.value",
                "testProperties.value"
            );


            obj.Value = new SearchProviderTestsBase.PropertyValue { Value = 99.99m, ValueType = "Number" };

            var names2 = obj.GetPropertyNames<object>(7);

            // Should return one more name from the entity that was null earlier
            names2.Should().BeEquivalentTo(
                "testProperties.array.value",
                "testProperties.list.value",
                "testProperties.valueInProperty.value",
                "testProperties.value",
                "value.value"
            );
        }
    }
}
