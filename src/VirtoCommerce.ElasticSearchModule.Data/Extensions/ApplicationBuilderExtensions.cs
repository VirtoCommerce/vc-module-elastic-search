using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.ElasticSearchModule.Data.Extensions;

public static class ApplicationBuilderExtensions
{
    public static T UseSearchProvider<T>(this IApplicationBuilder applicationBuilder, string name, Action<T, IList<string>> configureAction = null)
        where T : ISearchProvider
    {
        var serviceProvider = applicationBuilder.ApplicationServices;
        var gateway = serviceProvider.GetRequiredService<ISearchGateway>();
        var provider = serviceProvider.GetRequiredService<T>();
        gateway.AddSearchProvider(provider, name);

        // Configure search provider with a list of document types related to the given provider name
        if (configureAction != null)
        {
            var configurations = serviceProvider.GetRequiredService<IEnumerable<IndexDocumentConfiguration>>();
            var documentTypes = configurations.Select(c => c.DocumentType).Distinct();
            var options = serviceProvider.GetRequiredService<IOptions<SearchOptions>>().Value;

            if (options.Provider.EqualsInvariant(name))
            {
                var otherProviderDocumentTypes = options.DocumentScopes
                    .Where(x => !string.IsNullOrEmpty(x.Provider) && !x.Provider.EqualsInvariant(name))
                    .Select(x => x.DocumentType);

                documentTypes = documentTypes.Except(otherProviderDocumentTypes, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var currentProviderDocumentTypes = options.DocumentScopes
                    .Where(x => x.Provider.EqualsInvariant(name))
                    .Select(x => x.DocumentType);

                documentTypes = documentTypes.Intersect(currentProviderDocumentTypes, StringComparer.OrdinalIgnoreCase);
            }

            configureAction(provider, documentTypes.ToList());
        }

        return provider;
    }
}
