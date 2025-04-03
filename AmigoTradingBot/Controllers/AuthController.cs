using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UpstoxService _upstoxService;

    public AuthController(UpstoxService upstoxService)
    {
        _upstoxService = upstoxService;
    }

    [HttpPost("get-access-token")]
public async Task<IActionResult> GetAccessToken([FromBody] AccessTokenRequest request)
{
    try
    {
        if (request == null || string.IsNullOrEmpty(request.Code))
        {
            Console.WriteLine("Invalid request: Code is missing");
            return BadRequest(new { error = "Code is required" });
        }
        Console.WriteLine($"Received POST to get-access-token with code: {request.Code}");
        var accessToken = await _upstoxService.GetAccessTokenAsync(request.Code);
        return Ok(new { accessToken });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in GetAccessToken: {ex.Message}, StackTrace: {ex.StackTrace}");
        return StatusCode(500, new { error = "Failed to fetch access token", details = ex.Message });
    }
}
    [HttpGet("get-access-token")]
    public IActionResult GetAccessTokenGet()
    {
        var queryString = Request.QueryString.ToString();
        var headers = Request.Headers.ToString();
        Console.WriteLine($"Unexpected GET request to /api/auth/get-access-token. Query: {queryString}, Headers: {headers}");
        return BadRequest(new { error = "This endpoint requires a POST request with a JSON body containing 'code'." });
    }

    [HttpGet("login-url")]
    public IActionResult GetLoginUrl()
    {
        var authUrl = $"https://api.upstox.com/v2/login/authorization/dialog?client_id={_upstoxService.ApiKey}&redirect_uri={_upstoxService.RedirectUri}&response_type=code&state=amigo";
        return Ok(new { loginUrl = authUrl });
    }
}

public class AccessTokenRequest
{
    public string Code { get; set; }
}