using Microsoft.AspNetCore.Mvc;
using WBF.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using WBF.Models;
using Microsoft.Extensions.Options;

namespace WBF.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class WebpageAnalyseController : ControllerBase
{
    private readonly IWebpageAnalyseService _webpageAnalyseService;
    // private readonly string _pythonServer = null!;
    private readonly ILogger<WebpageAnalyseController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _PageSpeedAPI_API_KEY;
    public WebpageAnalyseController(IWebpageAnalyseService webpageAnalyseService, IHttpClientFactory httpClientFactory, ILogger<WebpageAnalyseController> logger, IOptions<PageSpeedAPIConfig> pageSpeedAPIConfig)
    {
        _logger = logger;
        _webpageAnalyseService = webpageAnalyseService;
        _httpClientFactory = httpClientFactory;
        _PageSpeedAPI_API_KEY = pageSpeedAPIConfig.Value.API_KEY;
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

    private async Task<LLMResponse?> ForwardToLLM(string htmlText, string? specification = null, Stream? designFile = null, string designFileName = "designFile.png", string? designFileType = "image/png", WebAuditResults? webAuditResults = null)
    //throws error if failed. Should be handled by the function that is calling this. it is understood that the input format is what is required.
    {
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StringContent(htmlText), "htmlText");
            if (specification != null)
            {
                content.Add(new StringContent(specification), "specification");
            }

            if (designFile != null && designFileType != null)
            {
                StreamContent designFileHttpContent = new StreamContent(designFile);
                designFileHttpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(designFileType);

                content.Add(designFileHttpContent, "designFile", designFileName);
            }
            if (webAuditResults != null)
            {
                content.Add(new StringContent(JsonSerializer.Serialize(webAuditResults)), "webAuditResults");
            }
            var pythonServer = _httpClientFactory.CreateClient("PythonServer");
            HttpResponseMessage response = await pythonServer.PostAsync("/webpage-analysis", content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"{response.StatusCode}: {responseString}");
            }

            return JsonSerializer.Deserialize<LLMResponse>(responseString)?? null;
        }
    }

    private async Task<AxeCoreResponse?> ForwardToAxeCoreService(string? htmlText, string? url)
    {
        if (htmlText != null && url != null || htmlText == null && url == null) throw new Exception("Mention either one of htmlText or url.");
        var client = _httpClientFactory.CreateClient("AxeCore");
        JsonContent content;
        // string queryParameters = (url==null)? "?lightHouseRequired=true" : "";
        string queryParameters = "";
        if (url == null) content = JsonContent.Create(new { html = htmlText });
        else content = JsonContent.Create(new { url = url });

        var response = await client.PostAsync($"/analyse{queryParameters}", content);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{response.StatusCode}: {responseString}");
        }
        // _logger.LogInformation(responseString);
        var result = JsonSerializer.Deserialize<AxeCoreResponse>(responseString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? null;
    }

    private async Task<PageSpeedResponse?> ForwardToPageSpeedAPI(string url)
    {
        var client = _httpClientFactory.CreateClient("PageSpeedAPI");

        var encodedUrl = Uri.EscapeDataString(url);
        var query = $"?key={_PageSpeedAPI_API_KEY}&url={encodedUrl}&category=performance&category=accessibility&category=seo&category=best-practices";

        var response = await client.GetAsync(query);
        var responseString = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{response.StatusCode}: {responseString}");
        }
        // _logger.LogInformation(responseString.Substring(0, 100));
        var result = JsonSerializer.Deserialize<PageSpeedResponse>(responseString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? null;
    }

    private async Task<List<NuValidatorMessage>> ForwardToNuValidator(string? htmlText, string? url)
    {
        var client = _httpClientFactory.CreateClient("NuValidator");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("fad/1.0");
        string responseString = "";
        if (htmlText != null)
        {
            using var content = new StringContent(htmlText, System.Text.Encoding.UTF8, "text/html");
            var response = await client.PostAsync("?out=json", content);
            responseString = await response.Content.ReadAsStringAsync();
            // _logger.LogInformation(responseString);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"{response.StatusCode}: {responseString}");
            }
        }
        if (url != null)
        {
            url = Uri.EscapeDataString(url);
            var response = await client.GetAsync($"?doc={url}&out=json");
            responseString = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"{response.StatusCode}: {responseString}");
            }
        }

        // _logger.LogInformation(responseString);


        var result = JsonSerializer.Deserialize<NuValidatorResponse>(responseString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result?.Messages?.Where(m => m.Type == "error").ToList() ?? new();
    }


    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] string? name, [FromQuery] string email, IFormFile? htmlFile = null, [FromForm] string? url = null, IFormFile? designFile = null, IFormFile? specificationFile = null)
    {
        // _logger.LogInformation(url);
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
            //Read HTML
            string htmlText;
            if (htmlFile != null)
            {
                using var reader = new StreamReader(htmlFile.OpenReadStream());
                htmlText = await reader.ReadToEndAsync();
            }
            else
            {
                var pythonServer = _httpClientFactory.CreateClient("PythonServer");
                try
                {
                    htmlText = await pythonServer.GetStringAsync(url!);

                }
                catch
                {
                    throw new Exception("Invalid or broken URL.");
                }
            }


            
            //Upload Files
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

            //axe-core api
            AxeCoreResponse? axeCoreResult = null;
            try
            {
                axeCoreResult = await ForwardToAxeCoreService(url==null? htmlText: null, url);
            }
            catch
            {
                axeCoreResult = null;
            }

            //pagespeed api
            PageSpeedResponse? pageSpeedResult = null;
            try
            {
                if (url != null)
                {
                    pageSpeedResult = await ForwardToPageSpeedAPI(url);
                }
            }
            catch
            {
                pageSpeedResult = null;
            }

            //Nu validator
            List<NuValidatorMessage> nuValidatorResult = new();
            try
            {
                if (htmlFile != null)
                {
                    nuValidatorResult = await ForwardToNuValidator(htmlText, null);
                }
                else
                {
                    nuValidatorResult = await ForwardToNuValidator(null, url);
                }
            }
            catch
            {
                nuValidatorResult = new();
            }

            //LLM Query:
            WebAuditResults webAuditResults = new WebAuditResults{axeCoreResult = axeCoreResult?.violations, pageSpeedResult = pageSpeedResult, nuValidatorResult=nuValidatorResult, responsivenessResult = axeCoreResult?.responsivenessResults};

            LLMResponse? llmResult = null;
            try
            {
                string? specification = null;
                if (specificationFile != null)
                {
                    using var specReader = new StreamReader(specificationFile.OpenReadStream());
                    specification = await specReader.ReadToEndAsync();
                }
                llmResult = await ForwardToLLM(htmlText: htmlText, specification: specification, designFile: designFile?.OpenReadStream(), designFileName: designFile?.FileName ?? "designFile.png", designFileType: designFile?.ContentType, webAuditResults: webAuditResults);
            }
            catch
            {
                llmResult = null;
            }
            // return new JsonResult(new
            // {
            //     llmResult,
            //     axeCoreResult,
            //     pageSpeedResult,
            //     nuValidatorResult
            // });

            // Create models
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
            var webpageAnalysisResult = new WebpageAnalysisResult
            {
                LLMResponse = llmResult,
                webAuditResults = webAuditResults
            };

            var webpageId = await _webpageAnalyseService.CreateWebpageAndAnalysisResultAsync(webpage, webpageAnalysisResult);
            _logger.LogInformation(webpageId);
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

        var (htmlContent, webpageAnalysisResult) = await _webpageAnalyseService.GetWebpageContentAndAnalysisAsync(id);
        if (htmlContent == null)
            return NotFound("Webpage or HTML content not found");

        return Ok(new { htmlContent, webpageAnalysisResult });
    }
}
