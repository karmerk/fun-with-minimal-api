using System.Web;
using WebApi.Crud;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Items>(_ => new Items(3));
builder.Services.AddEndpointHandlers();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseRouting();
app.MapGet("/", () => "Hello World!");


// lets drop the concept with RequestHandlers and make it EndpointHandlers

app.UseEndpoints(configure => configure.MapGet("/test", EndpointHandler.Delegate<ReadRessourceHandler, int, MyResponse>()));
//app.UseEndpoints(configure => configure.MapPost("/bam", EndpointHandler.Delegate<MyHandler, MyRequest, MyResponse>()));
//app.UseEndpoints(configure => configure.MapGet("/items", EndpointHandler.Delegate<ListItemsHandler, object?, IEnumerable<Item>>()));
app.UseEndpoints(configure => configure.MapGet("/items", EndpointHandler.AutoDelegate<ListItemsHandler>()));

app.UseEndpointHandler<MyHandler>("/bam", HttpMethod.Get);

// Add Crud
app.AddCrud<Item>("/item", x =>
{
    x.AddCreate<ItemCreateHandler>();
    x.AddRead<ItemReadHandler, int>();
});



app.Run();


public static class EndpointHandler 
{
    public static WebApplication? UseEndpointHandler<THandler>(this WebApplication? webApplication, string pattern, HttpMethod httpMethod, Action<IEndpointRouteBuilder>? configure = null)
    {
        webApplication?.UseEndpoints(builder =>
        {
            builder.MapMethods(pattern, new[] { httpMethod.Method }, AutoDelegate<THandler>());
            configure?.Invoke(builder);
        });

        return webApplication;
    }

    public static RequestDelegate AutoDelegate<THandler>()
    {
        var handlerType = typeof(THandler);

        // handler must be IHandler<,>
        // TODO need to do a sh*tload of checks in this method

        var genericArguments = handlerType.GetInterfaces()
            .Where(x => x.GetGenericTypeDefinition() == typeof(IHandler<,>))
            .First()
            ?.GetGenericArguments();

        var requestType = genericArguments[0];
        var resposeType = genericArguments[1];

        var method = typeof(EndpointHandler).GetMethod("Delegate", 3, new Type[] { typeof(HandlingOptions) });
        
        method = method!.MakeGenericMethod(handlerType, genericArguments[0], genericArguments[1]);

        return (RequestDelegate)method.Invoke(null, parameters: new object?[] { null });
    }
    
    public static RequestDelegate Delegate<THandler, TRequest, TResponse>(HandlingOptions? options = null) where THandler : IHandler<TRequest, TResponse>
    {

        // use and rethink the whole options thing

        return new RequestDelegate(async (context) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<THandler>>();

            IRequestReader reader = new DefaultReader(); ;
            var httpRequest = context.Request;
            TRequest request = default!;

            // Also request should be able to come from body, querystring, routevalues and form? that'll be fun to implement
            switch (options?.DeserializeRequestFrom)
            {
                case HandlingOptions.RequestOrigin.QueryString:
                    reader = new QueryStringReader();
                    break;
                case HandlingOptions.RequestOrigin.RouteValues:
                    reader = new RouteValueReader();
                    break;
                case HandlingOptions.RequestOrigin.Form:
                    throw new NotSupportedException(":(");
                case HandlingOptions.RequestOrigin.TryThemAll:
                    throw new NotSupportedException(":(");
                default:
                    reader = new JsonRequestReader();
                    break;
            }

            request = await reader.ReadAsync<TRequest>(httpRequest);

            if (request == null && Nullable.GetUnderlyingType(typeof(TRequest)) == null)
            {
                throw new ArgumentException($"Failed to deserialze object of {typeof(TRequest).Name} from HTTP request using {reader.GetType().Name}");
            }
                        
            var handler = context.RequestServices.GetRequiredService<THandler>();
            var response = await handler.HandleAsync(request, CancellationToken.None);

            if (response != null)
            {
                await context.Response.WriteAsJsonAsync(response);
            }
        });
    }
}

public interface IRequestReader
{
    Task<T> ReadAsync<T>(HttpRequest httpRequest);
}

public class DefaultReader : IRequestReader
{
    public Task<T> ReadAsync<T>(HttpRequest httpRequest)
    {
        return Task.FromResult<T>(default!);
    }
}

public class QueryStringReader : IRequestReader
{
    public Task<T> ReadAsync<T>(HttpRequest httpRequest)
    {
        string responseString = httpRequest.QueryString.ToString();
        var dict = HttpUtility.ParseQueryString(responseString);

        // TODO if we only have one value and T is an basic value type, then return the value directly
        // we could share that logic with the Route value reader.. and also the json part can be used to deserialize into 
        // a complex type for both readers

        string json = System.Text.Json.JsonSerializer.Serialize(dict.Cast<string>().ToDictionary(k => k, v => dict[v]));

        return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json)!);
    }
}

public class RouteValueReader : IRequestReader
{
    public Task<T> ReadAsync<T>(HttpRequest httpRequest)
    {
        T result = default!;

        var value = httpRequest.RouteValues.FirstOrDefault().Value;
        if (value != default)
        {
            if (typeof(T) == typeof(int) && value is string str)
            {
                object integer = Convert.ToInt32(str);

                result = (T)integer;
            }

            if (typeof(T) == typeof(string) && value is string)
            {
                result = (T)value;
            }

            // handle guid ? is that valid as a route param?

            // T is a complex type deserialize into T using route names ?
        }

        return Task.FromResult(result);
    }
}


public class JsonRequestReader : IRequestReader
{
    public async Task<T> ReadAsync<T>(HttpRequest httpRequest)
    {
        T result = default!;
        try
        {
            var value = await httpRequest.ReadFromJsonAsync<T>();

            result = value!;
        }
        catch
        {
            // buhuu do this smarter
            result = default!;
        }

        return result;
    }
}

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


public sealed class HandlingOptions
{
    public enum RequestOrigin
    {
        Body,
        QueryString,
        RouteValues,
        Form,
        TryThemAll // ?
    }

    public RequestOrigin DeserializeRequestFrom { get; init; } = RequestOrigin.Body;
}

// Do we need all the combinations - does that even make any sense ?
// nothing in, nothing out
// nothing in something out
// something in, nothing out
// something in, something out

public interface IHandler<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public record MyRequest(string Id, string Text);
public record MyResponse(string Id, string Text);

public class MyHandler : IHandler<MyRequest, MyResponse>
{
    private readonly ILogger<MyHandler> _logger;

    public MyHandler(ILogger<MyHandler> logger)
    {
        _logger = logger;
    }

    public Task<MyResponse> HandleAsync(MyRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"MyHandler.HandleAsync: {request}");

        var response = new MyResponse(request.Id, request.Text);

        return Task.FromResult(response);
    }
}

public class ReadRessourceHandler : IHandler<int, MyResponse>
{
    private readonly ILogger<ReadRessourceHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReadRessourceHandler(ILogger<ReadRessourceHandler> logger, IHttpContextAccessor httpContextAccessor /* just to show that DI works*/)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<MyResponse> HandleAsync(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"ReadRessourceHandler.HandleAsync: {id}");

        var response = new MyResponse($"{id}", "Read");

        return Task.FromResult(response);
    }
}


public sealed class ListItemsHandler : IHandler<object?, IEnumerable<Item>>
{
    private readonly Items _items;

    public ListItemsHandler(Items items)
    {
        _items = items;
    }

    public async Task<IEnumerable<Item>> HandleAsync(object? request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Awesome

        return _items.Values.Values; // Values.Values :)
    }
}



// Playing around with CRUD

public record Item(int Id, string Name)
{
}

public sealed class Items
{
    public IDictionary<int, Item> Values { get; } = new Dictionary<int,Item>();

    public Items(int count)
    {
        foreach(var id in Enumerable.Range(1, count))
        {
            Values.Add(id, new Item(id, $"Name + {id}"));
        }
    }
}

public sealed class ItemCreateHandler : IHandler<Item, Item>
{
    private readonly Items _items;

    public ItemCreateHandler(Items items)
    {
        _items = items;
    }

    public Task<Item> HandleAsync(Item request, CancellationToken cancellationToken)
    {
        _items.Values.Add(request.Id, request);

        return Task.FromResult(request);
    }
}

public sealed class ItemReadHandler : IHandler<int, Item>
{
    private readonly Items _items;

    public ItemReadHandler(Items items)
    {
        _items = items;
    }

    public Task<Item> HandleAsync(int request, CancellationToken cancellationToken)
    {
        var response = _items.Values[request];

        return Task.FromResult(response);
    }
}

