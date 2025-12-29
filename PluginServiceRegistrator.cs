using UniverseCollections.Services;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace UniverseCollections;

/// <summary>
/// Registers plugin services with the DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<TraktService>();
        services.AddSingleton<CollectionSyncService>();
    }
}
