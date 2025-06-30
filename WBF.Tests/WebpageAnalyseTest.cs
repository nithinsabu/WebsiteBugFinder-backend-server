using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.Protected;
using WBF.Controllers;
using WBF.Services;
using WBF.Models;
using Newtonsoft.Json;
using System.Text;
using System.Net;
using Xunit;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
// using Castle.Core.Configuration;
using Microsoft.Extensions.Configuration;
namespace WBF.Tests;

public class WebpageAnalyseControllerTests : IDisposable
{
    private readonly Mock<IWebpageAnalyseService> _mockService;
    private readonly WebpageAnalyseController _controller;

    public WebpageAnalyseControllerTests()
    {
        _mockService = new Mock<IWebpageAnalyseService>();
        var mockLogger = new Mock<ILogger<WebpageAnalyseController>>();
        var MockIHttpClientFactory = MockHttpClientFactory("mocked LLM response");
        _controller = new WebpageAnalyseController(_mockService.Object, MockIHttpClientFactory, mockLogger.Object);
    }

    private static IHttpClientFactory MockHttpClientFactory(string responseContent)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://fake")
        };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("PythonServer")).Returns(client);
        return factory.Object;
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
                    .ReturnsAsync((string)null);

        var result = await _controller.Login(email);

        Assert.IsType<UnauthorizedResult>(result);
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


    //Upload Validation tests
    [Fact]
    public async Task Upload_MissingEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: null!);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_InvalidEmail_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "not-an-email");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_NoHtmlOrUrl_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_BothHtmlAndUrl_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile(), url: "https://example.com");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_HtmlWithWrongExtension_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateFile("file.txt", "text/plain"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_SpecificationWithInvalidExtension_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile(), specificationFile: CreateFile("spec.pdf", "application/pdf"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_DesignFileWithInvalidExtension_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile(), designFile: CreateFile("design.exe", "application/octet-stream"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    //Business logic tests
    [Fact]
    public async Task Upload_UserNotFound_ReturnsUnauthorized()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("abc123");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("abc123");

        var result = await _controller.Upload(name: "test", email: "test@example.com", htmlFile: CreateHtmlFile());
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Upload_WithHtmlOnly_ReturnsOk()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "MyPage", email: "test@example.com", htmlFile: CreateHtmlFile());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("webpage123", ok.Value);
    }

    [Fact]
    public async Task Upload_WithUrlOnly_ReturnsOk()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("htmlFileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

        var result = await _controller.Upload(name: "FromUrl", email: "test@example.com", url: "https://example.com");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("webpage123", ok.Value);
    }

    [Fact]
    public async Task Upload_WithDesignAndSpec_ReturnsOk()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("123abc");
        _mockService.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("fileId");
        _mockService.Setup(s => s.CreateWebpageAndLLMFeedbackAsync(It.IsAny<Webpage>(), It.IsAny<LLMFeedback>())).ReturnsAsync("webpage123");

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

    [Fact]
public async Task ViewWebpage_ValidId_ReturnsHtmlAndLLM()
{
    // Arrange
    string webpageId = "abc123";
    string email = "test@example.com";
    _mockService.Setup(s => s.GetUserByEmailAsync(email)).ReturnsAsync("user123");
    _mockService.Setup(s => s.GetWebpageContentAndLLMAsync(webpageId))
                .ReturnsAsync(("<html>hi</html>", "LLM feedback"));

    // Act
    var result = await _controller.ViewWebpage(webpageId, email);

    // Assert
   var okResult = Assert.IsType<OkObjectResult>(result);
    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(
        JsonConvert.SerializeObject(okResult.Value)
    )!;

    Assert.Contains("<html>hi</html>", dict["html"]);
    Assert.Equal("LLM feedback", dict["llm"]);
}

    [Fact]
    public async Task ViewWebpage_UserNotFound_ReturnsUnauthorized()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync((string?)null);
        _mockService.Setup(s => s.GetWebpageContentAndLLMAsync(It.IsAny<string>()))
                .ReturnsAsync(("<html>hi</html>", "LLM feedback"));

        var result = await _controller.ViewWebpage("abc123", "test@example.com");

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Please Sign up", unauthorized.Value);
    }

    [Fact]
    public async Task ViewWebpage_InvalidWebpage_ReturnsNotFound()
    {
        _mockService.Setup(s => s.GetUserByEmailAsync("test@example.com")).ReturnsAsync("user123");
        _mockService.Setup(s => s.GetWebpageContentAndLLMAsync("abc123")).ReturnsAsync((null, null));

        var result = await _controller.ViewWebpage("abc123", "test@example.com");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Webpage or HTML content not found", notFound.Value);
    }





}