using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json; // Added this namespace
using System.Threading.Tasks;

public class MarketDataHub : Hub
{
    private readonly UpstoxService _upstoxService;
    private readonly ILogger<MarketDataHub> _logger;

    public MarketDataHub(UpstoxService upstoxService, ILogger<MarketDataHub> logger)
    {
        _upstoxService = upstoxService;
        _logger = logger;
    }

    /// <summary>
    /// Handles client connection, proxies Upstox WebSocket data.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var accessToken = Context.GetHttpContext()?.Request.Query["access_token"];
        var guid = _upstoxService.GenerateGuid();
        _logger.LogInformation("Client connected with GUID: {Guid}", guid);

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token missing for GUID: {Guid}", guid);
            Context.Abort();
            return;
        }

        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var authResponse = await httpClient.GetAsync("https://api.upstox.com/v2/feed/market-data-feed/authorize");
            authResponse.EnsureSuccessStatusCode();
            var authJson = await authResponse.Content.ReadAsStringAsync();
            var authData = JsonSerializer.Deserialize<Dictionary<string, object>>(authJson);
            var wsUrl = authData["data"].ToString();

            using var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
            _logger.LogInformation("Connected to Upstox WebSocket with GUID: {Guid}", guid);

            // Multi-threaded data forwarding
            _ = Task.Run(async () =>
            {
                var buffer = new byte[1024 * 4];
                while (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        await Clients.Caller.SendAsync("ReceiveMarketData", buffer[..result.Count]);
                        _logger.LogDebug("Forwarded market data with GUID: {Guid}", guid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error forwarding market data with GUID: {Guid}", guid);
                        break;
                    }
                }
            });

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Upstox WebSocket with GUID: {Guid}", guid);
            Context.Abort();
        }
    }
}