using Xunit;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Bson;
using Microsoft.Extensions.Options;
using WBF.Services;
using WBF.Models;
using Microsoft.Extensions.Logging;

public class WebpageAnalyseServiceTests
{
    private readonly Mock<IMongoDatabase> _mockDb = new();
    private readonly Mock<IGridFSBucket> _mockBucket = new();
    private readonly Mock<IMongoCollection<Webpage>> _mockWebpages = new();
    private readonly Mock<IMongoCollection<User>> _mockUsers = new();
    private readonly Mock<IMongoCollection<WebpageAnalysisResult>> _mockAnalysisResults = new();
    private readonly WebpageAnalyseService _service;
    private readonly ObjectId _uploadFileReturn = ObjectId.GenerateNewId();
    public WebpageAnalyseServiceTests()
    {
        var settings = Options.Create(new WebpageAnalyseDatabaseSettings
        {
            WebpagesCollectionName = "Webpages",
            UsersCollectionName = "Users",
            WebpageAnalysisResultsCollectionName = "Analysis"
        });

        _mockDb.Setup(db => db.GetCollection<Webpage>("Webpages", null)).Returns(_mockWebpages.Object);
        _mockDb.Setup(db => db.GetCollection<User>("Users", null)).Returns(_mockUsers.Object);
        _mockDb.Setup(db => db.GetCollection<WebpageAnalysisResult>("Analysis", null)).Returns(_mockAnalysisResults.Object);

        var mockCursor = new Mock<IAsyncCursor<GridFSFileInfo>>();
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor
            .SetupGet(c => c.Current)
            .Returns(new List<GridFSFileInfo>
            {
                new GridFSFileInfo(new BsonDocument { { "filename", "test.txt" } })
            });

        _mockBucket.Setup(b => b.UploadFromStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, default)).ReturnsAsync(_uploadFileReturn);
        _mockBucket.Setup(b => b.DownloadToStreamAsync(
            It.IsAny<ObjectId>(),
            It.IsAny<Stream>(),
            It.IsAny<GridFSDownloadOptions>(),
            It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
        _mockBucket
            .Setup(b => b.Find(
                It.IsAny<FilterDefinition<GridFSFileInfo>>(),
                It.IsAny<GridFSFindOptions>(), default))
            .Returns(mockCursor.Object);

        var mockLogger = new Mock<ILogger<WebpageAnalyseService>>();
        _service = new WebpageAnalyseService(_mockDb.Object, settings, mockLogger.Object, _mockBucket.Object);
    }

    private static IAsyncCursor<T> MockEmptyCursor<T>()
    {
        var mockCursor = new Mock<IAsyncCursor<T>>();
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(false); // no results
        mockCursor.SetupGet(c => c.Current).Returns(Enumerable.Empty<T>());
        return mockCursor.Object;
    }

    private static IAsyncCursor<T> MockNonEmptyCursor<T>(T document)
    {
        var mockCursor = new Mock<IAsyncCursor<T>>();
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true)
                  .ReturnsAsync(false);
        mockCursor.SetupGet(c => c.Current).Returns(new List<T> { document });
        return mockCursor.Object;
    }
    private static IAsyncCursor<T> MockNonEmptyCursor<T>(List<T> documents)
    {
        var mockCursor = new Mock<IAsyncCursor<T>>();
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true)
                  .ReturnsAsync(false);
        mockCursor.SetupGet(c => c.Current).Returns(documents);
        return mockCursor.Object;
    }


    [Fact]
    public async Task CreateUserAsync_NewEmail_InsertsUserAndReturnsId()
    {
        // Arrange
        var email = "new@example.com";
        _mockUsers
            .Setup(u => u.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockEmptyCursor<User>());

        _mockUsers
            .Setup(u => u.InsertOneAsync(
                It.IsAny<User>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<User, InsertOneOptions, CancellationToken>((user, _, _) =>
            {
                user.Id = "generated-id";
            });

        // Act
        var result = await _service.CreateUserAsync(email);

        // Assert
        Assert.Equal("generated-id", result);
        _mockUsers.Verify(u => u.InsertOneAsync(It.IsAny<User>(), null, default), Times.Once);
        _mockUsers.Verify(x => x.FindAsync(It.IsAny<FilterDefinition<User>>(),
               It.IsAny<FindOptions<User, User>>(),
               It.IsAny<CancellationToken>()), Times.Once());
    }
    [Fact]
    public async Task CreateUserAsync_ExistingEmail_ReturnsNull()
    {
        // Arrange
        var email = "existing@example.com";
        var existingUser = new User { Id = "existing-id", Email = email };

        _mockUsers
            .Setup(u => u.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockNonEmptyCursor<User>(existingUser));

        // Act
        var result = await _service.CreateUserAsync(email);

        // Assert
        Assert.Null(result);
        _mockUsers.Verify(u => u.InsertOneAsync(It.IsAny<User>(), null, default), Times.Never);
        _mockUsers.Verify(x => x.FindAsync(It.IsAny<FilterDefinition<User>>(),
               It.IsAny<FindOptions<User, User>>(),
               It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task CreateUserAsync_InsertFails_ThrowsException()
    {
        // Arrange
        var email = "error@example.com";
        _mockUsers
            .Setup(u => u.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockEmptyCursor<User>());

        _mockUsers
            .Setup(u => u.InsertOneAsync(
                It.IsAny<User>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("Insert failed"));

        // Act & Assert
        await Assert.ThrowsAsync<MongoException>(() => _service.CreateUserAsync(email));
    }

    [Fact]
    public async Task GetUserByEmailAsync_ExistingEmail_ReturnsUserId()
    {
        // Arrange
        var email = "existing@example.com";
        var existingUser = new User { Id = "existing-id", Email = email };

        _mockUsers
           .Setup(u => u.FindAsync(
               It.IsAny<FilterDefinition<User>>(),
               It.IsAny<FindOptions<User, User>>(),
               It.IsAny<CancellationToken>()))
           .ReturnsAsync(MockNonEmptyCursor<User>(existingUser));

        //Act
        string? id = await _service.GetUserByEmailAsync(email);

        //Assert
        Assert.Equal(id, existingUser.Id);
        _mockUsers.Verify(x => x.FindAsync(It.IsAny<FilterDefinition<User>>(),
               It.IsAny<FindOptions<User, User>>(),
               It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetUserByEmailAsync_NonExistingEmail_ReturnsNull()
    {
        //Arrange
        _mockUsers
           .Setup(u => u.FindAsync(
               It.IsAny<FilterDefinition<User>>(),
               It.IsAny<FindOptions<User, User>>(),
               It.IsAny<CancellationToken>()))
           .ReturnsAsync(MockEmptyCursor<User>());
        //Act
        var id = await _service.GetUserByEmailAsync("test@test.com");
        //Assert
        Assert.Null(id);
        _mockUsers.Verify(x => x.FindAsync(It.IsAny<FilterDefinition<User>>(),
               It.IsAny<FindOptions<User, User>>(),
               It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetUserByEmailAsync_LookupFails_ThrowsException()
    {
        //Arrange
        _mockUsers
           .Setup(u => u.FindAsync(
               It.IsAny<FilterDefinition<User>>(),
               It.IsAny<FindOptions<User, User>>(),
               It.IsAny<CancellationToken>()))
            .Throws(new MongoException("Lookup failed"));
        //Assert
        await Assert.ThrowsAsync<MongoException>(() => _service.GetUserByEmailAsync("test@test.com"));
    }


    [Fact]
public async Task CreateWebpageAndAnalysisResultAsync_WhenCalled_InsertsWebpageAndAnalysisResult()
{
    // Arrange
    var insertedWebpage = new Webpage();
    var expectedWebpageId = ObjectId.GenerateNewId().ToString(); // simulate MongoDB generated ID
    var insertedAnalysis = new WebpageAnalysisResult();

    _mockWebpages
        .Setup(x => x.InsertOneAsync(insertedWebpage, null, default))
        .Returns(Task.CompletedTask)
        .Callback<Webpage, InsertOneOptions, CancellationToken>((w, _, _) =>
        {
            w.Id = expectedWebpageId; // simulate ID assignment
        });

    _mockAnalysisResults
        .Setup(x => x.InsertOneAsync(It.IsAny<WebpageAnalysisResult>(), null, default))
        .Returns(Task.CompletedTask)
        .Callback<WebpageAnalysisResult, InsertOneOptions, CancellationToken>((analysis, _, _) =>
        {
            insertedAnalysis = analysis;
        });

    // Act
    var result = await _service.CreateWebpageAndAnalysisResultAsync(insertedWebpage, insertedAnalysis);

    // Assert
    Assert.Equal(expectedWebpageId, result);
    Assert.Equal(expectedWebpageId, insertedAnalysis.WebpageId);

    _mockWebpages.Verify(x => x.InsertOneAsync(insertedWebpage, null, default), Times.Once);
    _mockAnalysisResults.Verify(x => x.InsertOneAsync(insertedAnalysis, null, default), Times.Once);
}


    [Fact]
public async Task CreateWebpageAndAnalysisResultAsync_WhenWebpageInsertFails_ThrowsException()
{
    // Arrange
    var webpage = new Webpage();
    var analysisResult = new WebpageAnalysisResult();

    _mockWebpages
        .Setup(x => x.InsertOneAsync(webpage, null, default))
        .ThrowsAsync(new Exception("DB insert failed"));

    // Act & Assert
    var ex = await Assert.ThrowsAsync<Exception>(() =>
        _service.CreateWebpageAndAnalysisResultAsync(webpage, analysisResult));

    Assert.Equal("DB insert failed", ex.Message);
}

[Fact]
public async Task CreateWebpageAndAnalysisResultAsync_WhenWebpageAnalysisResultInsertFails_ThrowsException()
{
    // Arrange
    var webpage = new Webpage { Id = ObjectId.GenerateNewId().ToString() };
    var analysisResult = new WebpageAnalysisResult();

    _mockWebpages
        .Setup(x => x.InsertOneAsync(webpage, null, default))
        .Returns(Task.CompletedTask);

    _mockAnalysisResults
        .Setup(x => x.InsertOneAsync(It.IsAny<WebpageAnalysisResult>(), null, default))
        .ThrowsAsync(new Exception("DB insert failed"));

    // Act & Assert
    var ex = await Assert.ThrowsAsync<Exception>(() =>
        _service.CreateWebpageAndAnalysisResultAsync(webpage, analysisResult));

    Assert.Equal("DB insert failed", ex.Message);
}



    //UploadFileAsync
    [Fact]
    public async Task UploadFileAsync_WhenCalled_ReturnsObjectIdAsString()
    {
        // Arrange
        var stream = new MemoryStream();
        var fileName = "test.txt";
        var expectedId = ObjectId.GenerateNewId();

        _mockBucket
            .Setup(b => b.UploadFromStreamAsync(fileName, stream, null, default))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _service.UploadFileAsync(stream, fileName);

        // Assert
        Assert.Equal(expectedId.ToString(), result);
        _mockBucket.Verify(b => b.UploadFromStreamAsync(fileName, stream, null, default), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_WhenUploadFails_ThrowsException()
    {
        // Arrange
        var stream = new MemoryStream();
        var fileName = "fail.txt";

        _mockBucket
            .Setup(b => b.UploadFromStreamAsync(fileName, stream, null, default))
            .ThrowsAsync(new IOException("Upload failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<IOException>(() =>
            _service.UploadFileAsync(stream, fileName));

        Assert.Equal("Upload failed", ex.Message);
        _mockBucket.Verify(b => b.UploadFromStreamAsync(fileName, stream, null, default), Times.Once);
    }

    //DownloadFileAsync
    [Fact]
    public async Task DownloadFileAsync_WhenFileExists_ReturnsStreamAndFileName()
    {
        // Arrange
        var fileId = ObjectId.GenerateNewId().ToString();
        var objectId = ObjectId.Parse(fileId);
        var mockCursor = new Mock<IAsyncCursor<GridFSFileInfo>>();
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor
            .SetupGet(c => c.Current)
            .Returns(new List<GridFSFileInfo>
            {
                new GridFSFileInfo(new BsonDocument { { "filename", "test.txt" } })
            });

        _mockBucket
            .Setup(b => b.DownloadToStreamAsync(objectId, It.IsAny<Stream>(), null, default))
            .Returns<ObjectId, Stream, GridFSDownloadOptions, CancellationToken>((_, s, _, _) =>
            {
                using var writer = new StreamWriter(s, leaveOpen: true);
                writer.Write("dummy content");
                writer.Flush();
                return Task.CompletedTask;
            });

        _mockBucket
            .Setup(b => b.Find(It.IsAny<FilterDefinition<GridFSFileInfo>>(), null, default))
            .Returns(mockCursor.Object);

        // Act
        var (stream, returnedFileName) = await _service.DownloadFileAsync(fileId);

        // Assert
        Assert.NotNull(stream);
        Assert.Equal("test.txt", returnedFileName);
        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream!);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("dummy content", content);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenFileIdIsInvalid_ReturnsNulls()
    {
        // Arrange
        var invalidId = "not-an-object-id";

        // Act
        var (stream, fileName) = await _service.DownloadFileAsync(invalidId);

        // Assert
        Assert.Null(stream);
        Assert.Null(fileName);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenDownloadFails_ReturnsNulls()
    {
        // Arrange
        var fileId = ObjectId.GenerateNewId().ToString();
        var objectId = ObjectId.Parse(fileId);

        _mockBucket
            .Setup(b => b.DownloadToStreamAsync(objectId, It.IsAny<Stream>(), null, default))
            .ThrowsAsync(new IOException("Download failed"));

        // Act
        var (stream, fileName) = await _service.DownloadFileAsync(fileId);

        // Assert
        Assert.Null(stream);
        Assert.Null(fileName);
    }

    //ListWebpagesAsync
    [Fact]
    public async Task ListWebpagesAsync_WhenUserHasWebpages_ReturnsSummaries()
    {
        // Arrange
        var userId = "user123";

        var webpages = new List<Webpage>
    {
        new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Test Page 1",
            Url = "https://example.com",
            FileName = "test1.html",
            UploadDate = DateTime.UtcNow,
            UserId = userId
        }
        ,
        new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Test Page 2",
            Url = "https://example.com",
            FileName = "test2.html",
            UploadDate = DateTime.UtcNow,
            UserId = userId
        }
    };

        var summaries = webpages.Select(w => new WebpageSummary
        {
            Id = w.Id,
            Name = w.Name,
            UploadDate = w.UploadDate,
            FileName = w.FileName,
            Url = w.Url
        }).ToList();
        _mockWebpages
    .Setup(c => c.FindAsync(
        It.IsAny<FilterDefinition<Webpage>>(),
        It.IsAny<FindOptions<Webpage, WebpageSummary>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(MockNonEmptyCursor<WebpageSummary>(summaries));

        // Act
        var result = await _service.ListWebpagesAsync(userId);

        // Assert
        Assert.Equal(webpages.Count, result.Count);

        for (int i = 0; i < webpages.Count; i++)
        {
            Assert.Equal(webpages[i].Id, result[i].Id);
            Assert.Equal(webpages[i].Name, result[i].Name);
            Assert.Equal(webpages[i].Url, result[i].Url);
            Assert.Equal(webpages[i].FileName, result[i].FileName);
            Assert.Equal(webpages[i].UploadDate, result[i].UploadDate);
        }
    }

    public async Task ListWebpagesAsync_WhenUserHasNoWebpages_ReturnsEmptyList()
    {
        // Arrange
        var userId = "user123";
        var emptySummaries = new List<WebpageSummary>();

        _mockWebpages
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage, WebpageSummary>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockNonEmptyCursor<WebpageSummary>(emptySummaries));

        // Act
        var result = await _service.ListWebpagesAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListWebpagesAsync_WhenCursorIsNull_ThrowsException()
    {
        // Arrange
        var userId = "user123";

        _mockWebpages
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage, WebpageSummary>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncCursor<WebpageSummary>?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ListWebpagesAsync(userId));
    }

    //GetWebpageContentAndAnalysisAsync
    [Fact]
    public async Task GetWebpageContentAndAnalysisAsync_WhenWebpageNotFound_ReturnsNullTuple()
    {
        // Arrange
        _mockWebpages
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockEmptyCursor<Webpage>());

        // Act
        var (html, analysis) = await _service.GetWebpageContentAndAnalysisAsync("web123");

        // Assert
        Assert.Null(html);
        Assert.Null(analysis);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetWebpageContentAndAnalysisAsync_WhenHtmlContentIdMissing_ReturnsNullTuple(string? htmlContentId)
    {
        // Arrange
        var webpage = new Webpage { Id = "web123", HtmlContentId = htmlContentId };
        _mockWebpages
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockNonEmptyCursor(webpage));

        // Act
        var (html, analysis) = await _service.GetWebpageContentAndAnalysisAsync("web123");

        // Assert
        Assert.Null(html);
        Assert.Null(analysis);
    }

    [Fact]
    public async Task GetWebpageContentAndAnalysisAsync_WhenAnalysisResultMissing_ReturnsNullTuple()
    {
        // Arrange
        var fileId = ObjectId.GenerateNewId().ToString();
        var webpage = new Webpage
        {
            Id = "web123",
            HtmlContentId = fileId
        };

        _mockWebpages
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockNonEmptyCursor(webpage));

        // Simulate download returning content
        _mockBucket
            .Setup(b => b.DownloadToStreamAsync(
                ObjectId.Parse(fileId),
                It.IsAny<Stream>(),
                null,
                default))
            .Returns<ObjectId, Stream, GridFSDownloadOptions, CancellationToken>((_, stream, _, _) =>
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write("dummy html");
                writer.Flush();
                return Task.CompletedTask;
            });

        _mockAnalysisResults
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<WebpageAnalysisResult>>(),
                It.IsAny<FindOptions<WebpageAnalysisResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockEmptyCursor<WebpageAnalysisResult>());

        // Act
        var (html, analysis) = await _service.GetWebpageContentAndAnalysisAsync("web123");

        // Assert
        Assert.Null(html);
        Assert.Null(analysis);
    }

    [Fact]
    public async Task GetWebpageContentAndAnalysisAsync_WhenDataIsValid_ReturnsHtmlAndAnalysis()
    {
        // Arrange
        var fileId = ObjectId.GenerateNewId().ToString();
        var webpage = new Webpage
        {
            Id = "web123",
            HtmlContentId = fileId
        };

        var analysisResult = new WebpageAnalysisResult
        {
            WebpageId = "web123",
        };

        _mockWebpages
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockNonEmptyCursor(webpage));

        _mockBucket
            .Setup(b => b.DownloadToStreamAsync(
                ObjectId.Parse(fileId),
                It.IsAny<Stream>(),
                null,
                default))
            .Returns<ObjectId, Stream, GridFSDownloadOptions, CancellationToken>((_, stream, _, _) =>
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write("valid html content");
                writer.Flush();
                return Task.CompletedTask;
            });
        var mockCursor = new Mock<IAsyncCursor<GridFSFileInfo>>();
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor
            .SetupGet(c => c.Current)
            .Returns(new List<GridFSFileInfo>
            {
                new GridFSFileInfo(new BsonDocument { { "filename", "test.txt" } })
            });

        _mockBucket
           .Setup(b => b.Find(It.IsAny<FilterDefinition<GridFSFileInfo>>(), null, default))
           .Returns(mockCursor.Object);

        _mockAnalysisResults
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<WebpageAnalysisResult>>(),
                It.IsAny<FindOptions<WebpageAnalysisResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockNonEmptyCursor(analysisResult));

        // Act
        var (html, analysis) = await _service.GetWebpageContentAndAnalysisAsync("web123");

        // Assert
        Assert.Equal("valid html content", html);
        Assert.Equal(analysisResult.WebpageId, analysis.WebpageId);
    }

    [Fact]
    public async Task GetWebpageAsync_WhenWebpageIdIsNull_ReturnsNull()
    {
        // Act
        var result = await _service.GetWebpageAsync(null, "user123");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWebpageAsync_WhenUserIdIsNull_ReturnsNull()
    {
        // Act
        var result = await _service.GetWebpageAsync("web123", null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWebpageAsync_WhenWebpageNotFound_ReturnsNull()
    {
        // Arrange
        _mockWebpages
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage, Webpage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockEmptyCursor<Webpage>());

        // Act
        var result = await _service.GetWebpageAsync("web123", "user123");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWebpageAsync_WhenWebpageExists_ReturnsWebpage()
    {
        // Arrange
        var webpage = new Webpage { Id = "web123", UserId = "user123" };

        _mockWebpages
            .Setup(x => x.FindAsync(
                It.IsAny<FilterDefinition<Webpage>>(),
                It.IsAny<FindOptions<Webpage, Webpage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockNonEmptyCursor(webpage));

        // Act
        var result = await _service.GetWebpageAsync("web123", "user123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("web123", result!.Id);
        Assert.Equal("user123", result.UserId);
    }
}
