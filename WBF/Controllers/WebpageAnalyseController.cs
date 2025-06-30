using Microsoft.AspNetCore.Mvc;
using WBF.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;
using WBF.Models;
namespace WBF.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class WebpageAnalyseController : ControllerBase
{
    private readonly IWebpageAnalyseService _webpageAnalyseService;
    // private readonly string _pythonServer = null!;
    private readonly ILogger<WebpageAnalyseController> _logger;
    private readonly IHttpClientFactory _pythonServerFactory;

    public WebpageAnalyseController(IWebpageAnalyseService webpageAnalyseService, IHttpClientFactory _httpClientFactory, ILogger<WebpageAnalyseController> logger)
    {
        _logger = logger;
        _webpageAnalyseService = webpageAnalyseService;
        _pythonServerFactory = _httpClientFactory;
        // _logger.LogInformation(_pythonServer.BaseAddress);
    }

    // [HttpGet("test")]
    // public async Task<string> Home()
    // {
    //     var pythonServer = _pythonServerFactory.CreateClient("PythonServer");
    //     string x = "fefe";
    //     _logger.LogInformation(pythonServer.BaseAddress?.ToString());
    //     return x;
    // }
    // private async Task<bool> ValidateEmail(string email)
    // {
    //     return await _webpageAnalyseService.GetUserByEmailAsync(email)!=null;
    // }
    [HttpPost("login")]
    public async Task<IActionResult> Login(string email)
    {
        if (!new EmailAddressAttribute().IsValid(email))
            return BadRequest("Invalid email");

        var userEmail = await _webpageAnalyseService.GetUserByEmailAsync(email);
        if (userEmail != null)
            return Ok(new { email });

        return Unauthorized();
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(string email)
    {
        if (!new EmailAddressAttribute().IsValid(email))
            return BadRequest("Invalid email");

        string? userId = await _webpageAnalyseService.CreateUserAsync(email);
        if (userId == null)
        {
            return BadRequest("Email already exists");
        }
        return Ok(new { userId = userId, email = email });
    }

    private async Task<string> ForwardToLLM(string htmlText, string? specification = null, Stream? designFile = null, string designFileName = "designFile.png")
    //throws error if failed. Should be handled by the function that is calling this. it is understood that the input format is what is required.
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
            var pythonServer = _pythonServerFactory.CreateClient("PythonServer");
            HttpResponseMessage response = await pythonServer.PostAsync("/webpage-analysis", content);
            response.EnsureSuccessStatusCode();
            string responseString = await response.Content.ReadAsStringAsync();
            return responseString;
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] string? name, [FromQuery] string email, IFormFile? htmlFile = null, [FromForm] string? url = null, IFormFile? designFile = null, IFormFile? specificationFile = null)
    {

        if (!new EmailAddressAttribute().IsValid(email))
            return BadRequest("Invalid email");

        if (htmlFile == null && string.IsNullOrWhiteSpace(url))
            return BadRequest("Either HTML file or URL is required");

        if (htmlFile != null && !string.IsNullOrWhiteSpace(url))
            return BadRequest("Provide either HTML file or URL, not both");

        if (htmlFile != null && Path.GetExtension(htmlFile.FileName).ToLower() != ".html")
            return BadRequest("HTML file must have .html extension");

        if (specificationFile != null)
        {
            var ext = Path.GetExtension(specificationFile.FileName).ToLower();
            if (ext != ".txt")
                return BadRequest("Specification file must be .txt");
        }

        if (designFile != null)
        {
            var ext = Path.GetExtension(designFile.FileName).ToLower();
            var validImageExts = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".fig" };
            if (!validImageExts.Contains(ext))
                return BadRequest("Design file must be an image or .fig (Figma)");
        }

        var userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
        if (userId == null)
            return Unauthorized("Please Sign up");
        try
        {
            string htmlText;

            if (htmlFile != null)
            {
                using var reader = new StreamReader(htmlFile.OpenReadStream());
                htmlText = await reader.ReadToEndAsync();
            }
            else
            {
                var pythonServer = _pythonServerFactory.CreateClient("PythonServer");
                htmlText = await pythonServer.GetStringAsync(url!);
            }

            string? specification = null;
            if (specificationFile != null)
            {
                using var specReader = new StreamReader(specificationFile.OpenReadStream());
                specification = await specReader.ReadToEndAsync();
            }

            var result = await ForwardToLLM(
                htmlText: htmlText,
                specification: specification,
                designFile: designFile?.OpenReadStream(),
                designFileName: designFile?.FileName ?? "designFile.png"
            );
            _logger.LogInformation(result);

            string htmlFileId;
            using (var htmlStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(htmlText)))
            {
                htmlFileId = await _webpageAnalyseService.UploadFileAsync(htmlStream, htmlFile?.FileName ?? "from-url.html");
            }

            string? designFileId = null;
            if (designFile != null)
            {
                designFileId = await _webpageAnalyseService.UploadFileAsync(designFile.OpenReadStream(), designFile.FileName);
            }

            string? specFileId = null;
            if (specificationFile != null)
            {
                specFileId = await _webpageAnalyseService.UploadFileAsync(specificationFile.OpenReadStream(), specificationFile.FileName);
            }

            // 7. Create models
            var webpage = new Webpage
            {
                UserId = userId!,
                HtmlContentId = htmlFileId,
                Url = url,
                FileName = htmlFile?.FileName,
                Name = string.IsNullOrWhiteSpace(name) ? htmlFile?.FileName ?? url : name,
                DesignFileId = designFileId,
                SpecificationFileId = specFileId,
                UploadDate = DateTime.UtcNow
            };

            var llmFeedback = new LLMFeedback
            {
                LLMResponse = result
            };

            var webpageId = await _webpageAnalyseService.CreateWebpageAndLLMFeedbackAsync(webpage, llmFeedback);

            return Ok(webpageId);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Error during upload.");
            return BadRequest(error.Message);
        }
    }

    [HttpGet("list-webpages")]
    public async Task<IActionResult> ListWebpages([FromQuery] string email)
    {
        if (!new EmailAddressAttribute().IsValid(email))
            return BadRequest("Invalid email");

        var userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
        if (userId == null)
            return Unauthorized("Please Sign up");

        var list = await _webpageAnalyseService.ListWebpagesAsync(userId);
        return Ok(list);
    }
    [HttpGet("view-webpage/{id}")]
    public async Task<IActionResult> ViewWebpage(string id, [FromQuery] string email)
    {
        var userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
        if (userId == null)
            return Unauthorized("Please Sign up");

        var (htmlContent, llmResponse) = await _webpageAnalyseService.GetWebpageContentAndLLMAsync(id);
        if (htmlContent == null)
            return NotFound("Webpage or HTML content not found");

        return Ok(new { htmlContent, llmResponse });
    }
}
