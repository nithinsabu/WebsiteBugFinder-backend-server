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
    }

    public class LoginResponse
    {
        public string Email { get; set; }
    }
    /// <summary>
    /// Logs in a user by validating their email address.
    /// </summary>
    /// <param name="email">The email address of the user attempting to log in.</param>
    /// <returns>
    /// A <see cref="LoginResponse"/> containing the user's email if login is successful.
    /// </returns>
    /// <response code="200">The user exists and login is successful.</response>
    /// <response code="400">The email is null, empty, too long, or in an invalid format.</response>
    /// <response code="401">The email is valid but no matching user exists.</response>
    /// <response code="503">A server or database error occurred while processing the request.</response>
    /// <exception cref="ValidationException">
    /// Thrown when the email is null, empty, too long, or invalid.
    /// </exception>
    /// <remarks>
    /// This endpoint validates the provided email against existing user records.
    /// </remarks>
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email cannot be Null");
            if (email.Length > 100) return BadRequest("Email is too long");
            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");

            var userEmail = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userEmail != null)
                return Ok(new LoginResponse { Email = email });

            return Unauthorized();
        }
        catch (Exception e)
        {
            _logger.LogError($"Error in Login: {e.Message}");
            return StatusCode(503, $"Something went wrong.");
        }
    }

    public class SignupResponse
    {
        public string UserId { get; set; }
        public string Email { get; set; }
    }

    /// <summary>
    /// Registers a new user with the given email address.
    /// </summary>
    /// <param name="email">The email address to register for the new user.</param>
    /// <returns>
    /// A <see cref="SignupResponse"/> containing the user ID and email if registration is successful.
    /// </returns>
    /// <response code="200">Registration succeeded; returns the new user's ID and email.</response>
    /// <response code="400">The email is null, empty, too long, invalid, or already exists.</response>
    /// <response code="503">A server or database error occurred while processing the registration.</response>
    /// <exception cref="ValidationException">
    /// Thrown when the email is null, empty, too long, or invalid.
    /// </exception>
    /// <remarks>
    /// This endpoint validates the provided email and attempts to create a new user record.
    /// </remarks>
    [ProducesResponseType(typeof(SignupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [HttpPost("signup")]
    public async Task<IActionResult> Signup(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email cannot be Null");
            if (email.Length > 100) return BadRequest("Email is too long");
            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");

            string? userId = await _webpageAnalyseService.CreateUserAsync(email);
            if (userId == null)
            {
                return BadRequest("Email already exists");
            }
            return Ok(new SignupResponse { UserId = userId, Email = email });
        }
        catch (Exception e)
        {
            _logger.LogError($"Error in Signup: {e.Message}");
            return StatusCode(503, $"Something went wrong.");
        }
    }

    private async Task<LLMResponse?> ForwardToLLM(string htmlText, string? specification = null, Stream? designFile = null, string designFileName = "designFile.png", string? designFileType = "image/png", WebAuditResults? webAuditResults = null)
    //throws error if failed. Should be handled by the function that is calling this. it is understood that the input format is what is required.
    {
        try
        {
            using var content = new MultipartFormDataContent();

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
        catch (Exception e)
        {
            _logger.LogError($"Gemini Server error: {e.Message}");
            return null;
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
        catch (Exception e)
        {
            _logger.LogError($"Axe Core Server Error: {e.Message}");
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
        catch (Exception e)
        {
            _logger.LogError($"PageSpeed Server Error: {e.Message}");
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
        catch (Exception e)
        {
            _logger.LogError($"Nu Validator Server Error: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Uploads an HTML file or URL for accessibility and performance analysis,
    /// with optional design and specification files.
    /// </summary>
    /// <param name="name">Optional custom name for the webpage. Must not exceed 100 characters.</param>
    /// <param name="email">The email address of the user uploading the webpage.</param>
    /// <param name="htmlFile">Optional HTML file to upload. Must be a .html file up to 5MB.</param>
    /// <param name="url">Optional URL to fetch HTML content from. Must not exceed 2000 characters.</param>
    /// <param name="designFile">Optional design image file (.png, .jpg, .jpeg, .gif, .bmp, .svg) up to 5MB.</param>
    /// <param name="specificationFile">Optional specification file in .txt or .pdf format, up to 2MB.</param>
    /// <returns>
    /// The unique identifier of the uploaded webpage.
    /// </returns>
    /// <response code="200">
    /// Upload and analysis succeeded; returns the unique ID of the uploaded webpage.
    /// </response>
    /// <response code="400">
    /// Invalid input — for example:  
    /// • Missing both HTML file and URL  
    /// • Both HTML file and URL provided  
    /// • Invalid or oversized file  
    /// • Invalid or missing email  
    /// </response>
    /// <response code="401">
    /// User not found — email is valid but no registered account exists.
    /// </response>
    /// <response code="503">
    /// A server or analysis service error occurred while processing the upload.
    /// </response>
    /// <remarks>
    /// The provided HTML file or URL will be analyzed using accessibility, performance,  
    /// and HTML validation tools, and the results will be stored for later retrieval.
    /// </remarks>
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] string? name, [FromQuery] string email, IFormFile? htmlFile = null, [FromForm] string? url = null, IFormFile? designFile = null, IFormFile? specificationFile = null)
    {
        //input validation
        if (string.IsNullOrWhiteSpace(email) || email.Length > 100 || !new EmailAddressAttribute().IsValid(email))
            return BadRequest("Invalid email");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return BadRequest("Name cannot be null nor longer than 100 characters");
        if (!string.IsNullOrWhiteSpace(url) && url.Length > 2000)
            return BadRequest("URL is too long");
        if (htmlFile == null && string.IsNullOrWhiteSpace(url))
            return BadRequest("Either HTML file or URL is required");

        if (htmlFile != null && !string.IsNullOrWhiteSpace(url))
            return BadRequest("Provide either HTML file or URL, not both");

        if (htmlFile != null)
        {
            if (Path.GetExtension(htmlFile.FileName).ToLower() != ".html")
                return BadRequest("HTML file must have .html extension");

            if (htmlFile.Length > 2 * 1024 * 1024)
                return BadRequest("HTML file size cannot exceed 5MB");
        }
        if (specificationFile != null)
        {
            var ext = Path.GetExtension(specificationFile.FileName).ToLower();
            if (ext != ".txt" && ext != ".pdf")
                return BadRequest("Specification file must be .txt or .pdf");

            if (specificationFile.Length > 2 * 1024 * 1024)
                return BadRequest("Specification file size cannot exceed 2MB");
        }

        if (designFile != null)
        {
            var ext = Path.GetExtension(designFile.FileName).ToLower();
            var validImageExts = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg" };
            if (!validImageExts.Contains(ext))
                return BadRequest("Design file must be an image");

            if (designFile.Length > 5 * 1024 * 1024)
                return BadRequest("Design file size cannot exceed 5MB");
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
                    return BadRequest("Invalid or broken URL.");
                }
            }
            //Upload Files
            string htmlFileId;
            string? designFileId = null;
            string? specFileId = null;
            Task<string> taskHtmlFileId;
            Task<string> taskdesignFileId = Task.FromResult<string>(default!);
            Task<string> taskspecFileId = Task.FromResult<string>(default!);

            using var htmlStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(htmlText));
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

            //LLM Query:
            WebAuditResults webAuditResults = new WebAuditResults { axeCoreResult = axeCoreResult?.violations, pageSpeedResult = pageSpeedResult, nuValidatorResult = nuValidatorResult, responsivenessResult = axeCoreResult?.responsivenessResults };
            LLMResponse? llmResult = null;
            string? specification = await GetSpecifications(specificationFile);
            llmResult = await ForwardToLLM(htmlText: htmlText, specification: specification, designFile: designFile?.OpenReadStream(), designFileName: designFile?.FileName ?? "designFile.png", designFileType: designFile?.ContentType, webAuditResults: webAuditResults);
            bool llmError = llmResult == null;

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
            return Ok(webpageId);
        }
        catch (Exception error)
        {
            _logger.LogError($"Error in Upload: {error.Message}");
            return StatusCode(503, $"Something went wrong.");
        }
    }
    private async Task<string?> GetSpecifications(IFormFile? specificationFile)
    {
        if (specificationFile != null)
        {
            var ext = Path.GetExtension(specificationFile.FileName).ToLower();
            if (ext == ".txt")
            {
                using var specReader = new StreamReader(specificationFile.OpenReadStream());
                return await specReader.ReadToEndAsync();
            }
            else if (ext == ".pdf")
            {
                return await ExtractTextFromPdfAsync(specificationFile);
            }
        }
        return null;
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

    /// <summary>
    /// Retrieves a list of webpages uploaded by the user associated with the given email.
    /// </summary>
    /// <param name="email">The email address of the user. Must be valid and not exceed 100 characters.</param>
    /// <returns>
    /// A collection of <see cref="WebpageSummary"/> objects representing the webpages uploaded by the user.
    /// </returns>
    /// <response code="200">
    /// Successfully retrieved the list of webpages for the user.
    /// </response>
    /// <response code="400">
    /// Invalid email — null, empty, too long, or in an invalid format.
    /// </response>
    /// <response code="401">
    /// The email is valid but no registered user exists for it.
    /// </response>
    /// <response code="503">
    /// A server or database error occurred while retrieving the list.
    /// </response>
    /// <remarks>
    /// The result includes only summary details of each webpage (e.g., ID, name, upload date, file name, and URL),
    /// not the full webpage content.
    /// </remarks>
    [ProducesResponseType(typeof(List<WebpageSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [HttpGet("list-webpages")]
    public async Task<IActionResult> ListWebpages([FromQuery] string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 100 || !new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");

            var userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");

            var list = await _webpageAnalyseService.ListWebpagesAsync(userId);
            return Ok(list);
        }
        catch (Exception error)
        {
            _logger.LogError($"Error in ListWebpages: {error.Message}");
            return StatusCode(503, $"Something went wrong.");
        }
    }

    /// <summary>
    /// Retrieves the HTML content and analysis result of a specific webpage uploaded by the user.
    /// </summary>
    /// <param name="id">The unique ID of the webpage to retrieve. Must be a valid MongoDB ObjectId.</param>
    /// <param name="email">The email address of the user requesting the webpage. Must be valid and not exceed 100 characters.</param>
    /// <returns>
    /// A <see cref="WebpageContentAndAnalysisResponse"/> containing the HTML content and associated analysis results.
    /// </returns>
    /// <response code="200">
    /// Successfully retrieved the webpage's HTML content and analysis result.
    /// </response>
    /// <response code="400">
    /// Invalid input — the email or webpage ID is missing, incorrectly formatted, or exceeds length limits.
    /// </response>
    /// <response code="401">
    /// The email is valid but no registered user exists for it.
    /// </response>
    /// <response code="404">
    /// The specified webpage or its HTML content could not be found.
    /// </response>
    /// <response code="503">
    /// A server or database error occurred while retrieving the content.
    /// </response>
    /// <remarks>
    /// The response includes both the raw HTML content and any associated analysis results
    /// such as accessibility, performance, and validation findings.
    /// </remarks>
    [ProducesResponseType(typeof(WebpageContentAndAnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]

    [HttpGet("view-webpage/{id}")]
    public async Task<IActionResult> ViewWebpage(string id, string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 100 || !new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");
            if (string.IsNullOrWhiteSpace(id) || !MongoDB.Bson.ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID");
            var userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");

            var contentResult = await _webpageAnalyseService.GetWebpageContentAndAnalysisAsync(id);
            var htmlContent = contentResult.HtmlContent;
            var webpageAnalysisResult = contentResult.WebpageAnalysisResult;
            if (htmlContent == null)
                return NotFound("Webpage or HTML content not found");

            return Ok(new WebpageContentAndAnalysisResult { HtmlContent = htmlContent, WebpageAnalysisResult = webpageAnalysisResult });
        }
        catch (Exception error)
        {
            _logger.LogError($"Error in ViewWebpage: {error.Message}");
            return StatusCode(503, $"Something went wrong.");
        }
    }
    /// <summary>
    /// Downloads the design file associated with a specific webpage for a registered user.
    /// </summary>
    /// <param name="webpageId">
    /// The unique ID of the webpage whose design file is to be downloaded. Must be a valid MongoDB ObjectId.
    /// </param>
    /// <param name="email">
    /// The email address of the user requesting the download. Must be valid and not exceed 100 characters.
    /// </param>
    /// <returns>
    /// A <see cref="FileDownloadResponse"/> containing the design file stream, file name, and MIME type.
    /// </returns>
    /// <response code="200">
    /// The design file was found and returned as a downloadable stream.
    /// </response>
    /// <response code="400">
    /// The email or webpage ID is invalid.
    /// </response>
    /// <response code="401">
    /// The user is not registered.
    /// </response>
    /// <response code="404">
    /// The webpage was not found or it does not have an associated design file.
    /// </response>
    /// <response code="503">
    /// A server or file retrieval error occurred while processing the request.
    /// </response>
    /// <remarks>
    /// This endpoint retrieves the design file uploaded for a webpage and returns it
    /// as a downloadable file stream with the appropriate MIME type.
    /// </remarks>
    [ProducesResponseType(typeof(File), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [HttpGet("download-designfile/{webpageId}")]
    public async Task<IActionResult> DownloadDesignFile(string webpageId, [FromQuery] string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 100 || !new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");
            if (string.IsNullOrWhiteSpace(webpageId) || !MongoDB.Bson.ObjectId.TryParse(webpageId, out _))
                return BadRequest("Invalid ID");
            string userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");

            Webpage webpage = await _webpageAnalyseService.GetWebpageAsync(webpageId, userId);
            if (webpage == null) return NotFound("Webpage not found");
            string? designFileId = webpage.DesignFileId;
            if (designFileId == null) return NotFound("Design File was not uploaded.");

            var fileResult = await _webpageAnalyseService.DownloadFileAsync(designFileId);
            var stream = fileResult.Stream;
            var fileName = fileResult.FileName;
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
        catch (Exception error)
        {
            _logger.LogError($"Error in DownloadDesignFile: {error.Message}");
            return StatusCode(503, $"Something went wrong.");
        }
    }

    class DownloadSpecificationsResponse
    {
        public string Content;
    }
    /// <summary>
    /// Retrieves the text content from the specification file (PDF or text) associated with a specific webpage for a registered user.
    /// </summary>
    /// <param name="webpageId">
    /// The unique ID of the webpage whose specification file content is to be retrieved. Must be a valid MongoDB ObjectId.
    /// </param>
    /// <param name="email">
    /// The email address of the user requesting the content. Must be valid and not exceed 100 characters.
    /// </param>
    /// <returns>
    /// JSON containing the extracted text content from the specification file.
    /// </returns>
    /// <response code="200">
    /// Successfully retrieved the specification file content as plain text.
    /// </response>
    /// <response code="400">
    /// Invalid input — the email or webpage ID is missing or incorrectly formatted.
    /// </response>
    /// <response code="401">
    /// The email is valid but no registered user exists for it.
    /// </response>
    /// <response code="404">
    /// The specified webpage or its specification file could not be found.
    /// </response>
    /// <response code="503">
    /// A server or file processing error occurred while retrieving or parsing the specification file.
    /// </response>
    /// <remarks>
    /// - If the file is `.txt`, its content is returned as-is.  
    /// - If the file is `.pdf`, the text is extracted from all pages and returned.  
    /// - The result is wrapped in a JSON object with a `content` property.
    /// </remarks>
    [ProducesResponseType(typeof(DownloadSpecificationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [HttpGet("download-specifications/{webpageId}")]
    public async Task<IActionResult> DownloadSpecifications(string webpageId, [FromQuery] string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 100 || !new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email");
            if (string.IsNullOrWhiteSpace(webpageId) || !MongoDB.Bson.ObjectId.TryParse(webpageId, out _))
                return BadRequest("Invalid ID");
            string userId = await _webpageAnalyseService.GetUserByEmailAsync(email);
            if (userId == null)
                return Unauthorized("Please Sign up");
            if (webpageId == null) return BadRequest("WebpageId cannot be null");

            Webpage webpage = await _webpageAnalyseService.GetWebpageAsync(webpageId, userId);
            if (webpage == null) return NotFound("Webpage not found");
            string? specificationFileId = webpage.SpecificationFileId;
            if (specificationFileId == null) return NotFound("Specification File was not uploaded.");

            var fileResult = await _webpageAnalyseService.DownloadFileAsync(specificationFileId);
            var stream = fileResult.Stream;
            var fileName = fileResult.FileName;

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
            return Ok(new DownloadSpecificationsResponse { Content = content });
        }
        catch (Exception error)
        {
            _logger.LogError($"Error in DownloadSpecifications: {error.Message}");
            return StatusCode(503, $"Something went wrong.");
        }
    }


}
