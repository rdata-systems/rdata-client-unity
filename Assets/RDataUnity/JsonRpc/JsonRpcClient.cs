﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using WebSocketSharp;

namespace RData.JsonRpc
{
    public class JsonRpcClient : IJsonRpcClient
    {
        private string _hostName;

        private WebSocket _webSocket;

        private Dictionary<string, string> _responses = new Dictionary<string, string>();
        
        private float _reconnectTimeout = 5;

        private bool _closed = false;

        private volatile bool _lostConnection = false;
        
        public event Action OnReconnected;

        public bool IsAvailable
        {
            get { return _webSocket != null && _webSocket.IsAlive; }
        }
        
        public IEnumerator Open(string hostName, bool waitUntilConnected = true, double waitTimeout = 3d)
        {
            _hostName = hostName;
            _closed = false;

            CoroutineManager.StartCoroutine(WebsocketConnectionObserver());

            if (waitUntilConnected)
            {
                var now = DateTime.UtcNow;
                while (!IsAvailable && DateTime.UtcNow < now + TimeSpan.FromSeconds(waitTimeout))
                {
                    yield return null;
                }
            }
        }

        public IEnumerator WebsocketConnectionObserver()
        {
            using (_webSocket = new WebSocket(_hostName))
            {
                _webSocket.Log.Output = Log;

                _webSocket.OnOpen += OnWebsocketConnected;
                _webSocket.OnMessage += OnWebsocketMessage;
                _webSocket.OnClose += OnWebsocketDisconnected;
                _webSocket.OnError += OnWebsocketError;

                _webSocket.ConnectAsync();

                while (true)
                {
                    if (_closed)
                        yield break;

                    if(_lostConnection)
                    {
                        Debug.Log("Must reconnect. Reconnecting...");
                        _webSocket.ConnectAsync();

                        var now = DateTime.UtcNow;
                        while (!IsAvailable && DateTime.UtcNow < now + TimeSpan.FromSeconds(_reconnectTimeout))
                        {
                            yield return null;
                        }

                        if (IsAvailable)
                        {
                            _lostConnection = false;
                            Debug.Log("Successfully reconencted to websocket server.");

                            if (OnReconnected != null)
                                OnReconnected();
                        }
                    }
                    yield return null;
                }
            }

            // Any cleanup needed goes here
            Debug.Log("Websocket object destroyed");
        }

        public IEnumerator Close()
        {
            CloseImmidiately();

            while (IsAvailable)
            {
                yield return null;
            }
        }
        
        public void CloseImmidiately()
        {
            _closed = true;
            _webSocket.CloseAsync();
        }

        public IEnumerator Send<TRequest, TResponse>(TRequest request)
            where TRequest : JsonRpcBaseRequest
            where TResponse : JsonRpcBaseResponse
        {
            string id = request.Id;
            string message = LitJson.JsonMapper.ToJson(request);
            lock (_responses)
            {
                _responses.Add(id, null);
            }
            _webSocket.SendAsync(message, b => { });

            while (true)
            {
                lock (_responses)
                {
                    if(_responses.ContainsKey(id))
                    {
                        var responseJson = _responses[id];
                        request.SetResponse(LitJson.JsonMapper.ToObject<TResponse>(responseJson));
                        _responses.Remove(id);
                        yield break;
                    }
                }
                yield return null;
            }
        }
        
        public IEnumerator SendJson<TResponse>(string message, string id, Action<TResponse> onResponse)
            where TResponse : JsonRpcBaseResponse
        {
            lock (_responses)
            {
                _responses.Add(id, null);
            }
            _webSocket.SendAsync(message, b => { });

            while (true)
            {
                lock (_responses)
                {
                    if (_responses.ContainsKey(id))
                    {
                        var responseJson = _responses[id];
                        _responses.Remove(id);
                        if (onResponse != null)
                            onResponse(LitJson.JsonMapper.ToObject<TResponse>(responseJson));

                        yield break;
                    }
                }
                yield return null;
            }
        }
        
        private void OnWebsocketConnected(object sender, EventArgs e)
        {
            Debug.Log("Websocket connected");
        }

        private void OnWebsocketDisconnected(object sender, CloseEventArgs e)
        {
            _lostConnection = true;
            Debug.Log("Websocket disconnected");
        }

        private void OnWebsocketError(object sender, ErrorEventArgs e)
        {
            Debug.Log("WebSocket error: " + e.Message);
        }

        private void OnWebsocketMessage(object sender, MessageEventArgs e)
        {
            var response = LitJson.JsonMapper.ToObject<JsonRpcBaseResponse>(e.Data);
            lock (_responses)
            {
                if (!_responses.ContainsKey(response.Id))
                    throw new JsonRpcException("Response with that id wasn't expected");

                _responses[response.Id] = e.Data;
            }
        }

        private void Log(LogData logData, string message)
        {
            if (logData.Level == LogLevel.Debug || logData.Level == LogLevel.Info || logData.Level == LogLevel.Trace)
                Debug.Log(logData.Message);
            else if (logData.Level == LogLevel.Warn)
                Debug.LogWarning(logData.Message);
            else if (logData.Level == LogLevel.Error || logData.Level == LogLevel.Fatal)
                Debug.LogError(logData.Message);
        }
    }
}