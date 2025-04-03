using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

// Model for Upstox token response
public class UpstoxTokenResponse
{
    public string email { get; set; }
    public List<string> exchanges { get; set; }
    public List<string> products { get; set; }
    public string broker { get; set; }
    public string user_id { get; set; }
    public string user_name { get; set; }
    public List<string> order_types { get; set; }
    public string user_type { get; set; }
    public bool poa { get; set; }
    public bool ddpi { get; set; }
    public bool is_active { get; set; }
    public string access_token { get; set; }
    public string? extended_token { get; set; } // Nullable string for null value
}

public class UpstoxService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpstoxService> _logger;
    private List<Dictionary<string, object>> _cachedInstruments;
    private string _bankNiftyFutInstrumentKey;
    private string _bankNiftyFutTradingSymbol;

    // Configuration properties
    public string ApiKey => _config["Upstox:ApiKey"];
    public string ApiSecret => _config["Upstox:ApiSecret"];
    public string RedirectUri => _config["Upstox:RedirectUri"];

    public UpstoxService(IConfiguration config, HttpClient httpClient, ILogger<UpstoxService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cachedInstruments = null;
        _bankNiftyFutInstrumentKey = string.Empty;
        _bankNiftyFutTradingSymbol = string.Empty;
    }

    /// <summary>
    /// Generates a unique GUID for tracking requests.
    /// </summary>
    /// <returns>A string representation of the GUID.</returns>
    public string GenerateGuid()
    {
        var guid = Guid.NewGuid().ToString();
        _logger.LogInformation("Generated GUID: {Guid}", guid);
        return guid;
    }

    /// <summary>
    /// Fetches an access token from Upstox using the authorization code.
    /// </summary>
    /// <param name="code">The authorization code received from Upstox.</param>
    /// <returns>The access token as a string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the code is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if configuration values are missing.</exception>
    /// <exception cref="HttpRequestException">Thrown if the Upstox API request fails.</exception>
    public async Task<string> GetAccessTokenAsync(string code)
    {
        var guid = GenerateGuid();
        _logger.LogInformation("Fetching access token with GUID: {Guid}, Code: {Code}", guid, code);

        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentNullException(nameof(code), "Authorization code cannot be null or empty");
            }

            if (string.IsNullOrEmpty(ApiKey) || string.IsNullOrEmpty(ApiSecret) || string.IsNullOrEmpty(RedirectUri))
            {
                throw new InvalidOperationException($"Configuration missing: ApiKey={ApiKey}, ApiSecret={ApiSecret}, RedirectUri={RedirectUri}");
            }

            // Prepare the payload for the token request
            var payload = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", ApiKey),
                new KeyValuePair<string, string>("client_secret", ApiSecret),
                new KeyValuePair<string, string>("redirect_uri", RedirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            _logger.LogInformation("Sending request to Upstox with ApiKey: {ApiKey}, RedirectUri: {RedirectUri}", ApiKey, RedirectUri);
            var response = await _httpClient.PostAsync("https://api.upstox.com/v2/login/authorization/token", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Upstox API returned {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})");
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Upstox token response: {Response}", json);

            var tokenResponse = JsonSerializer.Deserialize<UpstoxTokenResponse>(json);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
            {
                throw new InvalidOperationException("Access token was not found in the response");
            }

            _logger.LogInformation("Access token fetched successfully with GUID: {Guid}", guid);
            return tokenResponse.access_token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch access token with GUID: {Guid}", guid);
            throw new Exception("Access token retrieval failed", ex);
        }
    }

    /// <summary>
    /// Fetches the list of instruments from Upstox and caches them.
    /// </summary>
    /// <returns>A list of instruments as dictionaries.</returns>
    /// <exception cref="HttpRequestException">Thrown if the Upstox API request fails.</exception>
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
            if (_cachedInstruments == null)
            {
                throw new InvalidOperationException("Failed to deserialize instruments");
            }

            // Find Bank Nifty Futures for April 2025
            var bankNiftyFut = _cachedInstruments.FirstOrDefault(inst =>
            {
                if (!inst.ContainsKey("expiry") || !inst.ContainsKey("instrument_type") || !inst.ContainsKey("trading_symbol"))
                {
                    return false;
                }

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

    // Properties for accessing Bank Nifty Futures data
    public string BankNiftyFutInstrumentKey => _bankNiftyFutInstrumentKey;
    public string BankNiftyFutTradingSymbol => _bankNiftyFutTradingSymbol;
}