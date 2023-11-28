using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.ElasticSearchModule.Data.Extensions;
using VirtoCommerce.ElasticSearchModule.Web.Infrastructure;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Extensions;

namespace VirtoCommerce.ElasticSearchModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
            {
                serviceCollection.Configure<ElasticSearchOptions>(Configuration.GetSection($"Search:{ModuleConstants.ProviderName}"));
                serviceCollection.AddTransient<ElasticSearchRequestBuilder>();
                serviceCollection.AddSingleton<IConnectionSettingsValues, ElasticSearchConnectionSettings>();
                serviceCollection.AddSingleton<IElasticClient, ElasticSearchClient>();
                serviceCollection.AddSingleton<ElasticSearchProvider>();
                serviceCollection.AddHealthChecks().AddCheck<ElasticHealthChecker>("Elastic server connection", tags: new[] { "Modules", "Elastic" });
            }
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
            {
                appBuilder.UseSearchProvider<ElasticSearchProvider>(ModuleConstants.ProviderName, (provider, documentTypes) =>
                {
                    provider.AddActiveAlias(documentTypes);
                });
            }
        }

        public void Uninstall()
        {
            // not needed
        }
    }
}
