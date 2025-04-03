using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

public class UpstoxService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpstoxService> _logger;
    private List<Dictionary<string, object>> _cachedInstruments;
    private string _bankNiftyFutInstrumentKey;
    private string _bankNiftyFutTradingSymbol;

    public UpstoxService(IConfiguration config, HttpClient httpClient, ILogger<UpstoxService> logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    // Public properties to expose configuration values
    public string ApiKey => _config["Upstox:ApiKey"];
    public string ApiSecret => _config["Upstox:ApiSecret"];
    public string RedirectUri => _config["Upstox:RedirectUri"];

    /// <summary>
    /// Generates a unique GUID for tracking requests.
    /// </summary>
    public string GenerateGuid()
    {
        var guid = Guid.NewGuid().ToString();
        _logger.LogInformation("Generated GUID: {Guid}", guid);
        return guid;
    }

    /// <summary>
    /// Fetches access token from Upstox using authorization code.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(string code)
    {
        var guid = GenerateGuid();
        _logger.LogInformation("Fetching access token with GUID: {Guid}", guid);
        try
        {
            var payload = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", ApiKey),
                new KeyValuePair<string, string>("client_secret", ApiSecret),
                new KeyValuePair<string, string>("redirect_uri", RedirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            var response = await _httpClient.PostAsync("https://api.upstox.com/v2/login/authorization/token", payload);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<Dictionary<string, string>>(json)["access_token"];
            _logger.LogInformation("Access token fetched successfully with GUID: {Guid}", guid);
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch access token with GUID: {Guid}", guid);
            throw new Exception("Access token retrieval failed", ex);
        }
    }

    /// <summary>
    /// Fetches and caches NSE instruments, identifying Bank Nifty futures.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetInstrumentsAsync()
    {
        var guid = GenerateGuid();
        if (_cachedInstruments != null)
        {
            _logger.LogInformation("Returning cached instruments with GUID: {Guid}", guid);
            return _cachedInstruments;
        }

        _logger.LogInformation("Fetching instruments with GUID: {Guid}", guid);
        try
        {
            var response = await _httpClient.GetAsync("https://assets.upstox.com/market-quote/instruments/exchange/NSE.json.gz");
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = await reader.ReadToEndAsync();
            _cachedInstruments = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

            var bankNiftyFut = _cachedInstruments.FirstOrDefault(inst =>
            {
                var expiry = DateTime.Parse(inst["expiry"].ToString());
                var expiryStr = $"{expiry.Year}-{expiry.Month:00}";
                return inst["instrument_type"].ToString() == "FUT" &&
                       inst["trading_symbol"].ToString().Contains("BANKNIFTY") &&
                       expiryStr == "2025-04";
            });

            if (bankNiftyFut != null)
            {
                _bankNiftyFutInstrumentKey = bankNiftyFut["instrument_key"].ToString();
                var tradingSymbol = bankNiftyFut["trading_symbol"].ToString();
                var match = System.Text.RegularExpressions.Regex.Match(tradingSymbol, @"BANKNIFTY FUT (\d{2}) ([A-Z]{3}) (\d{2})");
                _bankNiftyFutTradingSymbol = match.Success
                    ? $"NSE_FO:BANKNIFTY{match.Groups[3]}{match.Groups[2]}FUT"
                    : $"NSE_FO:{tradingSymbol.Replace(" ", "")}";
                _logger.LogInformation("Bank Nifty Futures identified - InstrumentKey: {Key}, TradingSymbol: {Symbol}", _bankNiftyFutInstrumentKey, _bankNiftyFutTradingSymbol);
            }
            else
            {
                _logger.LogWarning("Bank Nifty Futures not found for April 2025 with GUID: {Guid}", guid);
            }

            return _cachedInstruments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch instruments with GUID: {Guid}", guid);
            throw new Exception("Instrument fetch failed", ex);
        }
    }

    public string BankNiftyFutInstrumentKey => _bankNiftyFutInstrumentKey;
    public string BankNiftyFutTradingSymbol => _bankNiftyFutTradingSymbol;
}