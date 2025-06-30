using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WBF.Models;

public class Webpage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string HtmlContentId { get; set; } = null!;

    public string? Url { get; set; }

    public string? FileName { get; set; }

    public string? Name { get; set; }
    
    [BsonRepresentation(BsonType.ObjectId)]
    public string? DesignFileId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? SpecificationFileId { get; set; }

    public DateTime? UploadDate { get; set; } = DateTime.UtcNow;


}