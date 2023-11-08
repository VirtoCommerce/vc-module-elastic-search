using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.ElasticSearchModule.Web.Infrastructure;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearchModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.Configure<ElasticSearchOptions>(Configuration.GetSection("Search:ElasticSearch"));
            serviceCollection.AddTransient<ElasticSearchRequestBuilder>();
            serviceCollection.AddSingleton<IConnectionSettingsValues, ElasticSearchConnectionSettings>();
            serviceCollection.AddSingleton<IElasticClient, ElasticSearchClient>();
            serviceCollection.AddSingleton<ElasticSearchProvider>();
            serviceCollection.AddHealthChecks().AddCheck<ElasticHealthChecker>("Elastic server connection", tags: new[] { "Modules", "Elastic" });
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            var provider = appBuilder.UseSearchProvider<ElasticSearchProvider>("ElasticSearch");
            var documentConfigs = appBuilder.ApplicationServices.GetRequiredService<IEnumerable<IndexDocumentConfiguration>>();
            var documentTypes = documentConfigs.Select(c => c.DocumentType).Distinct();
            provider.AddActiveAlias(documentTypes);
        }

        public void Uninstall()
        {
            // not needed
        }
    }
}
