using WBF.Models;
using WBF.Services;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using WBF.Controllers;
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()    
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<WebpageAnalyseDatabaseSettings>(
    builder.Configuration.GetSection("WebpageAnalyseDatabase"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<WebpageAnalyseDatabaseSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<WebpageAnalyseDatabaseSettings>>().Value;
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(settings.DatabaseName);
});
builder.Services.AddScoped<IWebpageAnalyseService, WebpageAnalyseService>();
    // Console.WriteLine(builder.Configuration["PythonServer:ConnectionString"]);

builder.Services.AddHttpClient("PythonServer", client =>
{
    var baseUrl = builder.Configuration["PythonServer:ConnectionString"];
    if (string.IsNullOrEmpty(baseUrl))
        throw new InvalidOperationException("Missing config: PythonServer:ConnectionString");
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();
app.Logger.LogInformation("Starting the app");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();
