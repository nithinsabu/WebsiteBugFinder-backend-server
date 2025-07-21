using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WBF.Models;
public class WebpageAnalysisResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string WebpageId { get; set; } = null!;

    public LLMResponse? LLMResponse { get; set; } = null;
    public WebAuditResults? webAuditResults { get; set; } = null;
}