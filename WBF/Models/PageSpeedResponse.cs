using System.Text.Json.Serialization;

namespace WBF.Models;

public class PageSpeedResponse
{
    [JsonPropertyName("loadingExperience")]
    public LoadingExperience LoadingExperience { get; set; } = new();

    [JsonPropertyName("lighthouseResult")]
    public LighthouseResult LighthouseResult { get; set; } = new();
}

public class LoadingExperience
{
    [JsonPropertyName("metrics")]
    public Metrics Metrics { get; set; } = new();

    [JsonPropertyName("overall_category")]
    public string OverallCategory { get; set; } = string.Empty;

    public bool LabTest { get; set; } = false;
}

public class Metrics
{
    [JsonPropertyName("CUMULATIVE_LAYOUT_SHIFT_SCORE")]
    public MetricModel CUMULATIVE_LAYOUT_SHIFT_SCORE { get; set; } = new();

    [JsonPropertyName("EXPERIMENTAL_TIME_TO_FIRST_BYTE")]
    public MetricModel EXPERIMENTAL_TIME_TO_FIRST_BYTE { get; set; } = new();

    [JsonPropertyName("FIRST_CONTENTFUL_PAINT_MS")]
    public MetricModel FIRST_CONTENTFUL_PAINT_MS { get; set; } = new();

    [JsonPropertyName("INTERACTION_TO_NEXT_PAINT")]
    public MetricModel INTERACTION_TO_NEXT_PAINT { get; set; } = new();

    [JsonPropertyName("LARGEST_CONTENTFUL_PAINT_MS")]
    public MetricModel LARGEST_CONTENTFUL_PAINT_MS { get; set; } = new();
}

public class MetricModel
{
    [JsonPropertyName("percentile")]
    public int Percentile { get; set; } = 0;

    [JsonPropertyName("distributions")]
    public Distribution[] Distributions { get; set; } = Array.Empty<Distribution>();

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
}

public class Distribution
{
    [JsonPropertyName("min")]
    public int Min { get; set; } = 0;

    [JsonPropertyName("max")]
    public int? Max { get; set; } = 0;

    [JsonPropertyName("proportion")]
    public float Proportion { get; set; } = 0f;
}

public class LighthouseResult
{
    [JsonPropertyName("categories")]
    public LighthouseCategories Categories { get; set; } = new();
}

public class LighthouseCategories
{
    [JsonPropertyName("performance")]
    public CategoryScore Performance { get; set; } = new();

    [JsonPropertyName("seo")]
    public CategoryScore Seo { get; set; } = new();

    [JsonPropertyName("best-practices")]
    public CategoryScore BestPractices { get; set; } = new();

    [JsonPropertyName("accessibility")]
    public CategoryScore Accessibility { get; set; } = new();
}

public class CategoryScore
{
    [JsonPropertyName("score")]
    public float Score { get; set; } = 0f;
}

public class PageSpeedAPIConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string API_KEY { get; set; } = string.Empty;
}
