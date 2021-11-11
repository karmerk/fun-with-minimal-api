using WebApi.Crud;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Items>(_ => new Items(3));
builder.Services.AddEndpointHandlers();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseRouting();
app.MapGet("/", () => "Hello World!");


// lets drop the concept with RequestHandlers and make it EndpointHandlers

app.UseEndpoints(app => app.MapGet("/test", EndpointHandler.Delegate<ReadRessourceHandler, int, MyResponse>()));
app.UseEndpoints(app => app.MapPost("/bam", EndpointHandler.Delegate<MyHandler, MyRequest, MyResponse>()));
app.UseEndpoints(app => app.MapGet("/items", EndpointHandler.Delegate<ListItemsHandler, object?, IEnumerable<Item>>()));

// Add Crud
app.AddCrud<Item>("/item", x =>
{
    x.AddCreate<ItemCreateHandler>();
    x.AddRead<ItemReadHandler, int>();
});

app.Run();


public sealed class EndpointHandler 
{
    //public static async Task Delegate<THandler, TRequest, TResponse>(HttpContext context) where THandler : IHandler<TRequest, TResponse>
    //{
    //    var logger = context.RequestServices.GetRequiredService<ILogger<THandler>>();

    //    // If TRequest nullable then we should accept no input ! 
    //    // Also request should be able to come from body, querystring, routevalues and form? that'll be fun to implement
    //    var request = await context.Request.ReadFromJsonAsync<TRequest>() ?? throw new ArgumentException($"Could not read {typeof(TRequest).Name} from request body");

    //    var handler = context.RequestServices.GetRequiredService<THandler>();
    //    var response = await handler.HandleAsync(request, CancellationToken.None);

    //    if (response != null)
    //    {
    //        await context.Response.WriteAsJsonAsync(response);
    //    }
    //}

    public static RequestDelegate Delegate<THandler, TRequest, TResponse>(HandlingOptions? options = null) where THandler : IHandler<TRequest, TResponse>
    {

        // use and rethink the whole options thing

        return new RequestDelegate(async (context) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<THandler>>();

            var httpRequest = context.Request;
            TRequest request = default!;

            // Also request should be able to come from body, querystring, routevalues and form? that'll be fun to implement
            switch (options?.DeserializeRequestFrom)
            {
                case HandlingOptions.RequestOrigin.QueryString:
                    break;
                case HandlingOptions.RequestOrigin.RouteValues:
                    (_, request) = await new RouteValueRequestReader().TryReadAsync<TRequest>(httpRequest);
                    break;
                case HandlingOptions.RequestOrigin.Form:
                    break;
                case HandlingOptions.RequestOrigin.TryThemAll:
                    break;
                default:
                    (_, request) = await new JsonRequestReader().TryReadAsync<TRequest>(httpRequest);
                    break;
            }

            if( request == null && Nullable.GetUnderlyingType(typeof(TRequest)) != null)
            {
                throw new ArgumentException($"Failed to read {typeof(TRequest).Name} from HTTP request");
            }
                       
            
                        
            var handler = context.RequestServices.GetRequiredService<THandler>();
            var response = await handler.HandleAsync(request, CancellationToken.None);

            if (response != null)
            {
                await context.Response.WriteAsJsonAsync(response);
            }
        });
    }


    // Can we make a method that does not need to specify the TRequest and TResponse explicit ?

    // app.UseEndpoints(app => app.MapGet("/items", EndpointHandler.Delegate<ListItemsHandler>())); 
}

public class JsonRequestReader
{
    // Do a better API and pack it away
    public async Task<(bool, T)> TryReadAsync<T>(HttpRequest request)
    {
        try
        {
            var value = await request.ReadFromJsonAsync<T>();

            return (value != null, value!);
        }
        catch
        {
            // buhuu do this smarter
        }

        return (false, default!);
    }
}

public class RouteValueRequestReader
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task<(bool, T)> TryReadAsync<T>(HttpRequest request)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var value = request.RouteValues.FirstOrDefault().Value;

        if (value != default)
        {
            if (typeof(T) == typeof(int) && value is string str)
            {
                object integer = Convert.ToInt32(str);

                return (true, (T)integer);
            }
             
            if(typeof(T) == typeof(string) && value is string)
            {
                return (true, (T)value);
            }
        }
        return (false, default!);
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

