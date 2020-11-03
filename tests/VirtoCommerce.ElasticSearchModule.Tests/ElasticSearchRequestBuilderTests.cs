using System;
using System.Collections.Generic;
using AutoFixture;
using FluentAssertions;
using Nest;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    [Trait("Category", "CI")]
    public class ElasticSearchRequestBuilderTests
    {
        private readonly ElasticSearchRequestBuilderTestProxy _testClass = new ElasticSearchRequestBuilderTestProxy();
        private readonly Fixture _fixture = new Fixture();

        [Theory]
        [InlineData("0", "false")]
        [InlineData("1", "true")]
        [InlineData("true", "true")]
        [InlineData("false", "false")]
        [InlineData("tRuE", "true")]
        [InlineData("FaLsE", "false")]
        public void CreateTermFilter_BooleanAggregate_ShouldCreateCorrectValues(string value, string convertedValue)
        {
            // Arrange
            var fieldName = _fixture.Create<string>();

            var termFilter = new TermFilter
            {
                Values = new[] { value },
                FieldName = fieldName
            };

            var availableFields = new Properties<IProperties>(new Dictionary<PropertyName, IProperty>
            {
                { fieldName, new BooleanPropertyTestProxy() }
            });

            // Act
            var result = _testClass.CreateTermFilterProxy(termFilter, availableFields) as IQueryContainer;

            // Assert
            result.Terms.Terms.Should().Contain(convertedValue);
        }
    }

    public class ElasticSearchRequestBuilderTestProxy : ElasticSearchRequestBuilder
    {
        public QueryContainer CreateTermFilterProxy(TermFilter termFilter, Properties<IProperties> availableFields)
        {
            return base.CreateTermFilter(termFilter, availableFields);
        }
    }

    public class BooleanPropertyTestProxy : IProperty
    {
        public IDictionary<string, object> LocalMetadata { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IDictionary<string, string> Meta { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public PropertyName Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Type { get => "boolean"; set => throw new NotImplementedException(); }
    }
}
