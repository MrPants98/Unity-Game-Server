﻿using System.Net;
using System.Net.WebSockets;
using Unity_Game_Server.Models;

namespace Unity_Game_Server
{
    public class WebsocketServer
    {
        public delegate void PacketReceivedHandler(byte[] packetData, WebSocket packetSender);

        public event PacketReceivedHandler OnPacketReceived;

        public string url { get; private set; }
        public UInt16 port { get; private set; }

        private HttpListener httpListener = new HttpListener();
        public List<WebsocketClient> connectedWebsocketClients { get; private set; }

        public async void InitServer()
        {
            string uri = "http://" + url + ":" + port + "/";
            httpListener.Prefixes.Add(uri);
            httpListener.Start();
            Console.WriteLine($"Server started. Join with {url}:{port}");

            while(true)
            {
                HttpListenerContext context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                    _ = Task.Run(() => HandleClientJoining(context));
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private async Task HandleClientJoining(HttpListenerContext context)
        {
            HttpListenerWebSocketContext socketContext = await context.AcceptWebSocketAsync(null);
            WebSocket webSocket = socketContext.WebSocket;
            WebsocketClient client = new WebsocketClient(webSocket, 0);

            connectedWebsocketClients.Add(client);
            //Console.WriteLine("New Client Connected");

            try
            {
                await ReceivePackets(webSocket);
            } catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            } finally
            {
                connectedWebsocketClients.Remove(client);
                //Console.WriteLine("A Client Disconnected");
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                webSocket.Dispose();
            }
        }

        private async Task ReceivePackets(WebSocket webSocket)
        {
            byte[] buffer = new byte[4096];
            var fullPacket = new List<byte>();

            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    fullPacket.AddRange(buffer.Take(result.Count));
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    OnPacketReceived?.Invoke(fullPacket.ToArray(), webSocket);
                }

                fullPacket.Clear();
            }
        }

        public WebsocketServer(string _url, UInt16 _port)
        {
            url = _url;
            port = _port;
            connectedWebsocketClients = new List<WebsocketClient>();
        }
    }
}