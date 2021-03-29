﻿using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sisters.WudiLib.Posts;
using static Sisters.WudiLib.WebSocket.WebSocketUtility;

namespace Sisters.WudiLib.WebSocket
{
    /// <summary>
    /// 事件上报的 WebSocket 客户端。请注意，WebSocket 客户端暂不支持直接通过返回值响应。
    /// </summary>
    public class CqHttpWebSocketEvent : ApiPostListener
    {
        private readonly string _accessToken;
        private CancellationToken _cancellationToken;
        private readonly object _listenLock = new object();
        private Task _listenTask;

        /// <summary>
        /// 当前连接的 WebSocket 客户端。如果发生断线重连，则可能改变。
        /// </summary>
        protected System.Net.WebSockets.WebSocket WebSocket { get; private set; }

        /// <summary>
        /// 引发 <see cref="NotSupportedException"/>。
        /// </summary>
        /// <exception cref="NotSupportedException">不支持。</exception>
        public override string PostAddress
        {
            get => throw new NotSupportedException("WebSocket 不支持读取 PostAddress。");
            set => throw new NotSupportedException("WebSocket 不支持设置 PostAddress。");
        }

        /// <summary>
        /// 获取 uri。
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// 指示当前是否已启动监听。若要检查当前是否可用，请使用 <see cref="IsAvailable"/> 属性。
        /// </summary>
        public override bool IsListening => _listenTask?.IsCompleted == false;

        /// <summary>
        /// 获取当前是否能收到上报事件。注意自动重连过程中此项为 <c>false</c>，但无法再次通过 <see cref="StartListen()"/> 或 <see cref="StartListen(CancellationToken)"/> 连接。
        /// </summary>
        public virtual bool IsAvailable => WebSocket?.State == WebSocketState.Open;

        /// <summary>
        /// 构造通过 WebSocket 获取上报的监听客户端。
        /// </summary>
        /// <param name="uri">以 <c>ws://</c> 或者 <c>wss://</c> 开头的 uri，用于连接 WebSocket。</param>
        public CqHttpWebSocketEvent(string uri) => Uri = uri;

        /// <summary>
        /// 构造通过 WebSocket 获取上报的监听客户端。
        /// </summary>
        /// <param name="uri">以 <c>ws://</c> 或者 <c>wss://</c> 开头的 uri，用于连接 WebSocket。</param>
        /// <param name="accessToken">Access Token.</param>
        public CqHttpWebSocketEvent(string uri, string accessToken)
            : this(uri) => _accessToken = accessToken;

        /// <summary>
        /// 开始从 WebSocket 监听上报。
        /// </summary>
        /// <exception cref="Exception">连接失败等。</exception>
        public override void StartListen()
            => StartListen(default(CancellationToken));

        /// <summary>
        /// 开始从 WebSocket 监听上报。
        /// </summary>
        /// <param name="cancellationToken">一个 <see cref="CancellationToken"/> 应该被使用，以通知此操作应被取消。</param>
        /// <exception cref="Exception">连接失败等。</exception>
        public void StartListen(CancellationToken cancellationToken)
        {
            lock (_listenLock)
            {
                if (WebSocket != null && !_cancellationToken.IsCancellationRequested)
                {
                    throw new InvalidOperationException("已经有监听任务在执行！");
                }
                _cancellationToken = cancellationToken;
                InitializeWebSocket(cancellationToken);
            }
            _listenTask = Listening(cancellationToken);
        }

        private async Task Listening(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1024];
            var ms = new MemoryStream();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] eventArray;
                try
                {
                    var receiveResult = await WebSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    ms.Write(buffer, 0, receiveResult.Count);
                    if (!receiveResult.EndOfMessage) continue;
                    eventArray = ms.ToArray();
                }
                catch (Exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (WebSocket?.State != WebSocketState.Open)
                        {
                            (WebSocket as IDisposable).Dispose();
                            InitializeWebSocket(cancellationToken);
                        }
                    }
                    catch (Exception)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    ms = new MemoryStream();
                    continue;
                }

                _ = Task.Run(() => ProcessWSMessageAsync(eventArray));
                ms = new MemoryStream();
            }
        }

        private async Task ProcessWSMessageAsync(byte[] eventArray)
        {
            ForwardAsync(eventArray, Encoding.UTF8, null);
            string eventContent = Encoding.UTF8.GetString(eventArray);
            if (string.IsNullOrEmpty(eventContent))
                return;

            try
            {
                var response = ProcessPost(eventContent);
                var apiClient = ApiClient;
                if (response is RequestResponse && !(apiClient is null))
                {
                    JObject data;
                    data = JsonConvert.DeserializeObject<JObject>(eventContent);
                    data.Merge(JObject.FromObject(response));
                    switch (response)
                    {
                        case FriendRequestResponse friend:
                            await apiClient.HandleFriendRequestInternalAsync(data);
                            break;
                        case GroupRequestResponse group:
                            await apiClient.HandleGroupRequestInternalAsync(data);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                LogException(e, eventContent);
            }
        }

        private void InitializeWebSocket(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClientWebSocket clientWebSocket = CreateWebSocket(CreateUri(Uri, _accessToken), cancellationToken).GetAwaiter().GetResult();
            WebSocket = clientWebSocket;
        }
    }
}
