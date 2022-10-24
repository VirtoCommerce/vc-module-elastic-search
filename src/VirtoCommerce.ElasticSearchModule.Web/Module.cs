using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.ElasticSearchModule.Web.Infrastructure;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.ElasticSearchModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        private bool IsElasticEnabled
        {
            get
            {
                var provider = Configuration.GetValue<string>("Search:Provider");
                return provider.EqualsInvariant("ElasticSearch");
            }
        }

        public void Initialize(IServiceCollection serviceCollection)
        {
            if (IsElasticEnabled)
            {
                serviceCollection.Configure<ElasticSearchOptions>(Configuration.GetSection("Search:ElasticSearch"));
                serviceCollection.AddTransient<ElasticSearchRequestBuilder>();
                serviceCollection.AddSingleton<IConnectionSettingsValues, ElasticSearchConnectionSettings>();
                serviceCollection.AddSingleton<IElasticClient, ElasticSearchClient>();
                serviceCollection.AddSingleton<ISearchProvider, ElasticSearchProvider>();
                serviceCollection.AddHealthChecks().AddCheck<ElasticHealthChecker>("elastic_health_checker", tags: new string[] { "Modules", "Elastic" });
            }
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            if (IsElasticEnabled)
            {
                var documentConfigs = appBuilder.ApplicationServices.GetRequiredService<IEnumerable<IndexDocumentConfiguration>>();
                var documentTypes = documentConfigs.Select(c => c.DocumentType).Distinct().ToList();

                var provider = appBuilder.ApplicationServices.GetRequiredService<ISearchProvider>();
                ((ElasticSearchProvider)provider).AddActiveAlias(documentTypes);
            }
        }

        public void Uninstall()
        {
            // not needed
        }
    }
}
