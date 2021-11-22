public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEndpointHandlers(this IServiceCollection serviceCollection)
    {
        var type = typeof(IHandler<,>);
        var handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.DefinedTypes)
            .Where(x=>x.IsClass && !x.IsAbstract)
            .Where(x => x.ImplementedInterfaces.Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == type));

        foreach (var handlerType in handlerTypes)
        {
            serviceCollection.AddScoped(handlerType);
        }

        return serviceCollection;
    }
}

