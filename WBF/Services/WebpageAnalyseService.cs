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
    Task<string> CreateWebpageAndLLMFeedbackAsync(Webpage webpage, LLMFeedback llmFeedback);
    Task<string> UploadFileAsync(Stream stream, string filename);
    Task<Stream?> DownloadFileAsync(string fileId);
    Task<List<WebpageSummary>> ListWebpagesAsync(string userId);
    Task<(string? HtmlContent, string? LLMResponse)> GetWebpageContentAndLLMAsync(string webpageId);
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
    private readonly IMongoCollection<LLMFeedback> _llmFeedbackCollection;
    private readonly GridFSBucket _bucket;
    public WebpageAnalyseService(IMongoDatabase mongoDatabase, IOptions<WebpageAnalyseDatabaseSettings> webpageAnalyseDatabaseSettings)
    {
        _webpagesCollection = mongoDatabase.GetCollection<Webpage>(webpageAnalyseDatabaseSettings.Value.WebpagesCollectionName);
        _userCollection = mongoDatabase.GetCollection<User>(webpageAnalyseDatabaseSettings.Value.UsersCollectionName);
        _llmFeedbackCollection = mongoDatabase.GetCollection<LLMFeedback>(webpageAnalyseDatabaseSettings.Value.LLMFeedbacksCollectionName);
        _bucket = new GridFSBucket(mongoDatabase);
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

    public async Task<string> CreateWebpageAndLLMFeedbackAsync(Webpage webpage, LLMFeedback llmFeedback)
    {
        await _webpagesCollection.InsertOneAsync(webpage);
        llmFeedback.WebpageId = webpage.Id;
        await _llmFeedbackCollection.InsertOneAsync(llmFeedback);
        return webpage.Id;
    }

    public async Task<string> UploadFileAsync(Stream stream, string filename)
    {
        return (await _bucket.UploadFromStreamAsync(filename, stream)).ToString();
    }

    public async Task<Stream?> DownloadFileAsync(string fileId)
    {
        try
        {
            var objectId = ObjectId.Parse(fileId);
            var stream = new MemoryStream();
            await _bucket.DownloadToStreamAsync(objectId, stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
        catch
        {
            return null;
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

    public async Task<(string? HtmlContent, string? LLMResponse)> GetWebpageContentAndLLMAsync(string webpageId)
{
    var webpage = await _webpagesCollection.Find(w => w.Id == webpageId).FirstOrDefaultAsync();
    if (webpage == null || string.IsNullOrEmpty(webpage.HtmlContentId))
        return (null, null);

    var stream = await DownloadFileAsync(webpage.HtmlContentId);
    if (stream == null)
        return (null, null);

    using var reader = new StreamReader(stream);
    var html = await reader.ReadToEndAsync();

    var llmFeedback = await _llmFeedbackCollection.Find(f => f.WebpageId == webpageId).FirstOrDefaultAsync();
    return (html, llmFeedback?.LLMResponse);
}
}