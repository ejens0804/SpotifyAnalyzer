using SpotifyAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SpotifyService>(); // Singleton keeps the token in memory
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

// Allow CORS for local frontend development
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
    c.RoutePrefix = "swagger"; // Swagger now lives at /swagger
});

app.UseCors("AllowAll");
app.UseDefaultFiles();  // Serves wwwroot/index.html at root
app.UseStaticFiles();   // Serves all files from wwwroot
app.UseAuthorization();
app.MapControllers();

app.Run("http://127.0.0.1:5000");