using Microsoft.AspNetCore.Mvc;
using WebApi.Crud;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointHandlers();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseRouting();
app.MapGet("/", () => "Hello World!");
app.MapGet("/a", EndpointHandler.AutoDelegate<HomeHandler>());

//app.UseEndpoints(configure => configure.MapPost("/bam", EndpointHandler.Delegate<MyHandler, MyRequest, MyResponse>()));
//app.UseEndpoints(configure => configure.MapGet("/items", EndpointHandler.Delegate<ListItemsHandler, object?, IEnumerable<Item>>()));
app.UseEndpoints(configure => configure.MapGet("/items", EndpointHandler.AutoDelegate<ListItemsHandler>()));

app.UseEndpointHandler<MyHandler>("/bam", HttpMethod.Get, x => x.AllowAnonymous());
app.UseEndpointHandler<HelloWorld>("/hello", HttpMethod.Get);

// some standard endpoints
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/world", async (HelloWorld handler) => await handler.HandleAsync(null, CancellationToken.None));
    endpoints.MapGet("/test", async ([FromBody]TestHandler.Request request, TestHandler handler) => await handler.HandleAsync(request, CancellationToken.None));
});


// Leaving the crud experiments for now
// Add Crud
//app.AddCrud<Item>("/items/{id}", x =>
//{
//    x.AddCreate<CreateHandler>();
//});

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
public class HomeHandler : IHandler<HttpRequest, IResult>
{
    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        //await request.HttpContext.Response.WriteAsync("Hello World");
        await Task.Yield();

        return Results.Content("Hello world!");
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

public record ListRequest(int Page, int? PageSize);
public record Item(int Id, string Name);

public sealed class ListItemsHandler : IHandler<ListRequest?, IEnumerable<Item>>
{
    public async Task<IEnumerable<Item>> HandleAsync(ListRequest? request, CancellationToken cancellationToken)
    {
        await Task.Yield();

        var page = request?.Page ?? 0;
        var size = request?.PageSize ?? 50;
        
        return UnlimitedOrderedItems.Skip(page * size).Take(size);
    }

    // Really not unlimited - "only" 1 mio items
    private IEnumerable<Item> UnlimitedOrderedItems => Enumerable.Range(1, 1000000).Select(x => new Item(x, $"Item number {x}"));
}