namespace WBF.Models;

public class WebAuditResults
{
    public List<AxeCoreViolation>? axeCoreResult { get; set; } = [];
    public PageSpeedResponse? pageSpeedResult { get; set; } = null;
    public List<NuValidatorMessage>? nuValidatorResult { get; set; } = [];
    public List<ResponsivenessMetrics>? responsivenessResult { get; set; } = [];

}