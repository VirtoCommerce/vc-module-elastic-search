using System;
using Elasticsearch.Net;
using Microsoft.Extensions.Options;
using Nest;
using Nest.JsonNetSerializer;

namespace VirtoCommerce.ElasticSearchModule.Data;

public class ElasticSearchConnectionSettings : ConnectionSettings
{
    public ElasticSearchConnectionSettings(IOptions<ElasticSearchOptions> elasticSearchOptions)
        : base(GetConnectionPool(elasticSearchOptions.Value), JsonNetSerializer.Default)
    {
        var options = elasticSearchOptions.Value;
        var userName = options.User;
        var password = options.Key;

        if (!string.IsNullOrEmpty(password))
        {
            // elastic is default name for elastic cloud
            BasicAuthentication(userName ?? "elastic", password);
        }

        if (options.EnableHttpCompression.HasValue && (bool)options.EnableHttpCompression)
        {
            EnableHttpCompression();
        }

        if (options.EnableCompatibilityMode.HasValue && (bool)options.EnableCompatibilityMode)
        {
            EnableApiVersioningHeader();
        }

        if (!string.IsNullOrEmpty(options.CertificateFingerprint))
        {
            CertificateFingerprint(options.CertificateFingerprint);
        }
    }

    protected static IConnectionPool GetConnectionPool(ElasticSearchOptions options)
    {
        var serverUrl = GetServerUrl(options);

        return new SingleNodeConnectionPool(serverUrl);
    }

    protected static Uri GetServerUrl(ElasticSearchOptions options)
    {
        var server = options.Server;

        if (string.IsNullOrEmpty(server))
        {
            throw new ArgumentException("'Server' parameter must not be empty");
        }

        if (!server.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            server = "http://" + server;
        }

        server = server.TrimEnd('/');

        return new Uri(server);
    }
}
