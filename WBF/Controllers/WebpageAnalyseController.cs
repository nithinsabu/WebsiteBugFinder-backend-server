using Microsoft.AspNetCore.Mvc;
using WBF.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;
namespace WBF.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class WebpageAnalyseController : ControllerBase
{
    private readonly IWebpageAnalyseService _webpageAnalyseService;
    // private readonly string _pythonServer = null!;
    private readonly ILogger<WebpageAnalyseController> _logger;
    private readonly HttpClient _pythonServer;

    public WebpageAnalyseController(IWebpageAnalyseService webpageAnalyseService, HttpClient pythonServer, ILogger<WebpageAnalyseController> logger)
    {
        _logger = logger;
        _webpageAnalyseService = webpageAnalyseService;
        _pythonServer = pythonServer;
        // _logger.LogInformation(_pythonServer.BaseAddress);
    }

    // [HttpGet("test")]
    // public async Task<string> Home()
    // {
    //     string x = await ForwardToLLM("hi");
    //     _logger.LogInformation(x);
    //     return x;
    // }
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
        if (!new EmailAddressAttribute().IsValid(email))
            return BadRequest("Invalid email");

        var user = await _webpageAnalyseService.GetUserByEmailAsync(email);
        if (user != null)
            return Ok(new { email = user.Email });

        return Unauthorized();
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(string email)
    {
        if (!new EmailAddressAttribute().IsValid(email))
            return BadRequest("Invalid email");

        string userId = await _webpageAnalyseService.CreateUserAsync(email);
        return Ok(new { userId = userId, email = email });
    }

    private async Task<string> ForwardToLLM(string htmlText, string? specification = null, Stream? designFile = null,  string designFileName = "designFile.png")
    {
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StringContent(htmlText), "htmlText");
            if (specification != null)
            {
                content.Add(new StringContent(specification), "specification");
            }

            if (designFile != null)
            {
                StreamContent designFileHttpContent = new StreamContent(designFile);
                content.Add(designFileHttpContent, "designFile", designFileName);
            }
            
            HttpResponseMessage response = await _pythonServer.PostAsync("/webpage-analysis", content);
            response.EnsureSuccessStatusCode();
            string responseString = await response.Content.ReadAsStringAsync();
            return responseString;
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromQuery] string email, [FromForm] IFormFile? htmlFile = null, string? url = null, [FromForm] IFormFile? designFile = null, [FromForm] IFormFile? specificationFile = null)
    {
        if (!new EmailAddressAttribute().IsValid(email) || email==null || (htmlFile==null && url==null))
            return BadRequest("Invalid email");
        return Ok("ok");
    }

    // [HttpGet("list-webpages")]
    // public async Task<IActionResult> ListWebpages([FromQuery] string email)
    // {

    // }
    // [HttpGet("view-webpage/{id}")]
    // public async Task<IActionResult> ViewWebpage(string id){
        
    // }
}
