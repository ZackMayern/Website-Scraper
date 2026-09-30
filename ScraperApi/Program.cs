using MongoDB.Driver;
using ScraperApi.Repositories;
using ScraperApi.Services;

// Build the web application host and register the app services.
var builder = WebApplication.CreateBuilder(args);

// Read the MongoDB connection string from configuration and fail fast if it is missing.
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb")
    ?? throw new InvalidOperationException("Connection string 'MongoDb' is not configured.");

// Register API, OpenAPI, HTTP client, database, repository, and background worker services.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<ScraperService>();
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
    serviceProvider.GetRequiredService<IMongoClient>().GetDatabase("scraper_db"));
builder.Services.AddScoped<GameRepository>();
builder.Services.AddHostedService<ScrapeWorker>();

// Build the middleware pipeline.
var app = builder.Build();

// Enable Swagger only in development so the generated API docs stay out of production.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirect HTTP traffic to HTTPS, map the API controllers, and expose a lightweight health check.
app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => new
{
    status = "Healthy",
    utcTime = DateTime.UtcNow
});

app.Run();
