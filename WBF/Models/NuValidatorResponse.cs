using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WBF.Models;

public class NuValidatorMessage
{
    [BsonElement("Type")]
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("Last Line")]
    [JsonPropertyName("Last Line")]
    public int LastLine { get; set; } = 0;

    [BsonElement("Last Column")]
    [JsonPropertyName("Last Column")]
    public int LastColumn { get; set; }= 0;

    [BsonElement("First Column")]
    [JsonPropertyName("First Column")]
    public int FirstColumn { get; set; }= 0;

    [BsonElement("Message")]
    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("Extract")]
    [JsonPropertyName("Extract")]
    public string Extract { get; set; } = string.Empty;

    [BsonElement("HiliteStart")]
    [JsonPropertyName("HiliteStart")]
    public int HiliteStart { get; set; }= 0;

    [BsonElement("HiliteLength")]
    [JsonPropertyName("HiliteLength")]
    public int HiliteLength { get; set; }= 0;
}

public class NuValidatorResponse
{
    public List<NuValidatorMessage> Messages { get; set; } = new();
}