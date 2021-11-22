using System.Reflection;

internal sealed class EndpointHandlerSpecification
{
    public Type HandlerType { get; }
    public Type RequestType { get; }
    public Type ResponseType { get; }

    public bool RequestIsOptional { get; }

    public EndpointHandlerSpecification(Type handlerType, Type requestType, Type responseType)
    {
        HandlerType = handlerType;
        RequestType = requestType;
        ResponseType = responseType;

        var requestParameter = handlerType.GetMethod(nameof(IHandler<int, int>.HandleAsync))!.GetParameters().First()!;
        RequestIsOptional = IsOptional(requestParameter);
    }

    public static EndpointHandlerSpecification Create<THandler>()
    {
        var handlerType = typeof(THandler);
        var interfaceType = handlerType.GetInterfaces()
            .Where(x => x.GetGenericTypeDefinition() == typeof(IHandler<,>))
            .FirstOrDefault();

        if (interfaceType == null)
        {
            throw new ArgumentException($"It is a requirement that <THandler> implement IHandler");
        }

        var genericArguments = interfaceType?.GetGenericArguments()!;

        return new EndpointHandlerSpecification(handlerType, genericArguments[0], genericArguments[1]);
    }

    public Delegate BuildHandler()
    {
        var method = typeof(EndpointHandler).GetMethod(nameof(BuildHandler), 3, BindingFlags.NonPublic | BindingFlags.Static, binder: null, new Type[] { typeof(EndpointHandlerSpecification)}, modifiers: null)!;
        method = method.MakeGenericMethod(HandlerType, RequestType, ResponseType);

        return (Delegate)method.Invoke(this, parameters: new object?[] { this })!;
    }

    private static bool IsOptional(ParameterInfo parameterInfo)
    {
        var context = new NullabilityInfoContext();
        var nullabilityInfo = context.Create(parameterInfo);

        return nullabilityInfo?.ReadState == NullabilityState.Nullable;
    }
}

