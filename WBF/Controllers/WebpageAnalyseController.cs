using Microsoft.AspNetCore.Mvc;
using WBF.Services;
namespace WBF.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class WebpageAnalyseController : ControllerBase
{
    private readonly WebpageAnalyseService _webpageAnalyseService;

    // private readonly ILogger<WebpageAnalyseController> _logger;

    public WebpageAnalyseController(WebpageAnalyseService webpageAnalyseService)
    {
        _webpageAnalyseService = webpageAnalyseService;
    }

    private async Task<bool> ValidateEmail(string email)
    {
        var user = await _webpageAnalyseService.GetUserByEmailAsync(email);
        if (user != null)
        {
            return true;
        }
        return false;
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(string email)
    {
        var user = await _webpageAnalyseService.GetUserByEmailAsync(email);
        if (await ValidateEmail(email))
        {
            return Ok(new { email = email });
        }
        return Unauthorized();
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(string email)
    {

    }

    private async Task ForwardToLLM(string htmlText, Stream? designFile, Stream? specificationFile)
    {
        
    }
    
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile? htmlFile, string? url, [FromForm] IFormFile? designFile, [FromForm] IFormFile? specificationFile, [FromQuery] string email)
    {

    }

    [HttpGet("list-webpages")]
    public async Task<IActionResult> ListWebpages([FromQuery] string email)
    {

    }
    [HttpGet("view-webpage/{id}")]
    public async Task<IActionResult> ViewWebpage(string id){
        
    }
}
