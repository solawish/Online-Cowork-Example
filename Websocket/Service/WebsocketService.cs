using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Common;
using Websocket.Models;

namespace Websocket.Service
{
    public class WebsocketService
    {
        private string _fakeStorageValue = "Start!";

        private Dictionary<string, WebSocket> _clients;

        public WebsocketService()
        {
            _clients = new Dictionary<string, WebSocket>();
        }

        /// <summary>
        /// Main Process
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="webSocket">The web socket.</param>
        public async Task Process(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            var clientId = Guid.NewGuid().ToString();
            _clients.TryAdd(clientId, webSocket);

            while (!result.CloseStatus.HasValue)
            {
                string data = Encoding.UTF8.GetString(buffer, 0, result.Count).TrimEnd('\0');
                string type = data.Substring(0, 1);

                if (type == ((int)CommandEnum.Get).ToString())
                {
                    //Get Current Value
                    await GetValue(webSocket, result);
                }
                else if (type == ((int)CommandEnum.Set).ToString())
                {
                    //Set Current Value
                    await SetValue(webSocket, result, data);
                }
                else
                {
                    //Only Echo
                    await Echo(webSocket, result, buffer);
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            _clients.Remove(clientId);
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            webSocket.Dispose();
        }

        /// <summary>
        /// Echo Whole Message.
        /// </summary>
        /// <param name="webSocket">The web socket.</param>
        /// <param name="result">The result.</param>
        /// <param name="buffer">The buffer.</param>
        public async Task Echo(WebSocket webSocket, WebSocketReceiveResult result, byte[] buffer)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
        }

        /// <summary>
        /// Gets the Storeage Value.
        /// </summary>
        /// <param name="webSocket">The web socket.</param>
        /// <param name="result">The result.</param>
        public async Task GetValue(WebSocket webSocket, WebSocketReceiveResult result)
        {
            var data = new MessageModel { Message = _fakeStorageValue };

            var buffer = Encoding.UTF8.GetBytes($"{(int)CommandEnum.Get}{JsonSerializer.Serialize(data)}");

            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
        }

        /// <summary>
        /// Sets the Storeage value.
        /// </summary>
        /// <param name="webSocket">The web socket.</param>
        /// <param name="result">The result.</param>
        /// <param name="data">The data.</param>
        public async Task SetValue(WebSocket webSocket, WebSocketReceiveResult result, string data)
        {
            _fakeStorageValue = JsonSerializer.Deserialize<MessageModel>(data.Substring(1)).Message;

            var buffer = Encoding.UTF8.GetBytes($"{(int)CommandEnum.Set}");

            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);

            await this.Broadcast(Encoding.UTF8.GetBytes($"{(int)CommandEnum.Get}{JsonSerializer.Serialize(new MessageModel { Message = _fakeStorageValue })}"));
        }

        /// <summary>
        /// Broadcasts Message To All Client.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        public async Task Broadcast(byte[] buffer)
        {
            foreach (var socket in _clients)
            {
                if (socket.Value.State != WebSocketState.Open)
                {
                    continue;
                }

                await socket.Value.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}