using TelegramPostsArchive.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<TelegramPageClient>(client =>
{
    client.BaseAddress = new Uri("https://t.me/");
    client.Timeout = TimeSpan.FromSeconds(30);

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "TelegramPostsArchive/1.0");

    client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
});

builder.Services.AddSingleton(new TelegramHtmlParser());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
