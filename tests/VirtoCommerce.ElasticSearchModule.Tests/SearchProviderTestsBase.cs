using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.ElasticSearchModule.Tests
{
    public abstract class SearchProviderTestsBase
    {
        protected abstract ISearchProvider GetSearchProvider();

        protected virtual IList<IndexDocument> GetPrimaryDocuments()
        {
            return new List<IndexDocument>
            {
                CreateDocument("Item-1", "Sample Product", "Red", "2017-04-28T15:24:31.180Z", 2, "0,0", null, null, new TestObjectValue(true, "Boolean"), new Price("USD", "default", 123.23m)),
                CreateDocument("Item-2", "Red Shirt 2", "Red", "2017-04-27T15:24:31.180Z", 4, "0,10", null, null, new TestObjectValue("string", "ShortText"), new Price("USD", "default", 200m), new Price("USD", "sale", 99m), new Price("EUR", "sale", 300m)),
                CreateDocument("Item-3", "Red Shirt", "Red", "2017-04-26T15:24:31.180Z", 3, "0,20", null, null, new TestObjectValue(true, "Boolean"), new Price("USD", "default", 10m)),
                CreateDocument("Item-4", "black Sox", "Black", "2017-04-25T15:24:31.180Z", 10, "0,30", null, null, new TestObjectValue(99.99m, "Number"), new Price("USD", "default", 243.12m), new Price("USD", "super-sale", 89m)),
                CreateDocument("Item-5", "Black Sox2", "Silver", "2017-04-24T15:24:31.180Z", 20, "0,40", null, null, new TestObjectValue(new DateTime(2020, 12, 17, 0, 0, 0), "DateTime"), new Price("USD", "default", 700m)),
            };
        }

        protected virtual IList<IndexDocument> GetSecondaryDocuments()
        {
            return new List<IndexDocument>
            {
                CreateDocument("Item-6", "Blue Shirt", "Blue", "2017-04-23T15:24:31.180Z", 10, "0,50", "Blue Shirt 2", DateTime.UtcNow, new Price("USD", "default", 23.12m)),

                // The following documents will be deleted by test
                CreateDocument("Item-7", "Blue Shirt", "Blue", "2017-04-23T15:24:31.180Z", 10, "0,50", "Blue Shirt 2", DateTime.UtcNow, new Price("USD", "default", 23.12m)),
                CreateDocument("Item-8", "Blue Shirt", "Blue", "2017-04-23T15:24:31.180Z", 10, "0,50", "Blue Shirt 2", DateTime.UtcNow, new Price("USD", "default", 23.12m)),
            };
        }

        protected virtual IndexDocument CreateDocument(string id, string name, string color, string date, int size, string location, string name2, DateTime? date2, object obj, params Price[] prices)
        {
            var doc = new IndexDocument(id);

            doc.AddFilterableStringAndContentString("Name", name);
            doc.AddFilterableStringAndContentString("Color", color);

            doc.AddFilterableString("Code", id);
            doc.AddFilterableInteger("Size", size);
            doc.AddFilterableDateTime("Date", DateTime.Parse(date));
            doc.Add(new IndexDocumentField("Location", GeoPoint.TryParse(location), IndexDocumentFieldValueType.GeoPoint) { IsRetrievable = true, IsFilterable = true });

            doc.AddFilterableCollection("Catalog", "Goods");
            doc.AddFilterableCollection("Catalog", "Stuff");

            doc.Add(new IndexDocumentField("NumericCollection", size, IndexDocumentFieldValueType.Integer) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
            doc.Add(new IndexDocumentField("NumericCollection", 10, IndexDocumentFieldValueType.Integer) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
            doc.Add(new IndexDocumentField("NumericCollection", 20, IndexDocumentFieldValueType.Integer) { IsRetrievable = true, IsFilterable = true, IsCollection = true });

            doc.AddFilterableCollection("Is", "Priced");
            doc.AddFilterableCollection("Is", color);
            doc.AddFilterableCollection("Is", id);

            doc.Add(new IndexDocumentField("StoredField", "This value should not be processed in any way, it is just stored in the index.", IndexDocumentFieldValueType.String) { IsRetrievable = true });

            foreach (var price in prices)
            {
                doc.Add(new IndexDocumentField($"Price_{price.Currency}_{price.Pricelist}", price.Amount, IndexDocumentFieldValueType.Decimal) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
                doc.Add(new IndexDocumentField($"Price_{price.Currency}", price.Amount, IndexDocumentFieldValueType.Decimal) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
            }

            doc.AddFilterableBoolean("HasMultiplePrices", prices.Length > 1);

            // Adds extra fields to test mapping updates for indexer
            if (name2 != null)
            {
                doc.AddFilterableString("Name 2", name2);
            }

            if (date2 != null)
            {
                doc.AddFilterableDateTime("Date (2)", date2.Value);
            }

            doc.Add(new IndexDocumentField("__obj", obj, IndexDocumentFieldValueType.Complex) { IsRetrievable = true, IsFilterable = true });

            return doc;
        }

        protected virtual ISettingsManager GetSettingsManager()
        {
            var mock = new Mock<ITestSettingsManager>();

            mock.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string defaultValue) => defaultValue);
            mock.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<bool>())).Returns((string _, bool defaultValue) => defaultValue);
            mock.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<int>())).Returns((string _, int defaultValue) => defaultValue);
            mock.Setup(s => s.GetObjectSettingAsync(It.IsAny<string>(), null, null))
                .Returns(Task.FromResult(new ObjectSettingEntry()));

            return mock.Object;
        }

        protected virtual IFilter CreateRangeFilter(string fieldName, string lower, string upper, bool includeLower, bool includeUpper)
        {
            return new RangeFilter
            {
                FieldName = fieldName,
                Values = new[]
                {
                    new RangeFilterValue
                    {
                        Lower = lower,
                        Upper = upper,
                        IncludeLower = includeLower,
                        IncludeUpper = includeUpper,
                    }
                },
            };
        }

        protected virtual long GetAggregationValuesCount(SearchResponse response, string aggregationId)
        {
            var aggregation = GetAggregation(response, aggregationId);
            var result = aggregation?.Values?.Count ?? 0;
            return result;
        }

        protected virtual long GetAggregationValueCount(SearchResponse response, string aggregationId, string valueId)
        {
            var aggregation = GetAggregation(response, aggregationId);
            var result = GetAggregationValueCount(aggregation, valueId);
            return result;
        }

        protected virtual AggregationResponse GetAggregation(SearchResponse response, string aggregationId)
        {
            AggregationResponse result = null;

            if (response?.Aggregations?.Count > 0)
            {
                result = response.Aggregations.SingleOrDefault(a => a.Id.EqualsInvariant(aggregationId));
            }

            return result;
        }

        protected virtual long GetAggregationValueCount(AggregationResponse aggregation, string valueId)
        {
            long? result = null;

            if (aggregation?.Values?.Count > 0)
            {
                result = aggregation.Values
                    .Where(v => v.Id == valueId)
                    .Select(facet => facet.Count)
                    .SingleOrDefault();
            }

            return result ?? 0;
        }

        protected class Price
        {
            public Price(string currency, string pricelist, decimal amount)
            {
                Currency = currency;
                Pricelist = pricelist;
                Amount = amount;
            }

            public string Currency;
            public string Pricelist;
            public decimal Amount;
        }

        /// <summary>
        /// Allowing to moq extensions methods
        /// </summary>
        public interface ITestSettingsManager : ISettingsManager
        {
            T GetValue<T>(string name, T defaultValue);
            Task<T> GetValueAsync<T>(string name, T defaultValue);
        }

        public class TestObjectValue : IEntity
        {
            public TestObjectValue(object value, string valueType)
                : this()
            {
                AddProperty(value, valueType);
            }

            public TestObjectValue()
            {
                Id = Guid.NewGuid().ToString();
                var ids = new[] { Id };
                StringArray = ids;
                StringList = ids;
            }

            public Property AddProperty(object value, string valueType)
            {
                var propValue = new PropertyValue { Value = value, ValueType = valueType };
                var values = new[] { propValue };
                var property = new Property
                {
                    Array = values,
                    List = values,
                    ValueInProperty = propValue,
                    Value = value
                };

                TestProperties.Add(property);

                return property;
            }

            public IList<Property> TestProperties { get; set; } = new List<Property>();
            public string Id { get; set; }
            public string[] StringArray { get; set; }
            public IList<string> StringList { get; set; }
            public PropertyValue Value { get; set; }
        }

        public class Property : IEntity
        {
            public string[] Ids { get; set; }
            public PropertyValue[] Array { get; set; }
            public IList<PropertyValue> List { get; set; } = new List<PropertyValue>();
            public PropertyValue ValueInProperty { get; set; }
            public string ValueType { get; set; }
            public bool IsActive { get; set; }
            public string Id { get; set; }
            public object Value { get; set; }
        }

        public class PropertyValue : IEntity
        {
            public object Value { get; set; }
            public string ValueType { get; set; }
            public bool IsActive { get; set; }
            public string Id { get; set; }
        }
    }
}
