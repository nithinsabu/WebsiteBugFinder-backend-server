using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.Protected;
using WBF.Controllers;
using WBF.Services;
using WBF.Models;
using System.Text;
using System.Net;
using Xunit;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Newtonsoft.Json;
using PdfSharpCore;
// using Microsoft.Extensions.Configuration;
// using Castle.Core.Configuration;
using Microsoft.Extensions.Options;
namespace WBF.Tests;

public class WebpageAnalyseControllerTests : IDisposable
{
    private readonly Mock<IWebpageAnalyseService> _mockService;
    private readonly WebpageAnalyseController _controller;

    public WebpageAnalyseControllerTests()
    {
        _mockService = new Mock<IWebpageAnalyseService>();
        var mockLogger = new Mock<ILogger<WebpageAnalyseController>>();
        var MockIHttpClientFactory = MockHttpClientFactory();
        var mockPageSpeedConfig = new Mock<IOptions<PageSpeedAPIConfig>>();
        mockPageSpeedConfig.Setup(s => s.Value).Returns(new PageSpeedAPIConfig
        {
            API_KEY = "MOCK_API_KEY"
        });
        _controller = new WebpageAnalyseController(_mockService.Object, MockIHttpClientFactory, mockLogger.Object, mockPageSpeedConfig.Object);
    }

    private static IHttpClientFactory MockHttpClientFactory()
    {
        var factory = new Mock<IHttpClientFactory>();

        factory.Setup(f => f.CreateClient("PythonServer"))
            .Returns(CreateMockedHttpClient(new LLMResponse()));

        factory.Setup(f => f.CreateClient("AxeCore"))
            .Returns(CreateMockedHttpClient(new AxeCoreResponse()));

        factory.Setup(f => f.CreateClient("PageSpeedAPI"))
            .Returns(CreateMockedHttpClient(new PageSpeedResponse()));

        factory.Setup(f => f.CreateClient("NuValidator"))
            .Returns(CreateMockedHttpClient(new NuValidatorResponse()));

        return factory.Object;
    }

    private static HttpClient CreateMockedHttpClient<T>(T responseObject)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(responseObject),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            });

        return new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://fake")
        };
    }
    private IFormFile CreateHtmlFile()
    {
        var content = "sample html content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "htmlFile", "index.html");
    }

    private static FormFile CreateFile(string filename, string contentType)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("sample content"));
        return new FormFile(stream, 0, stream.Length, filename, filename)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public void Dispose()
    {
        _mockService.Reset();
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync("abc123");
        var result = await _controller.Login("invalidEmail");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid email", badRequest.Value);
    }

    [Fact]
    public async Task Login_UserExists_ReturnsOkWithEmail()
    {
        var email = "user@example.com";
        _mockService.Setup(s => s.GetUserByEmailAsync(email))
                    .ReturnsAsync("abc123");

        var result = await _controller.Login(email);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(okResult.Value));
        Assert.Equal(email, dict["email"]);
    }

    [Fact]
    public async Task Login_UserNotFound_ReturnsUnauthorized()
    {
        var email = "user@example.com";
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync((string?)null);

        var result = await _controller.Login(email);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
public async Task Login_Returns500_WhenExceptionIsThrown()
{
    // Arrange
    string email = "test@example.com";
    _mockService
        .Setup(s => s.GetUserByEmailAsync(email))
        .ThrowsAsync(new Exception("Simulated failure"));

    // Act
    var result = await _controller.Login(email);

    // Assert
    var status = Assert.IsType<ObjectResult>(result);
    Assert.Equal(500, status.StatusCode);
    Assert.Contains("Something went wrong: Simulated failure", status.Value.ToString());
}

    [Fact]
    public async Task Signup_WithInvalidEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.CreateUserAsync(It.IsAny<string>()))
                    .ReturnsAsync("userId");
        var result = await _controller.Signup("bademail");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid email", badRequest.Value);
    }

    [Fact]
    public async Task Signup_WithValidEmail_ReturnsOkWithEmail()
    {
        var email = "user@example.com";
        var userId = "abc123";

        _mockService.Setup(s => s.CreateUserAsync(email))
                    .ReturnsAsync(userId);

        var result = await _controller.Signup(email);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(okResult.Value));
        Assert.Equal(email, dict["email"]);
    }

    [Fact]
    public async Task Signup_ReturnsBadRequest_WhenUserAlreadyExists()
    {
        // Arrange
        string email = "test@example.com";
        _mockService
            .Setup(s => s.CreateUserAsync(email))
            .ReturnsAsync((string?)null); // Simulate user already exists

        // Act
        var result = await _controller.Signup(email);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email already exists", badRequest.Value);
    }

    [Fact]
    public async Task Signup_Returns500_WhenExceptionIsThrown()
    {
        // Arrange
        string email = "test@example.com";
        _mockService
            .Setup(s => s.CreateUserAsync(email))
            .ThrowsAsync(new Exception("Simulated failure"));

        // Act
        var result = await _controller.Signup(email);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Contains("Something went wrong: Simulated failure", statusResult.Value?.ToString());
    }

    //Upload Validation tests
    [Fact]
    public async Task Upload_MissingEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: null!);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_InvalidEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "not-an-email");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_NoHtmlOrUrl_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_BothHtmlAndUrl_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile(), url: "https://example.com");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_HtmlWithWrongExtension_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateFile("file.txt", "text/plain"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_SpecificationWithInvalidExtension_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile(), specificationFile: CreateFile("spec.png", "images/png"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_DesignFileWithInvalidExtension_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile(), designFile: CreateFile("design.exe", "application/octet-stream"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    //Business logic tests
    [Fact]
    public async Task Upload_UserNotFound_ReturnsUnauthorized()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("abc123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile());
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Upload_WithHtmlOnly_ReturnsOk()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "MyPage", email: "test@example.com", htmlFile: CreateHtmlFile());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("webpage123", ok.Value);
    }

    [Fact]
    public async Task Upload_WithUrlOnly_ReturnsOk()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "FromUrl", email: "test@example.com", url: "https://example.com");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("webpage123", ok.Value);
    }

    [Fact]
    public async Task Upload_WithDesignAndSpec_ReturnsOk()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("fileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(
            name: "WithFiles",
            email: "test@example.com",
            htmlFile: CreateHtmlFile(),
            designFile: CreateFile("design.png", "image/png"),
            specificationFile: CreateFile("spec.txt", "text/plain")
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("webpage123", ok.Value);
    }

    [Fact]
    public async Task Upload_WithPdfSpec_ReturnsOk()
    {
        // Arrange
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("fileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var htmlFile = CreateHtmlFile();
        var designFile = CreateFile("design.png", "image/png");

        var pdfBytes = CreateSimplePdfWithText("Mock PDF Spec");
        var pdfStream = new MemoryStream(pdfBytes);

        var specificationFile = new FormFile(pdfStream, 0, pdfBytes.Length, "specification", "specification.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        // Act
        var result = await _controller.Upload(
            name: "UploadWithPDF",
            email: "test@example.com",
            htmlFile: htmlFile,
            designFile: designFile,
            specificationFile: specificationFile
        );

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("webpage123", ok.Value);
    }


    [Fact]
    public async Task ListWebpages_InvalidEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync("user123");

        var expectedList = new List<WebpageSummary>
    {
        new WebpageSummary { Id = "id1", Name = "Test 1" },
        new WebpageSummary { Id = "id2", Name = "Test 2" }
    };

        _mockService.Setup(s => s.ListWebpagesAsync(It.IsAny<string>()))
                    .ReturnsAsync(expectedList);

        var result = await _controller.ListWebpages("invalid-email");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid email", badRequest.Value);
    }

    [Fact]
    public async Task ListWebpages_UserNotFound_ReturnsUnauthorized()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com"))
                    .ReturnsAsync((string?)null);

        var expectedList = new List<WebpageSummary>
    {
        new WebpageSummary { Id = "id1", Name = "Test 1" },
        new WebpageSummary { Id = "id2", Name = "Test 2" }
    };

        _mockService.Setup(s => s.ListWebpagesAsync(It.IsAny<string>()))
                    .ReturnsAsync(expectedList);

        var result = await _controller.ListWebpages("test@example.com");

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task ListWebpages_ValidUser_ReturnsWebpagesForUser()
    {
        var userId = "user123";
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com"))
                    .ReturnsAsync(userId);

        var expectedList = new List<WebpageSummary>
    {
        new WebpageSummary { Id = "id1", Name = "Test 1" },
        new WebpageSummary { Id = "id2", Name = "Test 2" }
    };

        _mockService.Setup(s => s.ListWebpagesAsync(userId))
                    .ReturnsAsync(expectedList);

        var result = await _controller.ListWebpages("test@example.com");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedList = Assert.IsType<List<WebpageSummary>>(okResult.Value);

        Assert.Equal(2, returnedList.Count);
        Assert.Equal("id1", returnedList[0].Id);
        Assert.Equal("Test 1", returnedList[0].Name);
    }


    public class ViewWebpageResponse
    {
        public string HtmlContent { get; set; } = default!;
        public WebpageAnalysisResult WebpageAnalysisResult { get; set; } = default!;
    };
    [Fact]
    public async Task ViewWebpage_ValidId_ReturnsHtmlAndLLM()
    {
        // Arrange
        string webpageId = "abc123";
        string email = "test@example.com";
        _mockService.Setup(s => s.GetUserByEmailAsync(email)).ReturnsAsync("user123");
        _mockService.Setup(s => s.GetWebpageContentAndAnalysisAsync(webpageId))
                        .ReturnsAsync(("<html>hi</html>", new WebpageAnalysisResult { Id = "webpage123", WebpageId = "abd123" }));

        // Act
        var result = await _controller.ViewWebpage(webpageId, email);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        var deserialized = JsonConvert.DeserializeObject<ViewWebpageResponse>(
            JsonConvert.SerializeObject(okResult.Value)
        )!;

        Assert.Equal("<html>hi</html>", deserialized.HtmlContent);
        Assert.Equal("webpage123", deserialized.WebpageAnalysisResult.Id);
        Assert.Equal("abd123", deserialized.WebpageAnalysisResult.WebpageId);
    }

    [Fact]
    public async Task ViewWebpage_UserNotFound_ReturnsUnauthorized()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync((string?)null);
        _mockService.Setup(s => s.GetWebpageContentAndAnalysisAsync("abd123"))
                .ReturnsAsync(("<html>hi</html>", new WebpageAnalysisResult { Id = "webpage123", WebpageId = "abd123" }));

        var result = await _controller.ViewWebpage("abc123", "test@example.com");

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task ViewWebpage_InvalidWebpage_ReturnsNotFound()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user123");
        _mockService.Setup(s => s.GetWebpageContentAndAnalysisAsync("abd123"))
                .ReturnsAsync(("<html>hi</html>", new WebpageAnalysisResult { Id = "webpage123", WebpageId = "abd123" }));

        var result = await _controller.ViewWebpage("abc123", "test@example.com");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Webpage or HTML content not found", notFound.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsBadRequest_IfEmailIsNull()
    {
        var result = await _controller.DownloadDesignFile("123", null);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email cannot be null", badRequest.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsUnauthorized_IfUserIdIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync((string)null);
        var result = await _controller.DownloadDesignFile("123", "test@example.com");
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsBadRequest_IfWebpageIdIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        var result = await _controller.DownloadDesignFile(null, "test@example.com");
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("WebpageId cannot be null", badRequest.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsNotFound_IfWebpageIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("123", "user1")).ReturnsAsync((Webpage)null);

        var result = await _controller.DownloadDesignFile("123", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Webpage not found", notFound.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsNotFound_IfDesignFileIdIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("123", "user1")).ReturnsAsync(new Webpage { DesignFileId = null });

        var result = await _controller.DownloadDesignFile("123", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Design File was not uploaded.", notFound.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_Returns500_IfStreamOrFileNameIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("123", "user1")).ReturnsAsync(new Webpage { DesignFileId = "file123" });
        _mockService.Setup(s => s.DownloadFileAsync("file123")).ReturnsAsync(((Stream)null, null));

        var result = await _controller.DownloadDesignFile("123", "test@example.com");
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
        Assert.Contains("Something went wrong", status.Value.ToString());
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsFile_WithValidInput()
    {
        var testStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "mockfile.png";

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("123", "user1")).ReturnsAsync(new Webpage { DesignFileId = "file123" });
        _mockService.Setup(s => s.DownloadFileAsync("file123")).ReturnsAsync((testStream, fileName));

        var result = await _controller.DownloadDesignFile("123", "test@example.com");

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("mockfile.png", fileResult.FileDownloadName);
        Assert.Equal("image/png", fileResult.ContentType); // assuming png maps correctly
        Assert.Same(testStream, fileResult.FileStream);
    }

    [Fact]
    public async Task Returns_BadRequest_If_Email_Is_Null()
    {
        var result = await _controller.DownloadSpecifications("id", null);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email cannot be null", badRequest.Value);
    }

    [Fact]
    public async Task Returns_Unauthorized_If_User_Not_Found()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync((string)null);

        var result = await _controller.DownloadSpecifications("id", "test@example.com");
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task Returns_BadRequest_If_WebpageId_Is_Null()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");

        var result = await _controller.DownloadSpecifications(null, "test@example.com");
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("WebpageId cannot be null", badRequest.Value);
    }

    [Fact]
    public async Task Returns_NotFound_If_Webpage_Not_Found()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("id", "user")).ReturnsAsync((Webpage)null);

        var result = await _controller.DownloadSpecifications("id", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Webpage not found", notFound.Value);
    }

    [Fact]
    public async Task Returns_NotFound_If_SpecificationFileId_Is_Null()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("id", "user")).ReturnsAsync(new Webpage { SpecificationFileId = null });

        var result = await _controller.DownloadSpecifications("id", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Specification File was not uploaded.", notFound.Value);
    }

    [Fact]
    public async Task Returns_500_If_Stream_Is_Null()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("id", "user")).ReturnsAsync(new Webpage { SpecificationFileId = "fid" });
        _mockService.Setup(s => s.DownloadFileAsync("fid")).ReturnsAsync((null as Stream, null));

        var result = await _controller.DownloadSpecifications("id", "test@example.com");
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);
        Assert.Contains("Something went wrong", obj.Value.ToString());
    }

    public class SpecificationResponse
    {
        public string Content { get; set; }
    }

    [Fact]
    public async Task Returns_Content_For_Text_File()
    {
        var content = "This is text content.";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("id", "user")).ReturnsAsync(new Webpage { SpecificationFileId = "fid" });
        _mockService.Setup(s => s.DownloadFileAsync("fid")).ReturnsAsync((stream, "file.txt"));

        var result = await _controller.DownloadSpecifications("id", "test@example.com");
        var ok = Assert.IsType<OkObjectResult>(result);

        string json = JsonConvert.SerializeObject(ok.Value);

        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        Assert.Equal(content, dict["content"]);
    }

    [Fact]
    public async Task DownloadSpecifications_ReturnsContent_ForPdfFile()
    {
        // Arrange
        var pdfBytes = CreateSimplePdfWithText("This is from PDF");
        var stream = new MemoryStream(pdfBytes);

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("id", "user1")).ReturnsAsync(new Webpage
        {
            SpecificationFileId = "spec123"
        });
        _mockService.Setup(s => s.DownloadFileAsync("spec123")).ReturnsAsync((stream, "mock.pdf"));

        // Act
        var result = await _controller.DownloadSpecifications("id", "test@example.com");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        string json = JsonConvert.SerializeObject(ok.Value);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        Assert.Contains("This is from PDF", dict["content"]);
    }

    private byte[] CreateSimplePdfWithText(string text)
    {
        using var mem = new MemoryStream();
        var doc = new PdfSharpCore.Pdf.PdfDocument();
        var page = doc.AddPage();
        var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
        gfx.DrawString(text,
            new PdfSharpCore.Drawing.XFont("Arial", 12),
            PdfSharpCore.Drawing.XBrushes.Black,
            new PdfSharpCore.Drawing.XPoint(50, 100));
        doc.Save(mem, false);
        return mem.ToArray();
    }


}