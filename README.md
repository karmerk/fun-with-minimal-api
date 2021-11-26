# fun-with-minimal-api
Inspired by [MediatR](https://github.com/jbogard/MediatR) and [FastEndpoints](https://github.com/dj-nitehawk/FastEndpoints) I wanted to do some experimenting to see if it was possbile to build something with a nice API that could hook directly into ASP.NET Core Minimal API.


The goal was to get to something like this:
```csharp

app.UseEndpointHandler<HelloWorld>("/hello", HttpMethod.Get);

```
  
Which would route requests directly to a ``HelloWorld`` handler, which needs to be registered as a service.

```csharp

builder.Services.AddScoped<HelloWorld>();

```

All handlers take a request object witch is deserialized from the (``RouteValues`` and ``QueryString``) **or** the body as JSON. As im writing this - I have not found a good way of combining all three sources into a single request object.

```csharp
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
```
