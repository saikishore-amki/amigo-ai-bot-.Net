using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly UpstoxService _upstoxService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MarketController> _logger;

    public MarketController(UpstoxService upstoxService, HttpClient httpClient, ILogger<MarketController> logger)
    {
        _upstoxService = upstoxService;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches initial data for the frontend (spot price, indicators, index quotes).
    /// </summary>
    [HttpGet("initial-data")]
    public async Task<IActionResult> GetInitialData()
    {
        var guid = _upstoxService.GenerateGuid();
        var accessToken = Request.Headers["accesstoken"];
        _logger.LogInformation("Fetching initial data with GUID: {Guid}", guid);

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token missing for GUID: {Guid}", guid);
            return BadRequest(new { error = "Access token is required" });
        }

        try
        {
            await _upstoxService.GetInstrumentsAsync();
            if (string.IsNullOrEmpty(_upstoxService.BankNiftyFutInstrumentKey))
            {
                _logger.LogWarning("Bank Nifty Futures not found for GUID: {Guid}", guid);
                return NotFound(new { error = "Bank Nifty Futures contract not found" });
            }

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync($"https://api.upstox.com/v2/market-quote/ltp?instrument_key={_upstoxService.BankNiftyFutInstrumentKey}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            var instrumentData = (Dictionary<string, object>)data["data"];
            var spotPrice = Convert.ToDouble(((Dictionary<string, object>)instrumentData[_upstoxService.BankNiftyFutTradingSymbol])["last_price"]?.ToString());

            // Placeholder for indicators (to be calculated in TradeDecisionService)
            var indicators = new { bollingerBands = new { upper = 0, lower = 0 }, macd = new { macdLine = 0, signalLine = 0 }, rsi = 0, vwap = 0 };
            var indexQuotes = new[] { new { name = "Bank Nifty", ltp = spotPrice, change = 0 } };

            _logger.LogInformation("Initial data fetched with GUID: {Guid}", guid);
            return Ok(new { spotPrice, indicators, indexQuotes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch initial data with GUID: {Guid}", guid);
            return StatusCode(500, new { error = "Failed to fetch initial data", details = ex.Message });
        }
    }

    /// <summary>
    /// Fetches spot price for Bank Nifty futures.
    /// </summary>
    [HttpGet("spot-price")]
    public async Task<IActionResult> GetSpotPrice()
    {
        var guid = _upstoxService.GenerateGuid();
        var accessToken = Request.Headers["accesstoken"];
        _logger.LogInformation("Fetching spot price with GUID: {Guid}", guid);

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token missing for GUID: {Guid}", guid);
            return BadRequest(new { error = "Access token is required" });
        }

        try
        {
            await _upstoxService.GetInstrumentsAsync();
            if (string.IsNullOrEmpty(_upstoxService.BankNiftyFutInstrumentKey))
            {
                _logger.LogWarning("Bank Nifty Futures not found for GUID: {Guid}", guid);
                return NotFound(new { error = "Bank Nifty Futures contract not found" });
            }

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync($"https://api.upstox.com/v2/market-quote/ltp?instrument_key={_upstoxService.BankNiftyFutInstrumentKey}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            var instrumentData = (Dictionary<string, object>)data["data"];
            var spotPrice = Convert.ToDouble(((Dictionary<string, object>)instrumentData[_upstoxService.BankNiftyFutTradingSymbol])["last_price"]?.ToString());

            _logger.LogInformation("Spot price fetched: {SpotPrice} with GUID: {Guid}", spotPrice, guid);
            return Ok(new { spotPrice });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch spot price with GUID: {Guid}", guid);
            return StatusCode(500, new { error = "Failed to fetch spot price", details = ex.Message });
        }
    }

    /// <summary>
    /// Fetches all NSE instruments.
    /// </summary>
    [HttpGet("instruments")]
    public async Task<IActionResult> GetInstruments()
    {
        var guid = _upstoxService.GenerateGuid();
        _logger.LogInformation("Fetching instruments with GUID: {Guid}", guid);

        try
        {
            var instruments = await _upstoxService.GetInstrumentsAsync();
            _logger.LogInformation("Instruments fetched, count: {Count} with GUID: {Guid}", instruments.Count, guid);
            return Ok(instruments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch instruments with GUID: {Guid}", guid);
            return StatusCode(500, new { error = "Failed to fetch instruments", details = ex.Message });
        }
    }

    /// <summary>
    /// Fetches historical candle data for a given instrument.
    /// </summary>
    [HttpGet("historical-data")]
    public async Task<IActionResult> GetHistoricalData(
        [FromQuery] string instrument_key,
        [FromQuery] string interval,
        [FromQuery] string to_date,
        [FromQuery] string from_date)
    {
        var guid = _upstoxService.GenerateGuid();
        var accessToken = Request.Headers["accesstoken"];
        _logger.LogInformation("Fetching historical data with GUID: {Guid}", guid);

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token missing for GUID: {Guid}", guid);
            return BadRequest(new { error = "Access token is required" });
        }

        if (string.IsNullOrEmpty(instrument_key) || string.IsNullOrEmpty(interval) || string.IsNullOrEmpty(to_date) || string.IsNullOrEmpty(from_date))
        {
            _logger.LogWarning("Missing parameters for GUID: {Guid}", guid);
            return BadRequest(new { error = "Missing required parameters" });
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync($"https://api.upstox.com/v2/historical-candle/{instrument_key}/{interval}/{to_date}/{from_date}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            _logger.LogInformation("Historical data fetched with GUID: {Guid}", guid);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical data with GUID: {Guid}", guid);
            return StatusCode(500, new { error = "Failed to fetch historical data", details = ex.Message });
        }
    }

    /// <summary>
    /// Fetches live OHLC data for Bank Nifty futures.
    /// </summary>
    /// <summary>
    /// Fetches live OHLC data for Bank Nifty futures.
    /// </summary>
    [HttpGet("live-ohlc")]
    public async Task<IActionResult> GetLiveOhlc()
    {
        var guid = _upstoxService.GenerateGuid();
        var accessToken = Request.Headers["accesstoken"];
        _logger.LogInformation("Fetching live OHLC with GUID: {Guid}", guid);

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token missing for GUID: {Guid}", guid);
            return BadRequest(new { error = "Access token is required" });
        }

        try
        {
            await _upstoxService.GetInstrumentsAsync();
            if (string.IsNullOrEmpty(_upstoxService.BankNiftyFutInstrumentKey))
            {
                _logger.LogWarning("Bank Nifty Futures not found for GUID: {Guid}", guid);
                return NotFound(new { error = "Bank Nifty Futures contract not found" });
            }

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync($"https://api.upstox.com/v2/market-quote/ohlc?instrument_key={_upstoxService.BankNiftyFutInstrumentKey}&interval=5minute");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            var instrumentData = (Dictionary<string, object>)data["data"];
            var ohlcContainer = (Dictionary<string, object>)instrumentData[_upstoxService.BankNiftyFutTradingSymbol];
            var ohlcData = ohlcContainer["ohlc"] as List<object>;
            var latestOhlc = ohlcData?.Last() as Dictionary<string, object>;

            if (latestOhlc == null)
            {
                _logger.LogWarning("No OHLC data available for GUID: {Guid}", guid);
                return NotFound(new { error = "No OHLC data available" });
            }

            var formattedOhlc = new
            {
                open = Convert.ToDouble(latestOhlc["open"]),
                high = Convert.ToDouble(latestOhlc["high"]),
                low = Convert.ToDouble(latestOhlc["low"]),
                close = Convert.ToDouble(latestOhlc["close"]),
                volume = Convert.ToInt64(latestOhlc["volume"] ?? "0")
            };

            _logger.LogInformation("Live OHLC fetched with GUID: {Guid}", guid);
            return Ok(formattedOhlc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch live OHLC with GUID: {Guid}", guid);
            return StatusCode(500, new { error = "Failed to fetch live OHLC", details = ex.Message });
        }
    }
}