using Microsoft.Practices.Unity;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.ElasticSearchModule.Data;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ElasticSearchModule.Web
{
    public class Module : ModuleBase
    {
        private readonly IUnityContainer _container;

        public Module(IUnityContainer container)
        {
            _container = container;
        }

        public override void Initialize()
        {
            base.Initialize();

            var searchConnection = _container.Resolve<ISearchConnection>();

            if (searchConnection?.Provider?.EqualsInvariant("ElasticSearch") == true)
            {
                _container.RegisterType<ISearchProvider, ElasticSearchProvider>(
                    new ContainerControlledLifetimeManager(),
                    new InjectionFactory(c => new ElasticSearchProvider(
                        c.Resolve<ISearchConnection>(),
                        c.Resolve<ISettingsManager>()))
                );
            }
        }
    }
}
