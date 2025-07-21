namespace WBF.Models;

public class WebpageAnalyseDatabaseSettings {
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; }= null!;
    public string UsersCollectionName { get; set; }= null!;
    public string WebpagesCollectionName { get; set; }= null!;
    public string WebpageAnalysisResultsCollectionName { get; set; }= null!;
}