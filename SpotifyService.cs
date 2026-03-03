using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpotifyAPI.Models;

namespace SpotifyAPI.Services
{
    public class SpotifyService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        // In a real app, store tokens per user in a database.
        // For this project, we store in memory (single user).
        private string _accessToken = "";
        private string _refreshToken = "";
        private DateTime _tokenExpiry = DateTime.MinValue;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public SpotifyService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        // ─── Auth ──────────────────────────────────────────────────────────────

        public string GetAuthorizationUrl()
        {
            var clientId = _config["Spotify:ClientId"];
            var redirectUri = Uri.EscapeDataString(_config["Spotify:RedirectUri"]!);
            var scopes = Uri.EscapeDataString(
                "user-read-private user-read-email user-top-read playlist-read-private playlist-read-collaborative"
            );

            return $"https://accounts.spotify.com/authorize" +
                   $"?response_type=code" +
                   $"&client_id={clientId}" +
                   $"&scope={scopes}" +
                   $"&redirect_uri={redirectUri}";
        }

        public async Task<bool> ExchangeCodeForTokenAsync(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var clientId = _config["Spotify:ClientId"];
            var clientSecret = _config["Spotify:ClientSecret"];
            var redirectUri = _config["Spotify:RedirectUri"];

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri!
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<SpotifyTokenResponse>(json, _jsonOptions);
            if (token == null) return false;

            _accessToken = token.AccessToken;
            _refreshToken = token.RefreshToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);
            return true;
        }

        private async Task RefreshAccessTokenAsync()
        {
            var client = _httpClientFactory.CreateClient();
            var clientId = _config["Spotify:ClientId"];
            var clientSecret = _config["Spotify:ClientSecret"];
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _refreshToken
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<SpotifyTokenResponse>(json, _jsonOptions);
            if (token == null) return;

            _accessToken = token.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);
        }

        public bool IsAuthenticated() => !string.IsNullOrEmpty(_accessToken);

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            if (DateTime.UtcNow >= _tokenExpiry && !string.IsNullOrEmpty(_refreshToken))
                await RefreshAccessTokenAsync();

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            return client;
        }

        // ─── API Calls ─────────────────────────────────────────────────────────

        public async Task<SpotifyUser?> GetCurrentUserAsync()
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("https://api.spotify.com/v1/me");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SpotifyUser>(json, _jsonOptions);
        }

        public async Task<List<TrackSummary>> GetTopTracksAsync(string timeRange = "medium_term", int limit = 20)
        {
            limit = Math.Clamp(limit, 1, 50);
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/tracks?time_range={timeRange}&limit={limit}"
            );
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SpotifyTopTracksResponse>(json, _jsonOptions);
            if (data == null) return new();

            return data.Items.Select(t => new TrackSummary
            {
                Name = t.Name,
                Artist = t.Artists.FirstOrDefault()?.Name ?? "Unknown",
                Album = t.Album.Name,
                Popularity = t.Popularity,
                DurationFormatted = FormatDuration(t.DurationMs),
                AlbumArtUrl = t.Album.Images.FirstOrDefault()?.Url ?? "",
                PreviewUrl = t.PreviewUrl
            }).ToList();
        }

        public async Task<List<ArtistSummary>> GetTopArtistsAsync(string timeRange = "medium_term", int limit = 20)
        {
            limit = Math.Clamp(limit, 1, 50);
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/artists?time_range={timeRange}&limit={limit}"
            );
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SpotifyTopArtistsResponse>(json, _jsonOptions);
            if (data == null) return new();

            return data.Items.Select(a => new ArtistSummary
            {
                Name = a.Name,
                Popularity = a.Popularity,
                Genres = a.Genres,
                ImageUrl = a.Images.FirstOrDefault()?.Url ?? ""
            }).ToList();
        }

        public async Task<List<PlaylistSummary>> GetPlaylistsAsync(string? sortBy = null)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("https://api.spotify.com/v1/me/playlists?limit=50");
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SpotifyPlaylistsResponse>(json, _jsonOptions);
            if (data == null) return new();

            var summaries = data.Items.Select(p => new PlaylistSummary
            {
                Name = p.Name,
                Description = p.Description,
                TrackCount = p.Tracks.Total,
                IsPublic = p.Public,
                ImageUrl = p.Images.FirstOrDefault()?.Url ?? ""
            }).ToList();

            // Filter/Sort based on query param
            return sortBy?.ToLower() switch
            {
                "size" => summaries.OrderByDescending(p => p.TrackCount).ToList(),
                "name" => summaries.OrderBy(p => p.Name).ToList(),
                _ => summaries
            };
        }

        public async Task<MoodSummary?> GetMoodSummaryAsync(string timeRange = "medium_term")
        {
            // Step 1: Get top tracks to retrieve IDs
            var client = await GetAuthenticatedClientAsync();
            var tracksResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/tracks?time_range={timeRange}&limit=50"
            );
            if (!tracksResponse.IsSuccessStatusCode) return null;

            var tracksJson = await tracksResponse.Content.ReadAsStringAsync();
            var tracksData = JsonSerializer.Deserialize<SpotifyTopTracksResponse>(tracksJson, _jsonOptions);
            if (tracksData == null || tracksData.Items.Count == 0) return null;

            var trackIds = string.Join(",", tracksData.Items.Select(t => t.Id).Take(50));
            var avgPopularity = tracksData.Items.Average(t => t.Popularity);

            // Step 2: Get audio features for those tracks
            var featuresResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/audio-features?ids={trackIds}"
            );
            if (!featuresResponse.IsSuccessStatusCode) return null;

            var featuresJson = await featuresResponse.Content.ReadAsStringAsync();
            var featuresData = JsonSerializer.Deserialize<SpotifyAudioFeaturesResponse>(featuresJson, _jsonOptions);
            if (featuresData?.AudioFeatures == null || featuresData.AudioFeatures.Count == 0) return null;

            var features = featuresData.AudioFeatures;
            var avgEnergy = features.Average(f => f.Energy);
            var avgValence = features.Average(f => f.Valence);
            var avgDance = features.Average(f => f.Danceability);
            var avgTempo = features.Average(f => f.Tempo);

            // Step 3: Determine mood label
            string mood = DetermineMood(avgEnergy, avgValence);
            string description = GenerateMoodDescription(avgEnergy, avgValence, avgDance, avgTempo);

            return new MoodSummary
            {
                OverallMood = mood,
                AverageEnergy = Math.Round(avgEnergy, 2),
                AverageValence = Math.Round(avgValence, 2),
                AverageDanceability = Math.Round(avgDance, 2),
                AverageTempo = Math.Round(avgTempo, 1),
                AveragePopularity = Math.Round(avgPopularity, 1),
                MoodDescription = description
            };
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static string FormatDuration(int ms)
        {
            var ts = TimeSpan.FromMilliseconds(ms);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }

        private static string DetermineMood(double energy, double valence)
        {
            return (energy >= 0.5, valence >= 0.5) switch
            {
                (true, true) => "Happy & Energetic",
                (true, false) => "Intense & Angry",
                (false, true) => "Calm & Content",
                (false, false) => "Sad & Melancholic"
            };
        }

        private static string GenerateMoodDescription(double energy, double valence, double dance, double tempo)
        {
            var parts = new List<string>();

            if (energy > 0.7) parts.Add("Your listening is highly energetic");
            else if (energy < 0.4) parts.Add("You tend to listen to chill, low-energy music");
            else parts.Add("Your music has moderate energy");

            if (valence > 0.6) parts.Add("with a generally positive and upbeat vibe");
            else if (valence < 0.4) parts.Add("with a more somber or melancholic tone");
            else parts.Add("with a balanced emotional tone");

            if (dance > 0.7) parts.Add("that's very danceable");
            if (tempo > 140) parts.Add($"and a fast average tempo of {tempo:F0} BPM");
            else if (tempo < 90) parts.Add($"and a slow average tempo of {tempo:F0} BPM");

            return string.Join(" ", parts) + ".";
        }
    }
}
