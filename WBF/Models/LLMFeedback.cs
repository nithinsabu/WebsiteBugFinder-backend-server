using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WBF.Models;

public class LLMFeedback
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string WebpageId { get; set; } = null!;

    public string? LLMResponse { get; set; } = null!;
}