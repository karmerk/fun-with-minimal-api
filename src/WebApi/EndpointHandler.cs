using System.Web;

public static class EndpointHandler
{
    // This is what i want as the primary API
    public static WebApplication? UseEndpointHandler<THandler>(this WebApplication? webApplication, string pattern, HttpMethod httpMethod, Action<RouteHandlerBuilder>? configure = null)
    {
        webApplication?.UseEndpoints(endpoints =>
        {
            var specification = EndpointHandlerSpecification.Create<THandler>();
            var handler = specification.BuildHandler();
            
            var builder = endpoints.MapMethods(pattern, new[] { httpMethod.Method }, handler);

            configure?.Invoke(builder);
        });

        return webApplication;
    }

    public static Delegate AutoDelegate<THandler>()
    {
        var specification = EndpointHandlerSpecification.Create<THandler>();

        return specification.BuildHandler();
    }

    [Obsolete("use UseEndpointHandler")]
    public static Delegate UsingDelegateMethod<THandler, TRequest, TResponse>() where THandler : IHandler<TRequest, TResponse>
    {
        // When using the delegate method, we can let, minimal api do all the work with the deserialization aso..
        // but this also means that we are requred to use the attributes to ensure that argument match
        
        // In this case it means that the "request" argument will be deserialized from the querystring and must be called request
        // this creates a dependency between the handler (in this case my endpoint handler) and the route.
        return (async (TRequest request, THandler handler) =>
        {
           return await handler.HandleAsync(request, CancellationToken.None);
        });

        // The upside is that all the model binding is taken care of.
        // https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Extensions/src/RequestDelegateFactory.cs#L259

        // Im having an existential cricis for this experimental playground, or at least the value of a EndpointHandler concept


        // From above
        // an integer is expected in the querystring, the UsingDelegateMethod defines this, the name is "request"
        // app.UseEndpoints(configure => configure.MapGet("/test2", EndpointHandler.UsingDelegateMethod<ReadRessourceHandler, int, MyResponse>()));

        // an integer is expected in the route, its defined in the route and the name must match the parameter in the delegate. this is the link
        // app.UseEndpoints(configure => configure.MapGet("/test3/{id}", async (int id, ReadRessourceHandler handler) => await handler.HandleAsync(id, CancellationToken.None)));
    }
        
    public static Delegate Delegate<THandler, TRequest, TResponse>() where THandler : IHandler<TRequest, TResponse>
    {
        var specification = new EndpointHandlerSpecification(typeof(THandler), typeof(TRequest), typeof(TResponse));

        return BuildHandler<THandler, TRequest, TResponse>(specification);
    }

    internal static Delegate BuildHandler<THandler, TRequest, TResponse>(EndpointHandlerSpecification specification) where THandler : IHandler<TRequest, TResponse>
    {
        // check that THandler, TRequest, TResponse match the expected types

        return new RequestDelegate(async (context) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<THandler>>();
            var httpRequest = context.Request;

            if (!RequestIsHttpRequest<TRequest>(httpRequest, out var request))
            {
                // complex object deserializer - should we have an option for basic types. string/guid/int
                // as it is now, the handlers must make a complex object with properties matching route+querystring args
                var deserializer = context.RequestServices.GetService<RequestDeserializer>() ?? new RequestDeserializer();

                request = await deserializer.DeserializeAsync<TRequest>(httpRequest, specification.RequestIsOptional);

                if (request == null && !specification.RequestIsOptional)
                {
                    throw new ArgumentException($"Failed to deserialze non optional object of {typeof(TRequest).Name} from HTTP request");
                }
            }

            var handler = context.RequestServices.GetRequiredService<THandler>();
            var response = await handler.HandleAsync(request, CancellationToken.None);

            if(response is IResult result)
            {
                await result.ExecuteAsync(context);
            }
            else if (response != null)
            {
                await Results.Json(response).ExecuteAsync(context);
            }
        });

    }

    public static bool RequestIsHttpRequest<TRequest>(HttpRequest httpRequest, out TRequest request)
    {
        if (typeof(TRequest) == typeof(HttpRequest))
        {
            object obj = httpRequest;

            request = (TRequest)obj;
            return true;
        }
        request = default!;
        return false;
    }

    public interface IRequestDeserializer
    {
        Task<TRequest> DeserializeAsync<TRequest>(HttpRequest httpRequest, bool optional);
    }

    public sealed class RequestDeserializer : IRequestDeserializer
    {
        public async Task<TRequest> DeserializeAsync<TRequest>(HttpRequest httpRequest, bool optional)
        {
            TRequest request = default!;

            var values = new Dictionary<string, string>();

            if (httpRequest.QueryString.HasValue)
            {
                var qs = HttpUtility.ParseQueryString(httpRequest.QueryString.ToString());

                foreach (var key in qs.Cast<string>())
                {
                    values[key] = qs[key] ?? string.Empty;
                }
            }

            if (httpRequest.RouteValues.Any())
            {
                foreach (var rv in httpRequest.RouteValues)
                {
                    values[rv.Key] = rv.Value?.ToString() ?? string.Empty;
                }
            }

            if (values.Any())
            {
                // Deserialize from querystring and route values
                var json = System.Text.Json.JsonSerializer.Serialize(values);
                request = System.Text.Json.JsonSerializer.Deserialize<TRequest>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            else if (httpRequest.HasJsonContentType())
            {
                // Deserialize from JSON body
                request = await httpRequest.ReadFromJsonAsync<TRequest>() ?? throw new BadHttpRequestException($"Failed to deserialize object {typeof(TRequest).Name} from JSON body");
            }

            return request;
        }
    }
}

