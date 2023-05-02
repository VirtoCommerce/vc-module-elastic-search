using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace VirtoCommerce.ElasticSearchModule.Tests;

public class Startup
{
    public static void ConfigureHost(IHostBuilder hostBuilder)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<ElasticSearchTests>()
            .AddEnvironmentVariables()
            .Build();

        hostBuilder.ConfigureHostConfiguration(builder => builder.AddConfiguration(configuration));
    }
}
