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

        // ─── Genre-to-Mood keyword mappings ────────────────────────────────────
        private static readonly HashSet<string> EnergeticKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "rock", "metal", "punk", "hardcore", "hard rock", "heavy metal",
            "thrash", "grunge", "electronic", "edm", "dance", "techno",
            "house", "drum and bass", "dubstep", "trance", "hardstyle",
            "industrial", "garage", "breakbeat", "rave"
        };

        private static readonly HashSet<string> HappyKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "pop", "funk", "disco", "reggae", "ska", "tropical",
            "latin", "k-pop", "j-pop", "afrobeat", "soca", "dancehall",
            "reggaeton", "salsa", "cumbia", "sunshine", "bubblegum",
            "happy", "party", "celebration"
        };

        private static readonly HashSet<string> CalmKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "ambient", "chill", "acoustic", "folk", "classical",
            "jazz", "bossa nova", "lo-fi", "lofi", "new age", "meditation",
            "spa", "sleep", "piano", "chamber", "baroque", "choral",
            "easy listening", "smooth jazz", "soft rock"
        };

        private static readonly HashSet<string> SadKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "emo", "gothic", "blues", "singer-songwriter", "sad",
            "melancholy", "dark", "doom", "shoegaze", "post-punk",
            "slowcore", "darkwave", "funeral", "depressive"
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
                Genres = a.Genres ?? new(),
                ImageUrl = a.Images?.FirstOrDefault()?.Url ?? ""
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
                Name = p.Name ?? "",
                Description = p.Description ?? "",
                TrackCount = p.Tracks?.Total ?? 0,
                IsPublic = p.Public,
                ImageUrl = p.Images?.FirstOrDefault()?.Url ?? ""
            }).ToList();

            // Filter/Sort based on query param
            return sortBy?.ToLower() switch
            {
                "size" => summaries.OrderByDescending(p => p.TrackCount).ToList(),
                "name" => summaries.OrderBy(p => p.Name).ToList(),
                _ => summaries
            };
        }

        // ─── Genre-Based Mood Analysis ─────────────────────────────────────────
        // The Spotify Audio Features API was deprecated in November 2024.
        // This approach analyzes mood from your top artists' genres and track
        // popularity instead.

        public async Task<MoodSummary?> GetMoodSummaryAsync(string timeRange = "medium_term")
        {
            var client = await GetAuthenticatedClientAsync();

            // Step 1: Get top tracks for popularity data
            var tracksResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/tracks?time_range={timeRange}&limit=50"
            );
            if (!tracksResponse.IsSuccessStatusCode) return null;

            var tracksJson = await tracksResponse.Content.ReadAsStringAsync();
            var tracksData = JsonSerializer.Deserialize<SpotifyTopTracksResponse>(tracksJson, _jsonOptions);
            if (tracksData == null || tracksData.Items.Count == 0) return null;

            // Step 2: Get top artists for genre data
            var artistsResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/artists?time_range={timeRange}&limit=50"
            );
            if (!artistsResponse.IsSuccessStatusCode) return null;

            var artistsJson = await artistsResponse.Content.ReadAsStringAsync();
            var artistsData = JsonSerializer.Deserialize<SpotifyTopArtistsResponse>(artistsJson, _jsonOptions);
            if (artistsData == null || artistsData.Items.Count == 0) return null;

            var avgPopularity = tracksData.Items.Average(t => t.Popularity);

            // Step 3: Collect all genres from top artists
            var allGenres = artistsData.Items
                .SelectMany(a => a.Genres ?? new List<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.ToLower().Trim())
                .ToList();

            if (allGenres.Count == 0) return null;

            // Step 4: Score each mood category by matching genre keywords
            double energeticScore = CalculateGenreScore(allGenres, EnergeticKeywords);
            double happyScore = CalculateGenreScore(allGenres, HappyKeywords);
            double calmScore = CalculateGenreScore(allGenres, CalmKeywords);
            double sadScore = CalculateGenreScore(allGenres, SadKeywords);

            // Normalize scores to percentages
            double total = energeticScore + happyScore + calmScore + sadScore;
            if (total > 0)
            {
                energeticScore = energeticScore / total * 100;
                happyScore = happyScore / total * 100;
                calmScore = calmScore / total * 100;
                sadScore = sadScore / total * 100;
            }
            else
            {
                // No genre matches — distribute evenly
                energeticScore = happyScore = calmScore = sadScore = 25;
            }

            // Step 5: Get top genres for display
            var topGenres = allGenres
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => new GenreCount { Genre = g.Key, Count = g.Count() })
                .ToList();

            // Step 6: Determine overall mood
            string overallMood = DetermineMood(energeticScore, happyScore, calmScore, sadScore);
            string description = GenerateMoodDescription(energeticScore, happyScore, calmScore, sadScore, avgPopularity);

            return new MoodSummary
            {
                OverallMood = overallMood,
                MoodDescription = description,
                AveragePopularity = Math.Round(avgPopularity, 1),
                TracksAnalyzed = tracksData.Items.Count,
                ArtistsAnalyzed = artistsData.Items.Count,
                MoodScores = new List<MoodScore>
                {
                    new() { Category = "Energetic", Score = Math.Round(energeticScore, 1) },
                    new() { Category = "Happy", Score = Math.Round(happyScore, 1) },
                    new() { Category = "Calm", Score = Math.Round(calmScore, 1) },
                    new() { Category = "Melancholic", Score = Math.Round(sadScore, 1) }
                },
                TopGenres = topGenres
            };
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static string FormatDuration(int ms)
        {
            var ts = TimeSpan.FromMilliseconds(ms);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }

        /// <summary>
        /// Scores how many of a user's genres match a mood keyword set.
        /// Uses partial matching so "indie rock" matches the "rock" keyword.
        /// </summary>
        private static double CalculateGenreScore(List<string> genres, HashSet<string> keywords)
        {
            double score = 0;
            foreach (var genre in genres)
            {
                // Exact match
                if (keywords.Contains(genre))
                {
                    score += 1.0;
                    continue;
                }
                // Partial match: "indie rock" contains "rock"
                foreach (var keyword in keywords)
                {
                    if (genre.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.7;
                        break;
                    }
                }
            }
            return score;
        }

        private static string DetermineMood(double energetic, double happy, double calm, double sad)
        {
            var scores = new (string Label, double Score)[]
            {
                ("Energetic & Intense", energetic),
                ("Happy & Upbeat", happy),
                ("Calm & Relaxed", calm),
                ("Sad & Melancholic", sad)
            };

            var top = scores.OrderByDescending(s => s.Score).First();
            var second = scores.OrderByDescending(s => s.Score).Skip(1).First();

            // If the top two are close, blend them
            if (top.Score - second.Score < 8)
            {
                return $"{top.Label.Split('&')[0].Trim()} & {second.Label.Split('&')[0].Trim()}";
            }

            return top.Label;
        }

        private static string GenerateMoodDescription(double energetic, double happy, double calm, double sad, double popularity)
        {
            var parts = new List<string>();

            var dominant = new[] {
                (Name: "energetic", Score: energetic),
                (Name: "happy and upbeat", Score: happy),
                (Name: "calm and relaxed", Score: calm),
                (Name: "sad or melancholic", Score: sad)
            }.OrderByDescending(s => s.Score).First();

            parts.Add($"Your listening leans most heavily toward {dominant.Name} genres");

            if (energetic > 35) parts.Add("with a strong preference for high-energy sounds");
            else if (calm > 35) parts.Add("with a clear taste for mellow, laid-back vibes");

            if (popularity > 70) parts.Add("and you tend to favor popular, mainstream tracks");
            else if (popularity < 40) parts.Add("and you gravitate toward lesser-known, niche music");
            else parts.Add("with a healthy mix of mainstream and underground picks");

            return string.Join(", ", parts) + ".";
        }
    }
}
