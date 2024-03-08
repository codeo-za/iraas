using System;

namespace IRAAS.Middleware
{
    public class NotImplementedExceptionMiddleware : 
        ExceptionHandlerMiddleware<NotImplementedException>
    {
        // see: https://www.gnu.org/fun/jokes/error-haiku.html
        public NotImplementedExceptionMiddleware(IAppSettings appSettings) :
            base(404,
@"The Web page you seek
cannot be located but
endless others exist", appSettings)
        {
        }
    }
}