var builder = WebApplication.CreateBuilder(args);

// TODO should also have an option to AddAllRequestHandlers(Assembly);

builder.Services.AddScoped<MyHandler>();
builder.Services.AddScoped<ReadRessourceHandler>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseRouting();
app.MapGet("/", () => "Hello World!");


// lets drop the concept with RequestHandlers and make it EndpointHandlers

app.UseEndpoints(app => app.MapGet("/items", EndpointHandler.Delegate<ReadRessourceHandler, int, MyResponse>()));
app.UseEndpoints(app => app.MapPost("/bam", EndpointHandler.Delegate<MyHandler, MyRequest, MyResponse>()));

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

            if(!httpRequest.HasJsonContentType())
            {
                throw new InvalidOperationException("Only JSON content type supported right now");
            }

            // If TRequest nullable then we should accept no input ! 
            // Also request should be able to come from body, querystring, routevalues and form? that'll be fun to implement
            var request = await httpRequest.ReadFromJsonAsync<TRequest>() ?? throw new ArgumentException($"Could not read {typeof(TRequest).Name} as JSON from request body");
            
            var handler = context.RequestServices.GetRequiredService<THandler>();
            var response = await handler.HandleAsync(request, CancellationToken.None);

            if (response != null)
            {
                await context.Response.WriteAsJsonAsync(response);
            }
        });
    }
    

    // Can we make a method that does not need to specify the TRequest and TResponse explicit ?
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