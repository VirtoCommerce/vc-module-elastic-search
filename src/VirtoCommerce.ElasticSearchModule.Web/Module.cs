using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.ElasticSearchModule.Data;
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
                serviceCollection.AddSingleton<ISearchProvider, ElasticSearchProvider>();
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

                var settingsManager = appBuilder.ApplicationServices.GetRequiredService<ISettingsManager>();
                var searchOptions = appBuilder.ApplicationServices.GetRequiredService<IOptions<SearchOptions>>();
                var elasticSearchOptions = appBuilder.ApplicationServices.GetRequiredService<IOptions<ElasticSearchOptions>>();

                var provider = new ElasticSearchProvider(elasticSearchOptions, searchOptions, settingsManager);
                provider.AddActiveAlias(documentTypes);
            }
        }

        public void Uninstall()
        {
            // not needed
        }
    }
}
