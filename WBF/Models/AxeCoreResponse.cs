using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace WBF.Models;

public class AxeCoreNode
{
    [BsonElement("Impact")]
    [JsonPropertyName("Impact")]
    public string? Impact { get; set; } = null;

    [BsonElement("Html")]
    [JsonPropertyName("Html")]
    public string? Html { get; set; }= null;

    [BsonElement("FailureSummary")]
    [JsonPropertyName("FailureSummary")]
    public string? FailureSummary { get; set; }= null;
}

public class AxeCoreViolation
{
    [BsonElement("Id")]
    [JsonPropertyName("Id")]
    public string? Id { get; set; }= null;

    [BsonElement("Description")]
    [JsonPropertyName("Description")]
    public string? Description { get; set; }= null;

    [BsonElement("Help")]
    [JsonPropertyName("Help")]
    public string? Help { get; set; }= null;

    [BsonElement("Nodes")]
    [JsonPropertyName("Nodes")]
    public List<AxeCoreNode> Nodes { get; set; } = new();
}

public class ResponsivenessMetrics
{
[BsonElement("Viewport")]
    [JsonPropertyName("Viewport")]
    public string? Viewport { get; set; }= null;
    [BsonElement("Overflow")]
    [JsonPropertyName("Overflow")]
    public bool? Overflow { get; set; }= null;
    [BsonElement("ImagesOversize")]
    [JsonPropertyName("ImagesOversize")]
    public bool? ImagesOversize { get; set; }= null;
}
public class AxeCoreResponse
{
    public List<AxeCoreViolation> violations { get; set; } = [];
    public PageSpeedResponse? lightHouseResults { get; set; } = null;

    public List<ResponsivenessMetrics> responsivenessResults { get; set; } = [];
}
