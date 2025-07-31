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
using MongoDB.Bson;
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

    //Login
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
    public async Task Login_WithNullEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync("abc123");
        var result = await _controller.Login(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email cannot be Null", badRequest.Value);
    }
    [Fact]
    public async Task Login_WithLongEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync("abc123");
        var result = await _controller.Login("averylongmockemailaddress_for_testing_purposes_only_that_exceeds_100_characters_example_1234567890@exampledomain.com");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email is too long", badRequest.Value);
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
        Assert.Equal(email, dict["Email"]);
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
    public async Task Login_Returns503_WhenExceptionIsThrown()
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
        Assert.Equal(503, status.StatusCode);
        Assert.Contains("Something went wrong.", status.Value.ToString());
    }

    //Signup
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
    public async Task Signup_WithNullEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.CreateUserAsync(It.IsAny<string>()))
                    .ReturnsAsync("userId");
        var result = await _controller.Signup(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email cannot be Null", badRequest.Value);
    }

    [Fact]
    public async Task Signup_WithLongEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.CreateUserAsync(It.IsAny<string>()))
                    .ReturnsAsync("userId");
        var result = await _controller.Signup("averylongmockemailaddress_for_testing_purposes_only_that_exceeds_100_characters_example_1234567890@exampledomain.com");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email is too long", badRequest.Value);
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
        Assert.Equal(email, dict["Email"]);
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
    public async Task Signup_Returns503_WhenExceptionIsThrown()
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
        Assert.Equal(503, statusResult.StatusCode);
        Assert.Contains("Something went wrong.", statusResult.Value?.ToString());
    }

    //Upload

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
    public async Task Upload_InvalidEmailLength_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var longEmail = new string('a', 101) + "@example.com";
        var result = await _controller.Upload(name: "test", email: longEmail);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_NameTooLong_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var longName = new string('n', 256); // Assuming 255 char limit
        var result = await _controller.Upload(name: longName, email: "test@example.com");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_UrlTooLong_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var longUrl = "http://" + new string('u', 2048); // Assuming 2048 char limit
        var result = await _controller.Upload(name: "test", email: "test@example.com", url: longUrl);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_DesignFileTooLarge_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var largeDesignFile = new FormFile(new MemoryStream(new byte[6 * 1024 * 1024]), 0, 6 * 1024 * 1024, "designFile", "design.png"); // 6 MB

        var result = await _controller.Upload(name: "test", email: "test@example.com", designFile: largeDesignFile);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_SpecFileTooLarge_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndAnalysisResultAsync(It.IsAny<Webpage>(), It.IsAny<WebpageAnalysisResult>())).ReturnsAsync("webpage123");

        var largeSpecFile = new FormFile(new MemoryStream(new byte[11 * 1024 * 1024]), 0, 11 * 1024 * 1024, "specFile", "spec.pdf"); // 11 MB

        var result = await _controller.Upload(name: "test", email: "test@example.com", specificationFile: largeSpecFile);
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

    //Upload: Business logic tests
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

    //ListWebpages
    [Theory]
    [InlineData(null)] // null email
    [InlineData("")] // empty string
    [InlineData("   ")] // whitespace
    [InlineData("invalid-email")] // invalid format
    [InlineData("reallylongemailaddress_thatexceedsonehundredcharacters_blahblahblahreallylongemailaddress_thatexceedsonehundredcharacters_blahblahblah@example.com")] // >100 chars
    public async Task ListWebpages_InvalidEmail_ReturnsBadRequest(string invalidEmail)
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync("user123");

        _mockService.Setup(s => s.ListWebpagesAsync(It.IsAny<string>()))
                    .ReturnsAsync(new List<WebpageSummary>());

        var result = await _controller.ListWebpages(invalidEmail);

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


    //ViewWebpage
    public class ViewWebpageResponse
    {
        public string HtmlContent { get; set; } = default!;
        public WebpageAnalysisResult WebpageAnalysisResult { get; set; } = default!;
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email-format")]
    [InlineData("thisisaverylongemailaddressthatexceedsthehundredcharacterslimitthisisaverylongemailaddressthatexceedsthehundredcharacterslimit@exampleveryverylongdomain.com")]
    public async Task ViewWebpage_InvalidEmail_ReturnsBadRequest(string invalidEmail)
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("user123");

        var result = await _controller.ViewWebpage("60d21b4667d0d8992e610c85", invalidEmail);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid email", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-object-id")]
    [InlineData("123")] // too short
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzz")] // invalid hex
    public async Task ViewWebpage_InvalidId_ReturnsBadRequest(string invalidId)
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("user123");

        var result = await _controller.ViewWebpage(invalidId, "valid@example.com");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid ID", badRequest.Value);
    }


    [Fact]
    public async Task ViewWebpage_ValidId_ReturnsHtmlAndAnalysisResult()
    {
        // Arrange
        string webpageId = ObjectId.GenerateNewId().ToString();
        string email = "test@example.com";
        _mockService.Setup(s => s.GetUserByEmailAsync(email)).ReturnsAsync("user123");
        _mockService.Setup(s => s.GetWebpageContentAndAnalysisAsync(webpageId))
                        .ReturnsAsync(new WebpageContentAndAnalysisResult
                        {
                            HtmlContent = "<html>hi</html>",
                            WebpageAnalysisResult = new WebpageAnalysisResult { Id = "webpage123", WebpageId = webpageId }
                        });

        // Act
        var result = await _controller.ViewWebpage(webpageId, email);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        var deserialized = JsonConvert.DeserializeObject<ViewWebpageResponse>(
            JsonConvert.SerializeObject(okResult.Value)
        )!;

        Assert.Equal("<html>hi</html>", deserialized.HtmlContent);
        Assert.Equal("webpage123", deserialized.WebpageAnalysisResult.Id);
        Assert.Equal(webpageId, deserialized.WebpageAnalysisResult.WebpageId);
    }

    [Fact]
    public async Task ViewWebpage_UserNotFound_ReturnsUnauthorized()
    {
        string webpageId = ObjectId.GenerateNewId().ToString();

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync((string?)null);
        _mockService.Setup(s => s.GetWebpageContentAndAnalysisAsync(webpageId))
     .ReturnsAsync(new WebpageContentAndAnalysisResult
     {
         HtmlContent = "<html>hi</html>",
         WebpageAnalysisResult = new WebpageAnalysisResult
         {
             Id = "webpage123",
             WebpageId = webpageId
         }
     });
        var result = await _controller.ViewWebpage(webpageId, "test@example.com");

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task ViewWebpage_InvalidWebpage_ReturnsNotFound()
    {
        string webpageId = ObjectId.GenerateNewId().ToString();

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user123");
        _mockService.Setup(s => s.GetWebpageContentAndAnalysisAsync(webpageId))
     .ReturnsAsync(new WebpageContentAndAnalysisResult());
        var result = await _controller.ViewWebpage(webpageId, "test@example.com");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Webpage or HTML content not found", notFound.Value);
    }

    //DownloadDesignFile
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("thisisaverylongemailaddressthatexceedsthehundredcharacterslimitthisisaverylongemailaddressthatexceedsthehundredcharacterslimit@exampleveryverylongdomain.com")]
    public async Task DownloadDesignFile_InvalidEmail_ReturnsBadRequest(string invalidEmail)
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync("user123");

        var result = await _controller.DownloadDesignFile("60d21b4667d0d8992e610c85", invalidEmail);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid email", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-objectid")]
    [InlineData("123")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task DownloadDesignFile_InvalidId_ReturnsBadRequest(string invalidId)
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>()))
                    .ReturnsAsync("user123");

        var result = await _controller.DownloadDesignFile(invalidId, "valid@example.com");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid ID", badRequest.Value);
    }



    [Fact]
    public async Task DownloadDesignFile_ReturnsUnauthorized_IfUserIdIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync((string)null);
        var result = await _controller.DownloadDesignFile("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsBadRequest_IfWebpageIdIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        var result = await _controller.DownloadDesignFile(null, "test@example.com");
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid ID", badRequest.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsNotFound_IfWebpageIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user1")).ReturnsAsync((Webpage)null);

        var result = await _controller.DownloadDesignFile("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Webpage not found", notFound.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsNotFound_IfDesignFileIdIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user1")).ReturnsAsync(new Webpage { DesignFileId = null });

        var result = await _controller.DownloadDesignFile("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Design File was not uploaded.", notFound.Value);
    }

    [Fact]
    public async Task DownloadDesignFile_Returns503_IfStreamOrFileNameIsNull()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user1")).ReturnsAsync(new Webpage { DesignFileId = "file123" });
        _mockService
    .Setup(s => s.DownloadFileAsync("file123"))
    .ReturnsAsync(new FileDownloadResult
    {
        Stream = null,
        FileName = null
    });

        var result = await _controller.DownloadDesignFile("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
        Assert.Contains("Something went wrong", status.Value.ToString());
    }

    [Fact]
    public async Task DownloadDesignFile_ReturnsFile_WithValidInput()
    {
        var testStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "mockfile.png";

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user1")).ReturnsAsync(new Webpage { DesignFileId = "file123" });
        _mockService.Setup(s => s.DownloadFileAsync("file123")).ReturnsAsync(new FileDownloadResult
        {
            Stream = testStream,
            FileName = fileName
        });

        var result = await _controller.DownloadDesignFile("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("mockfile.png", fileResult.FileDownloadName);
        Assert.Equal("image/png", fileResult.ContentType); // assuming png maps correctly
        Assert.Same(testStream, fileResult.FileStream);
    }

    //DownloadSpecifications
    [Fact]
    public async Task DownloadSpecifications_Returns_BadRequest_If_Email_Is_Null()
    {
        var result = await _controller.DownloadSpecifications("60f7d2a2c2a62b3b6c8e4f12", null);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid email", badRequest.Value);
    }

    [Fact]
    public async Task DownloadSpecifications_Returns_Unauthorized_If_User_Not_Found()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync((string)null);

        var result = await _controller.DownloadSpecifications("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task DownloadSpecifications_Returns_BadRequest_If_WebpageId_Is_Null()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");

        var result = await _controller.DownloadSpecifications(null, "test@example.com");
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid ID", badRequest.Value);
    }

    [Fact]
    public async Task DownloadSpecifications_Returns_NotFound_If_Webpage_Not_Found()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user")).ReturnsAsync((Webpage)null);

        var result = await _controller.DownloadSpecifications("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Webpage not found", notFound.Value);
    }

    [Fact]
    public async Task DownloadSpecifications_Returns_NotFound_If_SpecificationFileId_Is_Null()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user")).ReturnsAsync(new Webpage { SpecificationFileId = null });

        var result = await _controller.DownloadSpecifications("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Specification File was not uploaded.", notFound.Value);
    }

    [Fact]
    public async Task DownloadSpecifications_Returns_503_If_Stream_Is_Null()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user")).ReturnsAsync(new Webpage { SpecificationFileId = "fid" });
        _mockService
     .Setup(s => s.DownloadFileAsync("fid"))
     .ReturnsAsync(new FileDownloadResult
     {
         Stream = null,
         FileName = null
     });

        var result = await _controller.DownloadSpecifications("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        Assert.Contains("Something went wrong", obj.Value.ToString());
    }

    public class SpecificationResponse
    {
        public string Content { get; set; }
    }

    [Fact]
    public async Task DownloadSpecifications_Returns_Content_For_Text_File()
    {
        var content = "This is text content.";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user")).ReturnsAsync(new Webpage { SpecificationFileId = "fid" });
        _mockService
            .Setup(s => s.DownloadFileAsync("fid"))
            .ReturnsAsync(new FileDownloadResult
            {
                Stream = stream,
                FileName = "file.txt"
            });
        var result = await _controller.DownloadSpecifications("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");
        var ok = Assert.IsType<OkObjectResult>(result);

        string json = JsonConvert.SerializeObject(ok.Value);

        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        Assert.Equal(content, dict["Content"]);
    }

    [Fact]
    public async Task DownloadSpecifications_ReturnsContent_ForPdfFile()
    {
        // Arrange
        var pdfBytes = CreateSimplePdfWithText("This is from PDF");
        var stream = new MemoryStream(pdfBytes);

        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user1");
        _mockService.Setup(s => s.GetWebpageAsync("60f7d2a2c2a62b3b6c8e4f12", "user1")).ReturnsAsync(new Webpage
        {
            SpecificationFileId = "spec123"
        });
        _mockService
    .Setup(s => s.DownloadFileAsync("spec123"))
    .ReturnsAsync(new FileDownloadResult
    {
        Stream = stream,
        FileName = "mock.pdf"
    });

        // Act
        var result = await _controller.DownloadSpecifications("60f7d2a2c2a62b3b6c8e4f12", "test@example.com");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        string json = JsonConvert.SerializeObject(ok.Value);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        Assert.Contains("This is from PDF", dict["Content"]);
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