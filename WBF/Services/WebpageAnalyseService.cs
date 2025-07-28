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

public interface IWebpageAnalyseService
{
    Task<string?> CreateUserAsync(string email);
    Task<string?> GetUserByEmailAsync(string email);
    Task<string> CreateWebpageAndAnalysisResultAsync(Webpage webpage, WebpageAnalysisResult llmFeedback);
    Task<string> UploadFileAsync(Stream stream, string filename);
    Task<(Stream? stream, string? fileName)> DownloadFileAsync(string fileId);
    Task<List<WebpageSummary>> ListWebpagesAsync(string userId);
    Task<(string? HtmlContent, WebpageAnalysisResult? webpageAnalysisResult)> GetWebpageContentAndAnalysisAsync(string webpageId);
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
    public WebpageAnalyseService(IMongoDatabase mongoDatabase, IOptions<WebpageAnalyseDatabaseSettings> webpageAnalyseDatabaseSettings, IGridFSBucket? bucket = null)
    {
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

    public async Task<string?> GetUserByEmailAsync(string email)
    {
        return (await _userCollection.Find(u => u.Email == email).FirstOrDefaultAsync())?.Id;

    }

    public async Task<string> CreateWebpageAndAnalysisResultAsync(Webpage webpage, WebpageAnalysisResult webpageAnalysisResult)
    {
        using var session = await _mongoDatabase.Client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            await _webpagesCollection.InsertOneAsync(session, webpage);
            webpageAnalysisResult.WebpageId = webpage.Id;
            await _webpageAnalysisResultCollection.InsertOneAsync(session, webpageAnalysisResult);

            await session.CommitTransactionAsync();
            return webpage.Id;
        }
        catch(Exception e)
        {
            await session.AbortTransactionAsync();
            throw e;
        }
    }

    public async Task<string> UploadFileAsync(Stream stream, string filename)
    {
        return (await _bucket.UploadFromStreamAsync(filename, stream)).ToString();
    }

    public async Task<(Stream? stream, string? fileName)> DownloadFileAsync(string fileId)
    {
        try
        {
            var objectId = ObjectId.Parse(fileId);
            var stream = new MemoryStream();
            await _bucket.DownloadToStreamAsync(objectId, stream);
            var fileInfo = await _bucket.Find(Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, objectId)).FirstOrDefaultAsync();
            stream.Seek(0, SeekOrigin.Begin);
            string? fileName = fileInfo.Filename;
            return (stream, fileName);
        }
        catch
        {
            return (null, null);
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

    public async Task<(string? HtmlContent, WebpageAnalysisResult? webpageAnalysisResult)> GetWebpageContentAndAnalysisAsync(string webpageId)
    {
        var webpage = await _webpagesCollection.Find(w => w.Id == webpageId).FirstOrDefaultAsync();
        if (webpage == null || string.IsNullOrEmpty(webpage.HtmlContentId))
            return (null, null);

        var result = await DownloadFileAsync(webpage.HtmlContentId);
        Stream? stream = result.stream;
        if (stream == null)
            return (null, null);

        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();

        var webpageAnalysisResult = await _webpageAnalysisResultCollection.Find(f => f.WebpageId == webpageId).FirstOrDefaultAsync();
        if (webpageAnalysisResult == null) return (null, null);
        return (html, webpageAnalysisResult);
    }

    public async Task<Webpage?> GetWebpageAsync(string webpageId, string userId)
    {
        if (webpageId == null || userId == null) return null;
        return await _webpagesCollection.Find(webpage => webpage.Id == webpageId && webpage.UserId == userId).FirstOrDefaultAsync(); ;
    }
}