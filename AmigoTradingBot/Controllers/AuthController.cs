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
            var accessToken = await _upstoxService.GetAccessTokenAsync(request.Code);
            return Ok(new { accessToken });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to fetch access token", details = ex.Message });
        }
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