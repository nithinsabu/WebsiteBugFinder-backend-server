using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
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

   [BsonElement("AxeCoreError")]
        [JsonPropertyName("AxeCoreError")]
        public bool AxeCoreError { get; set; } = true;

        [BsonElement("NuValidatorError")]
        [JsonPropertyName("NuValidatorError")]
        public bool NuValidatorError { get; set; } = true;

        [BsonElement("PageSpeedError")]
        [JsonPropertyName("PageSpeedError")]
        public bool PageSpeedError { get; set; } = true;

        [BsonElement("LLMError")]
        [JsonPropertyName("LLMError")]
        public bool LLMError { get; set; } = true;

        [BsonElement("ResponsivenessError")]
        [JsonPropertyName("ResponsivenessError")]
        public bool ResponsivenessError { get; set; } = true;

}