namespace WebApi.Crud
{
    public static class WebApplicationExtensions
    {
        public static WebApplication AddCrud<T>(this WebApplication webApplication, string pattern, Action<ICrudBuilder<T>> configure)
        {
            var builder = new CrudBuilder<T>(webApplication, pattern);

            configure(builder);

            return webApplication;
        }
    }

    public interface ICrudBuilder<T>
    {
        public ICrudBuilder<T> AddCreate<THandler>() where THandler : IHandler<T, T>;
        public ICrudBuilder<T> AddRead<THandler, TKey>(Func<T, TKey>? selector = null) where THandler : IHandler<TKey, T>;

        //public ICrudBuilder<T> AddUpdate<THandler, TKey>() where THandler : IHandler<(TKey, T), T>;
        //public ICrudBuilder<T> AddDelete<THandler, TKey>() where THandler : IHandler<TKey, object?>;
        //public ICrudBuilder<T> AddList<THandler>() where THandler : IHandler<object?, IEnumerable<T>>;
    }

    internal class CrudBuilder<T> : ICrudBuilder<T>
    {
        private readonly WebApplication _webApplication;
        private readonly string _pattern;

        public CrudBuilder(WebApplication webApplication, string pattern)
        {
            _webApplication = webApplication;
            _pattern = pattern;
        }

        public ICrudBuilder<T> AddCreate<THandler>() where THandler : IHandler<T, T>
        {
            _webApplication.UseEndpoints(configure => configure.MapMethods(_pattern, new[] { "POST" }, EndpointHandler.Delegate<THandler, T, T>()));

            return this;
        }

        public ICrudBuilder<T> AddRead<THandler, TKey>(Func<T, TKey>? selector = null) where THandler : IHandler<TKey, T>
        {
            var options = new RequestOptions()
            {
                DeserializeRequestFrom = RequestOptions.RequestOrigin.RouteValues
            };

            _webApplication.UseEndpoints(configure => configure.MapMethods($"{_pattern}/{{key}}", new[] { "GET" }, EndpointHandler.Delegate<THandler, TKey, T>(options)));

            return this;
        }
    }   
}

public interface ICreateHandler<T> : IHandler<T, T>
{

}

public interface IReadHandler<T> : IHandler<Uri, T>
{

}

public interface IUpdateHandler<T> : IHandler<(Uri, T), T>
{

}

public interface IDeleteHandler<T> : IHandler<Uri, object?>
{

}