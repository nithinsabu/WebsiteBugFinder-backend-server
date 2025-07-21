using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace WBF.Models;

public class LLMResponse
{
    [BsonElement("Executive Summary")]
    [JsonPropertyName("Executive Summary")]
    public string? ExecutiveSummary { get; set; } = null;

    [BsonElement("Detailed Analysis")]
    [JsonPropertyName("Detailed Analysis")]
    public DetailedAnalysis? DetailedAnalysis { get; set; } = null;

    [BsonElement("Non-LLM Evaluations")]
    [JsonPropertyName("Non-LLM Evaluations")]
    public NonLLMEvaluations? NonLLMEvaluations { get; set; } = null;

    [BsonElement("Other Issues")]
    [JsonPropertyName("Other Issues")]
    public List<OtherIssue>? OtherIssues { get; set; } = new();
}

public class DetailedAnalysis
{
    [BsonElement("Content Discrepancies")]
    [JsonPropertyName("Content Discrepancies")]
    public ContentDiscrepancy? ContentDiscrepancies { get; set; } = null;

    [BsonElement("Styling Discrepancies")]
    [JsonPropertyName("Styling Discrepancies")]
    public StylingDiscrepancy? StylingDiscrepancies { get; set; } = null;

    [BsonElement("Intentional Flaws And Known Issues")]
    [JsonPropertyName("Intentional Flaws And Known Issues")]
    public IntentionalFlawsAndKnownIssues? IntentionalFlawsAndKnownIssues { get; set; } = null;

    [BsonElement("Functional Discrepancies")]
    [JsonPropertyName("Functional Discrepancies")]
    public FunctionalDiscrepancy? FunctionalDiscrepancies { get; set; } = null;
}

public class ContentDiscrepancy
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Findings")]
    [JsonPropertyName("Findings")]
    public List<ContentFinding>? Findings { get; set; } = new();
}

public class ContentFinding
{
    [BsonElement("Section")]
    [JsonPropertyName("Section")]
    public string? Section { get; set; } = null;

    [BsonElement("Issue")]
    [JsonPropertyName("Issue")]
    public string? Issue { get; set; } = null;

    [BsonElement("Details")]
    [JsonPropertyName("Details")]
    public string? Details { get; set; } = null;

    [BsonElement("Code")]
    [JsonPropertyName("Code")]
    public string? Code { get; set; } = null;

    [BsonElement("Recommended Fix")]
    [JsonPropertyName("Recommended Fix")]
    public string? RecommendedFix { get; set; } = null;
}

public class StylingDiscrepancy
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Findings")]
    [JsonPropertyName("Findings")]
    public List<ContentFinding>? Findings { get; set; } = new();
}

public class IntentionalFlawsAndKnownIssues
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Findings")]
    [JsonPropertyName("Findings")]
    public List<IntentionalFinding>? Findings { get; set; } = new();
}

public class IntentionalFinding
{
    [BsonElement("Category")]
    [JsonPropertyName("Category")]
    public string? Category { get; set; } = null;

    [BsonElement("Issue")]
    [JsonPropertyName("Issue")]
    public string? Issue { get; set; } = null;

    [BsonElement("Details")]
    [JsonPropertyName("Details")]
    public string? Details { get; set; } = null;

    [BsonElement("Recommended Fix")]
    [JsonPropertyName("Recommended Fix")]
    public string? RecommendedFix { get; set; } = null;
}

public class FunctionalDiscrepancy
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Findings")]
    [JsonPropertyName("Findings")]
    public List<FunctionalFinding>? Findings { get; set; } = new();
}

public class FunctionalFinding
{
    [BsonElement("Issue")]
    [JsonPropertyName("Issue")]
    public string? Issue { get; set; } = null;

    [BsonElement("Details")]
    [JsonPropertyName("Details")]
    public string? Details { get; set; } = null;

    [BsonElement("Code")]
    [JsonPropertyName("Code")]
    public string? Code { get; set; } = null;

    [BsonElement("Recommended Fix")]
    [JsonPropertyName("Recommended Fix")]
    public string? RecommendedFix { get; set; } = null;
}

public class NonLLMEvaluations
{
    [BsonElement("Accessibility Report")]
    [JsonPropertyName("Accessibility Report")]
    public AccessibilityReport? AccessibilityReport { get; set; } = null;

    [BsonElement("Performance Report")]
    [JsonPropertyName("Performance Report")]
    public PerformanceReport? PerformanceReport { get; set; } = null;

    [BsonElement("Validation Report")]
    [JsonPropertyName("Validation Report")]
    public ValidationReport? ValidationReport { get; set; } = null;

    [BsonElement("Layout Report")]
    [JsonPropertyName("Layout Report")]
    public LayoutReport? LayoutReport { get; set; } = null;
}

public class AccessibilityReport
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Key Findings")]
    [JsonPropertyName("Key Findings")]
    public List<KeyFinding>? KeyFindings { get; set; } = new();
}

public class PerformanceReport
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Key Findings")]
    [JsonPropertyName("Key Findings")]
    public List<KeyFinding>? KeyFindings { get; set; } = new();
}

public class ValidationReport
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Key Findings")]
    [JsonPropertyName("Key Findings")]
    public List<KeyFinding>? KeyFindings { get; set; } = new();
}

public class LayoutReport
{
    [BsonElement("Summary")]
    [JsonPropertyName("Summary")]
    public string? Summary { get; set; } = null;

    [BsonElement("Recommended Fix")]
    [JsonPropertyName("Recommended Fix")]
    public string? RecommendedFix { get; set; } = null;
}

public class KeyFinding
{
    [BsonElement("Issue")]
    [JsonPropertyName("Issue")]
    public string? Issue { get; set; } = null;

    [BsonElement("Recommended Fix")]
    [JsonPropertyName("Recommended Fix")]
    public string? RecommendedFix { get; set; } = null;
}

public class OtherIssue
{
    [BsonElement("Issue")]
    [JsonPropertyName("Issue")]
    public string? Issue { get; set; } = null;

    [BsonElement("Details")]
    [JsonPropertyName("Details")]
    public string? Details { get; set; } = null;

    [BsonElement("Code")]
    [JsonPropertyName("Code")]
    public string? Code { get; set; } = null;

    [BsonElement("Recommended Fix")]
    [JsonPropertyName("Recommended Fix")]
    public string? RecommendedFix { get; set; } = null;
}
