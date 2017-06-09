using System.Collections.Generic;
using System.Linq;
using Nest;
using Newtonsoft.Json.Linq;
using VirtoCommerce.Domain.Search;

namespace VirtoCommerce.ElasticSearchModule.Data
{
    public static class ElasticSearchResponseBuilder
    {
        public static SearchResponse ToSearchResponse(this ISearchResponse<SearchDocument> response, Domain.Search.SearchRequest request, string documentType)
        {
            var result = new SearchResponse
            {
                TotalCount = response.Total,
                Documents = response.Hits.Select(ToSearchDocument).ToArray(),
                Aggregations = GetAggregations(response.Aggregations, request)
            };

            return result;
        }

        public static SearchDocument ToSearchDocument(IHit<SearchDocument> hit)
        {
            var result = new SearchDocument { Id = hit.Id };

            // Copy fields and convert JArray to object[]
            foreach (var kvp in hit.Source)
            {
                var name = kvp.Key;
                var value = kvp.Value;

                var jArray = kvp.Value as JArray;
                if (jArray != null)
                {
                    value = jArray.ToObject<object[]>();
                }

                result.Add(name, value);
            }

            return result;
        }

        private static IList<AggregationResponse> GetAggregations(IReadOnlyDictionary<string, IAggregate> searchResponseAggregations, Domain.Search.SearchRequest request)
        {
            var result = new List<AggregationResponse>();

            if (request?.Aggregations != null && searchResponseAggregations != null)
            {
                foreach (var aggregationRequest in request.Aggregations)
                {
                    var aggregation = new AggregationResponse
                    {
                        Id = (aggregationRequest.Id ?? aggregationRequest.FieldName).ToLowerInvariant(),
                        Values = new List<AggregationResponseValue>(),
                    };

                    var termAggregationRequest = aggregationRequest as TermAggregationRequest;
                    var rangeAggregationRequest = aggregationRequest as RangeAggregationRequest;

                    if (termAggregationRequest != null)
                    {
                        AddAggregationValue(aggregation, aggregation.Id, aggregation.Id, searchResponseAggregations);
                    }
                    else if (rangeAggregationRequest?.Values != null)
                    {
                        foreach (var value in rangeAggregationRequest.Values)
                        {
                            var queryValueId = value.Id.ToLowerInvariant();
                            var responseValueId = $"{aggregation.Id}-{queryValueId}";
                            AddAggregationValue(aggregation, responseValueId, queryValueId, searchResponseAggregations);
                        }
                    }

                    if (aggregation.Values.Any())
                    {
                        result.Add(aggregation);
                    }
                }
            }

            return result;
        }

        private static void AddAggregationValue(AggregationResponse aggregation, string responseKey, string valueId, IReadOnlyDictionary<string, IAggregate> searchResponseAggregations)
        {
            if (searchResponseAggregations.ContainsKey(responseKey))
            {
                var aggregate = searchResponseAggregations[responseKey];
                var bucketAggregate = aggregate as BucketAggregate;
                var singleBucketAggregate = aggregate as SingleBucketAggregate;

                if (singleBucketAggregate != null)
                {
                    if (singleBucketAggregate.Aggregations != null)
                    {
                        bucketAggregate = singleBucketAggregate.Aggregations[responseKey] as BucketAggregate;
                    }
                    else if (singleBucketAggregate.DocCount > 0)
                    {
                        var aggregationValue = new AggregationResponseValue
                        {
                            Id = valueId,
                            Count = singleBucketAggregate.DocCount
                        };

                        aggregation.Values.Add(aggregationValue);
                    }
                }

                if (bucketAggregate != null)
                {
                    foreach (var term in bucketAggregate.Items.OfType<KeyedBucket<object>>())
                    {
                        if (term.DocCount > 0)
                        {
                            var aggregationValue = new AggregationResponseValue
                            {
                                Id = term.Key.ToStringInvariant(),
                                Count = term.DocCount ?? 0
                            };

                            aggregation.Values.Add(aggregationValue);
                        }
                    }
                }
            }
        }
    }
}
