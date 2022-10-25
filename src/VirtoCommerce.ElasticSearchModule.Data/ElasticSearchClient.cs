using Nest;

namespace VirtoCommerce.ElasticSearchModule.Data;

public class ElasticSearchClient : ElasticClient
{
    public ElasticSearchClient(IConnectionSettingsValues connectionSettingsValues)
        : base(connectionSettingsValues)
    {        
    }
}
