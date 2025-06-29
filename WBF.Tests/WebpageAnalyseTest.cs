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

        var handlerMock = new Mock<HttpMessageHandler>();

handlerMock.Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent("mocked response")
    });

var mockHttpClient = new HttpClient(handlerMock.Object);

        _controller = new WebpageAnalyseController(_mockService.Object, mockHttpClient, mockLogger.Object);
    }

    public void Dispose()
    {
        _mockService.Reset();
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsBadRequest()
    {
        var result = await _controller.Login("invalidEmail");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid email", badRequest.Value);
    }

    [Fact]
    public async Task Login_UserExists_ReturnsOkWithEmail()
    {
        var email = "user@example.com";
        _mockService.Setup(s => s.GetUserByEmailAsync(email))
                    .ReturnsAsync(new User { Email = email });

        var result = await _controller.Login(email);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(okResult.Value));
        Assert.Equal(email, dict["email"]);
    }

    [Fact]
    public async Task Login_UserNotFound_ReturnsUnauthorized()
    {
        var email = "user@example.com";
        _mockService.Setup(s => s.GetUserByEmailAsync(email))
                    .ReturnsAsync((User)null);

        var result = await _controller.Login(email);

        Assert.IsType<UnauthorizedResult>(result);
    }



    [Fact]
    public async Task Signup_WithInvalidEmail_ReturnsBadRequest()
    {
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


    private IFormFile CreateFile(string fileName = "file.html", string content = "<html></html>")
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, fileName, fileName);
    }

    [Fact]
    public async Task Upload_MissingEmail_ReturnsBadRequest()
    {
        var result = await _controller.Upload(null);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_InvalidEmail_ReturnsBadRequest()
    {
        var result = await _controller.Upload("not-an-email");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_ValidEmail_WithoutHtmlOrUrl_ReturnsBadRequest()
    {
        var result = await _controller.Upload("test@example.com");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_WithHtmlOnly_ReturnsOk()
    {
        _mockService.Setup(s => s.CreateWebpageAsync(It.IsAny<Webpage>())).ReturnsAsync("webpageId");

        var result = await _controller.Upload(
            email: "test@example.com",
            htmlFile: CreateFile()
        );

        Assert.IsType<OkObjectResult>(result);
    }
}