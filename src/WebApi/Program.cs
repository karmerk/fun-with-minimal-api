using WebApi.Crud;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Items>(_ => new Items(3));
builder.Services.AddEndpointHandlers();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseRouting();
app.MapGet("/", () => "Hello World!");
app.MapGet("/a", EndpointHandler.AutoDelegate<HomeHandler>());


// lets drop the concept with RequestHandlers and make it EndpointHandlers

app.UseEndpoints(configure => configure.MapGet("/test/{id}", EndpointHandler.AutoDelegate<TestHandler>()));
app.UseEndpoints(configure => configure.MapGet("/test3/{id}", async (int id, ReadRessourceHandler handler) => await handler.HandleAsync(id, CancellationToken.None)));

//app.UseEndpoints(configure => configure.MapPost("/bam", EndpointHandler.Delegate<MyHandler, MyRequest, MyResponse>()));
//app.UseEndpoints(configure => configure.MapGet("/items", EndpointHandler.Delegate<ListItemsHandler, object?, IEnumerable<Item>>()));
app.UseEndpoints(configure => configure.MapGet("/items", EndpointHandler.AutoDelegate<ListItemsHandler>()));

//app.UseEndpointHandler<MyHandler>("/bam", HttpMethod.Get);
app.UseEndpointHandler<MyHandler>("/bam", HttpMethod.Get, x => x.AllowAnonymous());

app.UseEndpointHandler<HelloWorld>("/hello", HttpMethod.Get);

// Add Crud
app.AddCrud<Item>("/item", x =>
{
    x.AddCreate<ItemCreateHandler>();
    x.AddRead<ItemReadHandler, int>();
});

app.Run();


public class HelloWorld : IHandler<HelloWorld.Nothing?, HelloWorld.Response>
{
    public Task<Response> HandleAsync(Nothing? request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Response("world"));
    }

    public sealed class Nothing {}

    public sealed class Response
    {
        public string Hello { get; }

        public Response(string hello)
        {
            Hello = hello;
        }
    }
}

// This responds to ../test/6d27e69f-e0b9-49d1-82c7-c177a410a0f9?text=hello world
public class TestHandler : IHandler<TestHandler.Request, TestHandler.Response>
{
    public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        await Task.Yield();

        request.Deconstruct(out var id, out var text);

        return new Response(id, text ?? "Empty string");
    }

    public record Request(Guid Id, string? Text);

    public record Response(Guid Id, string Text);
}

// Handler that uses the HttpRequest directly, could also be done by injekting IHttpContextAccessor
public class HomeHandler : IHandler<HttpRequest, HomeHandler.Nothing?>
{
    public async Task<Nothing?> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        await request.HttpContext.Response.WriteAsync("Hello World");

        return null;
    }

    public record Nothing { };

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

