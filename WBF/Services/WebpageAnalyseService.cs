using WBF.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
namespace WBF.Services;

public class WebpageSummary
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public DateTime? UploadDate { get; set; }
    public string? FileName { get; set; }
    public string? Url { get; set; }
}

public class WebpageContentAndAnalysisResult
{
    public string? HtmlContent { get; set; }
    public WebpageAnalysisResult? WebpageAnalysisResult { get; set; }
}
public class FileDownloadResult
{
    public Stream? Stream { get; set; }
    public string? FileName { get; set; }
}

public interface IWebpageAnalyseService
{
    Task<string?> CreateUserAsync(string email);
    Task<string?> GetUserByEmailAsync(string email);
    Task<string> CreateWebpageAndAnalysisResultAsync(Webpage webpage, WebpageAnalysisResult llmFeedback);
    Task<string> UploadFileAsync(Stream stream, string filename);
    Task<FileDownloadResult> DownloadFileAsync(string fileId);
    Task<List<WebpageSummary>> ListWebpagesAsync(string userId);
    Task<WebpageContentAndAnalysisResult> GetWebpageContentAndAnalysisAsync(string webpageId);
    Task<Webpage?> GetWebpageAsync(string webpageId, string userId);
}

public class WebpageAnalyseService : IWebpageAnalyseService
{
    // Services: User account creation
    // User account verification
    // Webpage document creation
    // List webpages ids with name, upload date filename/url
    // Download file (html, design, specs)
    // Upload file (html, design, specs)
    private readonly IMongoCollection<Webpage> _webpagesCollection;
    private readonly IMongoCollection<User> _userCollection;
    private readonly IMongoCollection<WebpageAnalysisResult> _webpageAnalysisResultCollection;
    private readonly IGridFSBucket _bucket;
    private readonly IMongoDatabase _mongoDatabase;
    private readonly ILogger<WebpageAnalyseService> _logger;
    public WebpageAnalyseService(IMongoDatabase mongoDatabase, IOptions<WebpageAnalyseDatabaseSettings> webpageAnalyseDatabaseSettings, ILogger<WebpageAnalyseService> logger, IGridFSBucket? bucket = null)
    {
        _logger = logger;
        _mongoDatabase = mongoDatabase;
        _webpagesCollection = mongoDatabase.GetCollection<Webpage>(webpageAnalyseDatabaseSettings.Value.WebpagesCollectionName);
        _userCollection = mongoDatabase.GetCollection<User>(webpageAnalyseDatabaseSettings.Value.UsersCollectionName);
        _webpageAnalysisResultCollection = mongoDatabase.GetCollection<WebpageAnalysisResult>(webpageAnalyseDatabaseSettings.Value.WebpageAnalysisResultsCollectionName);
        if (bucket != null)
        {
            _bucket = bucket;
        }
        else
        {
            _bucket = new GridFSBucket(mongoDatabase);
        }
    }
    /// <summary>
    /// Creates a new user with the specified email if one does not already exist.
    /// </summary>
    /// <param name="email">The email address of the user to create.</param>
    /// <returns>The ID of the newly created user, or null if the user already exists.</returns>
    /// <exception cref="MongoException">
    /// Thrown if there is an error inserting the user into the database.
    /// </exception>
    public async Task<string?> CreateUserAsync(string email)
    {
        if (await GetUserByEmailAsync(email) != null)
        {
            return null;
        }
        var user = new User { Email = email };
        await _userCollection.InsertOneAsync(user);
        return user.Id;
    }
    /// <summary>
    /// Retrieves the ID of a user with the specified email.
    /// </summary>
    /// <param name="email">The email address of the user to find.</param>
    /// <returns>The user ID if found; otherwise, null.</returns>
    /// <exception cref="MongoException">
    /// Thrown if there is an error querying the database.
    /// </exception>
    public async Task<string?> GetUserByEmailAsync(string email)
    {
        return (await _userCollection.Find(u => u.Email == email).FirstOrDefaultAsync())?.Id;

    }
    /// <summary>
    /// Creates a new webpage and its associated analysis result in the database.
    /// </summary>
    /// <param name="webpage">The webpage to insert.</param>
    /// <param name="webpageAnalysisResult">The analysis result to associate with the webpage.</param>
    /// <returns>The ID of the created webpage.</returns>
    /// <exception cref="MongoException">
    /// Thrown if there is an error inserting the webpage or analysis result into the database.
    /// </exception>
    public async Task<string> CreateWebpageAndAnalysisResultAsync(Webpage webpage, WebpageAnalysisResult webpageAnalysisResult)
    {


        await _webpagesCollection.InsertOneAsync(webpage);
        webpageAnalysisResult.WebpageId = webpage.Id;
        await _webpageAnalysisResultCollection.InsertOneAsync(webpageAnalysisResult);

        return webpage.Id;

    }
    /// <summary>
    /// Uploads a file stream to GridFS with the specified filename.
    /// </summary>
    /// <param name="stream">The file stream to upload.</param>
    /// <param name="filename">The name to assign to the uploaded file.</param>
    /// <returns>The ID of the uploaded file as a string.</returns>
    /// <exception cref="MongoException">
    /// Thrown if there is an error during the file upload to GridFS.
    /// </exception>
    public async Task<string> UploadFileAsync(Stream stream, string filename)
    {
        return (await _bucket.UploadFromStreamAsync(filename, stream)).ToString();
    }

    /// <summary>
    /// Downloads a file from GridFS by its ID.
    /// </summary>
    /// <param name="fileId">The ID of the file to download.</param>
    /// <returns>
    /// A <see cref="FileDownloadResult"/> containing the file stream and filename if found; otherwise null values.
    /// </returns>
    /// <remarks>
    /// Logs and suppresses exceptions; does not throw.
    /// </remarks>
    public async Task<FileDownloadResult> DownloadFileAsync(string fileId)
    {
        try
        {
            var objectId = ObjectId.Parse(fileId);
            var stream = new MemoryStream();

            await _bucket.DownloadToStreamAsync(objectId, stream);
            var fileInfo = await _bucket
                .Find(Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, objectId))
                .FirstOrDefaultAsync();

            stream.Seek(0, SeekOrigin.Begin);

            return new FileDownloadResult
            {
                Stream = stream,
                FileName = fileInfo?.Filename
            };
        }
        catch (Exception error)
        {
            _logger.LogError($"Error in Services.DownloadFileAsync: {error.Message}");
            return new FileDownloadResult(); // returns object with null properties
        }
    }

    public async Task<List<WebpageSummary>> ListWebpagesAsync(string userId)
    {
        return await _webpagesCollection
            .Find(w => w.UserId == userId)
            .Project(w => new WebpageSummary
            {
                Id = w.Id,
                Name = w.Name,
                UploadDate = w.UploadDate,
                FileName = w.FileName,
                Url = w.Url
            })
            .ToListAsync();
    }



    /// <summary>
    /// Retrieves the HTML content and analysis result for a specified webpage.
    /// </summary>
    /// <param name="webpageId">The ID of the webpage.</param>
    /// <returns>
    /// A <see cref="WebpageContentAndAnalysisResult"/> containing the HTML content and analysis result,
    /// or null values if the webpage or its content is not found.
    /// </returns>
    /// <exception cref="MongoException">
    /// Thrown if there is an error querying the database or retrieving the file.
    /// </exception>
    public async Task<WebpageContentAndAnalysisResult> GetWebpageContentAndAnalysisAsync(string webpageId)
    {
        var webpage = await _webpagesCollection.Find(w => w.Id == webpageId).FirstOrDefaultAsync();
        if (webpage == null || string.IsNullOrEmpty(webpage.HtmlContentId))
            return new WebpageContentAndAnalysisResult();

        var result = await DownloadFileAsync(webpage.HtmlContentId);
        Stream? stream = result.Stream;
        if (stream == null)
            return new WebpageContentAndAnalysisResult();

        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();

        var webpageAnalysisResult = await _webpageAnalysisResultCollection
            .Find(f => f.WebpageId == webpageId)
            .FirstOrDefaultAsync();

        if (webpageAnalysisResult == null)
            return new WebpageContentAndAnalysisResult();

        return new WebpageContentAndAnalysisResult
        {
            HtmlContent = html,
            WebpageAnalysisResult = webpageAnalysisResult
        };
    }

    /// <summary>
    /// Retrieves a webpage by its ID and associated user ID.
    /// </summary>
    /// <param name="webpageId">The ID of the webpage.</param>
    /// <param name="userId">The ID of the user who owns the webpage.</param>
    /// <returns>The <see cref="Webpage"/> if found; otherwise, null.</returns>
    /// <exception cref="MongoException">
    /// Thrown if there is an error querying the database.
    /// </exception>
    public async Task<Webpage?> GetWebpageAsync(string webpageId, string userId)
    {
        if (webpageId == null || userId == null) return null;
        return await _webpagesCollection.Find(webpage => webpage.Id == webpageId && webpage.UserId == userId).FirstOrDefaultAsync(); ;
    }
}