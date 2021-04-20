using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Websocket.Service;

namespace Websocket.Infrastructure.Middleware
{
    public class WebsocketMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly WebsocketService _websocketService;

        public WebsocketMiddleware(RequestDelegate next, WebsocketService websocketService)
        {
            _next = next;

            _websocketService = websocketService;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync())
                    {
                        await _websocketService.Process(context, webSocket);
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}