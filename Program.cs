// my-spotify-stats-e8bva9c5fme7fxew.northcentralus-01.azurewebsites.net

using SpotifyAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SpotifyService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Spotify Stats API",
        Version = "v1",
        Description = "A REST API that connects to Spotify to return personalized music stats, " +
                      "top tracks, top artists, playlist data, and mood analysis."
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Middleware pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Spotify Stats API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

// Azure sets PORT env var; locally fall back to 5000
if (app.Environment.IsDevelopment())
{
    app.Run("http://127.0.0.1:5000");
}
else
{
    app.Run();
}