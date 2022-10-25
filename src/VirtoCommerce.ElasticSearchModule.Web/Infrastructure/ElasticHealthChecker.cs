using System;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Nest;
using VirtoCommerce.ElasticSearchModule.Data;

namespace VirtoCommerce.ElasticSearchModule.Web.Infrastructure
{
    public class ElasticHealthChecker : IHealthCheck
    {
        private readonly IElasticClient _elasticClient;
        private readonly ElasticSearchOptions _elasticSearchOptions;

        public ElasticHealthChecker(IElasticClient elasticClient, IOptions<ElasticSearchOptions> elasticSearchOptions)
        {
            _elasticClient = elasticClient;
            _elasticSearchOptions = elasticSearchOptions.Value;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // Use different timeout for the healthchecking pings. Otherwise ping will hangs on default timeout so much longer (default is minute and more).
            var pingResult = await _elasticClient.PingAsync(
                new PingRequest() { RequestConfiguration = new RequestConfiguration() { RequestTimeout = TimeSpan.FromSeconds(_elasticSearchOptions.HealthCheckTimeout) } },
                cancellationToken);
            if (pingResult.IsValid)
            {
                return HealthCheckResult.Healthy("Elastic server is reachable");
            }
            else
            {
                return HealthCheckResult.Unhealthy(@$"No connection to Elastic-server.{Environment.NewLine}{pingResult.ApiCall.DebugInformation}");
            }
        }
    }
}
