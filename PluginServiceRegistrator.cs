using UniverseCollections.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace UniverseCollections;

/// <summary>
/// Registers plugin services with the DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient();
        serviceCollection.AddSingleton<TraktService>();
        serviceCollection.AddSingleton<CollectionSyncService>();
    }
}
