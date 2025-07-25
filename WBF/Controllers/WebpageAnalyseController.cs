using Microsoft.AspNetCore.Mvc;
using WBF.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using WBF.Models;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
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
        try
        {
            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");

            var userEmail = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userEmail != null)
                return Ok(new { email });

            return Unauthorized();
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Something went wrong: {e.Message}");
        }
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(string email)
    {
        try
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
        catch (Exception e)
        {
            return StatusCode(500, $"Something went wrong: {e.Message}");
        }
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

            return JsonSerializer.Deserialize<LLMResponse>(responseString) ?? null;
        }
    }

    private async Task<AxeCoreResponse?> ForwardToAxeCoreService(string? htmlText, string? url)
    {
        try
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
        catch
        {
            return null;
        }
    }

    private async Task<PageSpeedResponse?> ForwardToPageSpeedAPI(string url)
    {
        try
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

            return result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<NuValidatorMessage>?> ForwardToNuValidator(string? htmlText, string? url)
    {
        try
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

            var result = JsonSerializer.Deserialize<NuValidatorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Messages?.Where(m => m.Type == "error").ToList() ?? null;
        }
        catch
        {
            return null;
        }
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
            if (ext != ".txt" && ext != ".pdf")
                return BadRequest("Specification file must be .txt or .pdf");
        }

        if (designFile != null)
        {
            var ext = Path.GetExtension(designFile.FileName).ToLower();
            var validImageExts = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg" };
            if (!validImageExts.Contains(ext))
                return BadRequest("Design file must be an image");
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
            string? designFileId = null;
            string? specFileId = null;
            Task<string> taskHtmlFileId;
            Task<string> taskdesignFileId = Task.FromResult<string>(default!);
            Task<string> taskspecFileId = Task.FromResult<string>(default!);

            var htmlStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(htmlText));
            taskHtmlFileId = _webpageAnalyseService.UploadFileAsync(htmlStream, htmlFile?.FileName ?? "from-url.html");

            if (designFile != null)
                taskdesignFileId = _webpageAnalyseService.UploadFileAsync(designFile.OpenReadStream(), designFile.FileName);

            if (specificationFile != null)
                taskspecFileId = _webpageAnalyseService.UploadFileAsync(specificationFile.OpenReadStream(), specificationFile.FileName);



            Task<AxeCoreResponse?> taskAxeCoreResult = ForwardToAxeCoreService(url == null ? htmlText : null, url);
            Task<PageSpeedResponse?> taskPageSpeedResult = Task.FromResult<PageSpeedResponse?>(null);
            if (url != null)
            {
                taskPageSpeedResult = ForwardToPageSpeedAPI(url);
            }

            Task<List<NuValidatorMessage>?> taskNuValidatorResult = Task.FromResult<List<NuValidatorMessage>?>(null);
            if (htmlFile != null)
            {
                taskNuValidatorResult = ForwardToNuValidator(htmlText, null);
            }
            else
            {
                taskNuValidatorResult = ForwardToNuValidator(null, url);
            }
            // Await all tasks
            await Task.WhenAll(taskHtmlFileId, taskdesignFileId, taskspecFileId, taskAxeCoreResult, taskPageSpeedResult, taskNuValidatorResult);

            htmlFileId = await taskHtmlFileId;
            designFileId = await taskdesignFileId;
            specFileId = await taskspecFileId;
            AxeCoreResponse? axeCoreResult = await taskAxeCoreResult;
            PageSpeedResponse? pageSpeedResult = await taskPageSpeedResult;
            List<NuValidatorMessage>? nuValidatorResult = await taskNuValidatorResult;
            //set Errors
            bool axeCoreError = axeCoreResult == null;
            bool pageSpeedError = pageSpeedResult == null;
            bool nuValidatorError = nuValidatorResult == null;
            bool responsivenessError = axeCoreResult == null;
            htmlStream.Dispose();

            //LLM Query:
            WebAuditResults webAuditResults = new WebAuditResults { axeCoreResult = axeCoreResult?.violations, pageSpeedResult = pageSpeedResult, nuValidatorResult = nuValidatorResult, responsivenessResult = axeCoreResult?.responsivenessResults };

            LLMResponse? llmResult = null;
            bool llmError = true;
            try
            {
                string? specification = null;
                if (specificationFile != null)
                {
                    var ext = Path.GetExtension(specificationFile.FileName).ToLower();
                    if (ext == ".txt")
                    {
                        using var specReader = new StreamReader(specificationFile.OpenReadStream());
                        specification = await specReader.ReadToEndAsync();
                    }
                    else if (ext == ".pdf")
                    {
                        specification = await ExtractTextFromPdfAsync(specificationFile);
                    }
                    _logger.LogInformation(specification);
                }
                llmResult = await ForwardToLLM(htmlText: htmlText, specification: specification, designFile: designFile?.OpenReadStream(), designFileName: designFile?.FileName ?? "designFile.png", designFileType: designFile?.ContentType, webAuditResults: webAuditResults);
                llmError = false;
            }
            catch (Exception e)
            {
                llmResult = null;
                _logger.LogInformation(e.Message);
            }
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
                webAuditResults = webAuditResults,
                AxeCoreError = axeCoreError,
                PageSpeedError = pageSpeedError,
                LLMError = llmError,
                NuValidatorError = nuValidatorError,
                ResponsivenessError = responsivenessError
            };

            var webpageId = await _webpageAnalyseService.CreateWebpageAndAnalysisResultAsync(webpage, webpageAnalysisResult);
            _logger.LogInformation(webpageId);
            return Ok(webpageId);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Error during upload.");
            return StatusCode(500, $"Something went wrong: {error.Message}");
        }
    }
    private async Task<string> ExtractTextFromPdfAsync(IFormFile pdfFile)
    {
        using var memoryStream = new MemoryStream();
        await pdfFile.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var document = PdfDocument.Open(memoryStream);
        var text = new StringWriter();

        foreach (var page in document.GetPages())
        {
            text.WriteLine(page.Text);
        }

        return text.ToString();
    }

    [HttpGet("list-webpages")]
    public async Task<IActionResult> ListWebpages([FromQuery] string email)
    {
        try
        {
            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");

            var userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");

            var list = await _webpageAnalyseService.ListWebpagesAsync(userId);
            return Ok(list);
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Something went wrong: {e.Message}");
        }
    }
    [HttpGet("view-webpage/{id}")]
    public async Task<IActionResult> ViewWebpage(string id, [FromQuery] string email)
    {
        try
        {
            var userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");

            var (htmlContent, webpageAnalysisResult) = await _webpageAnalyseService.GetWebpageContentAndAnalysisAsync(id);
            if (htmlContent == null)
                return NotFound("Webpage or HTML content not found");
            // _logger.LogInformation(JsonSerializer.Serialize(webpageAnalysisResult));

            return Ok(new { htmlContent, webpageAnalysisResult });
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Something went wrong: {e.Message}");
        }
    }

    [HttpGet("download-designfile/{webpageId}")]
    public async Task<IActionResult> DownloadDesignFile(string webpageId, [FromQuery] string email)
    {
        try
        {
            if (email == null) return BadRequest("Email cannot be null");
            string userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");
            if (webpageId == null) return BadRequest("WebpageId cannot be null");

            Webpage webpage = await _webpageAnalyseService.GetWebpageAsync(webpageId, userId);
            if (webpage == null) return NotFound("Webpage not found");
            string? designFileId = webpage.DesignFileId;
            if (designFileId == null) return NotFound("Design File was not uploaded.");

            (Stream stream, string fileName) = await _webpageAnalyseService.DownloadFileAsync(designFileId);
            if (stream == null || fileName == null) throw new Exception("Couldn't find the file even though the file was uploaded.");
            var provider = new FileExtensionContentTypeProvider();
            string contentType;

            if (!provider.TryGetContentType(fileName, out contentType))
            {
                contentType = "application/octet-stream"; // fallback
            }
            // var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(stream, contentType, fileName);
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Something went wrong: {e.Message}");
        }
    }

    [HttpGet("download-specifications/{webpageId}")]
    public async Task<IActionResult> DownloadSpecifications(string webpageId, [FromQuery] string email)
    {
        try
        {
            if (email == null) return BadRequest("Email cannot be null");
            string userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");
            if (webpageId == null) return BadRequest("WebpageId cannot be null");

            Webpage webpage = await _webpageAnalyseService.GetWebpageAsync(webpageId, userId);
            if (webpage == null) return NotFound("Webpage not found");
            string? specificationFileId = webpage.SpecificationFileId;
            if (specificationFileId == null) return NotFound("Specification File was not uploaded.");

            (Stream stream, string fileName) = await _webpageAnalyseService.DownloadFileAsync(specificationFileId);
            if (stream == null || fileName == null) throw new Exception("Couldn't find the file even though the file was uploaded.");

            string content;
            if (Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var pdf = PdfDocument.Open(memoryStream);
                var textBuilder = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    textBuilder.AppendLine(page.Text);
                }

                content = textBuilder.ToString();
            }
            else
            {
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync();
            }
            return Ok(new { content });
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Something went wrong: {e.Message}");
        }
    }

    
}
