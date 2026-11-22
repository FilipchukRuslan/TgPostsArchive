
using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data;
using TelegramPostsArchive.Services;
using TelegramPostsArchive.Services.BackgroundWorkers;

var builder = WebApplication.CreateBuilder(args);

// API
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Telegram HTTP client
builder.Services.AddSingleton<TelegramRequestBridge>();

builder.Services.AddHttpClient<TelegramPageClient>(client =>
{
    client.BaseAddress = new Uri("https://t.me/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "TelegramPostsArchive/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
});

// Database
builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSql")));

builder.Services.AddSingleton<PostsValidationService>();

// Collection services
builder.Services.AddSingleton<TelegramHtmlParser>();
builder.Services.AddScoped<TelegramPostStore>();
builder.Services.AddScoped<TelegramCollector>();

// Background collection
builder.Services.AddOptions<PollingOptions>()
    .BindConfiguration("Polling")
    .Validate(options => options.Interval > TimeSpan.Zero,
        "Polling interval must be positive.")
    .ValidateOnStart();

builder.Services.AddHostedService<BackgroundCollectionWorker>();

var app = builder.Build();

// Apply database migrations
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<DataContext>();

    await db.Database.MigrateAsync();
}

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

await app.RunAsync();
