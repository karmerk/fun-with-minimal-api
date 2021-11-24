using WebApi;

namespace WebApi.Crud
{
    public static class WebApplicationExtensions
    {
        public static WebApplication AddCrud<T>(this WebApplication webApplication, string pattern, Action<ICrudBuilder<T>> configure)
        {
            var id = pattern.SkipWhile(x => x != '{').Skip(1).TakeWhile(x => x != '}').ToArray();

            var builder = new CrudBuilder<T>(webApplication, pattern, new string(id));

            configure(builder);

            return webApplication;
        }
    }

    public interface ICrudBuilder<T>
    {
        public ICrudBuilder<T> AddCreate<THandler>() where THandler : IHandler<T, T>;
        public ICrudBuilder<T> AddRead<THandler, TId>() where THandler : IHandler<TId, T>;

        //public ICrudBuilder<T> AddUpdate<THandler, TKey>() where THandler : IHandler<(TKey, T), T>;
        //public ICrudBuilder<T> AddDelete<THandler, TKey>() where THandler : IHandler<TKey, object?>;
        //public ICrudBuilder<T> AddList<THandler>() where THandler : IHandler<object?, IEnumerable<T>>;
    }

    internal class CrudBuilder<T> : ICrudBuilder<T>
    {
        private readonly WebApplication _webApplication;
        private readonly string _pattern;
        private readonly string _id;

        public CrudBuilder(WebApplication webApplication, string pattern, string id)
        {
            _webApplication = webApplication;
            _pattern = pattern;
            _id = id;
        }

        public ICrudBuilder<T> AddCreate<THandler>() where THandler : IHandler<T, T>
        {
            // Need to think about this further, and does it even make any sense?
            // The current problem is that i some how need to merge the route value and the body into one request object - the current
            // deserialize does not support this and i would also like not to put to many constraints and interfaces down on to the
            // implementations by addding a CreateRequest<TId, T> or simular

            EndpointHandler.UseEndpointHandler<THandler>(_webApplication, _pattern, HttpMethod.Post, configure => configure.Produces<T>(StatusCodes.Status201Created));

            return this;
        }

        public ICrudBuilder<T> AddRead<THandler, TId>() where THandler : IHandler<TId, T>
        {
            //var specification = EndpointHandlerSpecification.Create<THandler>();
            //var handler = specification.BuildHandler();
            //
            //
            //
            //EndpointHandler.UseEndpointHandler<THandler>(_webApplication, _uri, HttpMethod.Get, configure => { });

            return this;
        }
    }   
}



