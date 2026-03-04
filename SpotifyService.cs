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

        private string _accessToken = "";
        private string _refreshToken = "";
        private string _userId = "";
        private string _userCountry = "US";
        private string _userDisplayName = "";
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
                "user-read-private user-read-email user-top-read " +
                "playlist-read-private playlist-read-collaborative " +
                "user-read-recently-played"
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

        // ─── User Profile ──────────────────────────────────────────────────────

        public async Task<SpotifyUser?> GetCurrentUserAsync()
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("https://api.spotify.com/v1/me");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<SpotifyUser>(json, _jsonOptions);
            if (user != null)
            {
                if (!string.IsNullOrEmpty(user.Id)) _userId = user.Id;
                if (!string.IsNullOrEmpty(user.Country)) _userCountry = user.Country;
                if (!string.IsNullOrEmpty(user.DisplayName)) _userDisplayName = user.DisplayName;
            }
            return user;
        }

        // ─── Top Tracks ────────────────────────────────────────────────────────

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

            return data.Items.Select(MapTrackSummary).ToList();
        }

        // ─── Top Artists ───────────────────────────────────────────────────────

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
                Id = a.Id,
                Name = a.Name,
                Popularity = a.Popularity,
                Genres = a.Genres ?? new(),
                ImageUrl = a.Images?.FirstOrDefault()?.Url ?? ""
            }).ToList();
        }

        // ─── Recently Played ───────────────────────────────────────────────────

        public async Task<List<RecentlyPlayedSummary>> GetRecentlyPlayedAsync(int limit = 50)
        {
            limit = Math.Clamp(limit, 1, 50);
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync(
                $"https://api.spotify.com/v1/me/player/recently-played?limit={limit}"
            );
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<RecentlyPlayedResponse>(json, _jsonOptions);
            if (data == null) return new();

            return data.Items
                .Where(i => i.Track != null)
                .Select(i =>
                {
                    var t = i.Track!;
                    DateTime.TryParse(i.PlayedAt, out var playedAt);
                    return new RecentlyPlayedSummary
                    {
                        Name = t.Name ?? "",
                        Artist = t.Artists?.FirstOrDefault()?.Name ?? "Unknown",
                        Album = t.Album?.Name ?? "",
                        AlbumArtUrl = t.Album?.Images?.FirstOrDefault()?.Url ?? "",
                        PlayedAt = i.PlayedAt ?? "",
                        TimeAgo = FormatTimeAgo(playedAt),
                        DurationFormatted = FormatDuration(t.DurationMs),
                        SpotifyUrl = t.ExternalUrls?.Spotify ?? ""
                    };
                }).ToList();
        }

        // ─── Artist Detail (Deep Dive) ─────────────────────────────────────────

        public async Task<ArtistDetailResult?> GetArtistDetailAsync(string artistId)
        {
            var client = await GetAuthenticatedClientAsync();

            // Fetch artist profile
            var artistResponse = await client.GetAsync($"https://api.spotify.com/v1/artists/{artistId}");
            if (!artistResponse.IsSuccessStatusCode) return null;

            var artistJson = await artistResponse.Content.ReadAsStringAsync();
            var artist = JsonSerializer.Deserialize<SpotifyArtist>(artistJson, _jsonOptions);
            if (artist == null) return null;

            // Fetch artist's top tracks
            var tracksResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/artists/{artistId}/top-tracks?market={_userCountry}"
            );

            var topTracks = new List<TrackSummary>();
            if (tracksResponse.IsSuccessStatusCode)
            {
                var tracksJson = await tracksResponse.Content.ReadAsStringAsync();
                var tracksData = JsonSerializer.Deserialize<ArtistTopTracksResponse>(tracksJson, _jsonOptions);
                if (tracksData?.Tracks != null)
                    topTracks = tracksData.Tracks.Select(MapTrackSummary).ToList();
            }

            return new ArtistDetailResult
            {
                Id = artist.Id,
                Name = artist.Name,
                Popularity = artist.Popularity,
                Followers = artist.Followers?.Total ?? 0,
                Genres = artist.Genres ?? new(),
                ImageUrl = artist.Images?.FirstOrDefault()?.Url ?? "",
                SpotifyUrl = artist.ExternalUrls?.Spotify ?? "",
                TopTracks = topTracks
            };
        }

        // ─── Playlists ─────────────────────────────────────────────────────────

        public async Task<List<PlaylistSummary>> GetPlaylistsAsync(string? sortBy = null, int offset = 0, int limit = 50)
        {
            if (string.IsNullOrEmpty(_userId))
                await GetCurrentUserAsync();

            limit = Math.Clamp(limit, 1, 50);
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync(
                $"https://api.spotify.com/v1/me/playlists?limit={limit}&offset={offset}"
            );
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SpotifyPlaylistsResponse>(json, _jsonOptions);
            if (data == null) return new();

            var summaries = data.Items
                .Where(p => !string.IsNullOrEmpty(_userId)
                          && p.Owner != null
                          && p.Owner.Id == _userId)
                .Select(p => new PlaylistSummary
                {
                    Id = p.Id,
                    Name = p.Name ?? "",
                    Description = p.Description ?? "",
                    TrackCount = p.Tracks?.Total ?? 0,
                    IsPublic = p.Public ?? false,
                    ImageUrl = p.Images?.FirstOrDefault()?.Url ?? ""
                }).ToList();

            return sortBy?.ToLower() switch
            {
                "size" => summaries.OrderByDescending(p => p.TrackCount).ToList(),
                "name" => summaries.OrderBy(p => p.Name).ToList(),
                _ => summaries
            };
        }

        public async Task<int> GetPlaylistsTotalAsync()
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("https://api.spotify.com/v1/me/playlists?limit=1");
            if (!response.IsSuccessStatusCode) return 0;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SpotifyPlaylistsResponse>(json, _jsonOptions);
            return data?.Total ?? 0;
        }

        // ─── Playlist Detail ───────────────────────────────────────────────────

        public async Task<PlaylistDetailResult?> GetPlaylistDetailAsync(string playlistId)
        {
            var client = await GetAuthenticatedClientAsync();

            // Fetch playlist metadata
            var playlistResponse = await client.GetAsync($"https://api.spotify.com/v1/playlists/{playlistId}?fields=id,name,description,images,tracks.total");
            if (!playlistResponse.IsSuccessStatusCode) return null;

            var playlistJson = await playlistResponse.Content.ReadAsStringAsync();
            var playlist = JsonSerializer.Deserialize<SpotifyPlaylist>(playlistJson, _jsonOptions);
            if (playlist == null) return null;

            // Fetch tracks (up to 100)
            var tracksResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?limit=100&fields=items(track(id,name,popularity,duration_ms,album(id,name,images),artists(id,name),external_urls)),total"
            );
            if (!tracksResponse.IsSuccessStatusCode) return null;

            var tracksJson = await tracksResponse.Content.ReadAsStringAsync();
            var tracksData = JsonSerializer.Deserialize<PlaylistTracksResponse>(tracksJson, _jsonOptions);

            var validTracks = tracksData?.Items?
                .Where(i => i.Track != null && !string.IsNullOrEmpty(i.Track.Name))
                .Select(i => i.Track!)
                .ToList() ?? new();

            var trackSummaries = validTracks.Select(MapTrackSummary).ToList();

            // Calculate stats
            var totalMs = validTracks.Sum(t => (long)t.DurationMs);
            var avgPop = validTracks.Count > 0 ? validTracks.Average(t => t.Popularity) : 0;

            var topArtists = validTracks
                .SelectMany(t => t.Artists ?? new())
                .Where(a => !string.IsNullOrEmpty(a.Name))
                .GroupBy(a => a.Name)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new TopArtistCount { Name = g.Key, Count = g.Count() })
                .ToList();

            return new PlaylistDetailResult
            {
                Id = playlist.Id,
                Name = playlist.Name ?? "",
                Description = playlist.Description ?? "",
                ImageUrl = playlist.Images?.FirstOrDefault()?.Url ?? "",
                TotalTracks = playlist.Tracks?.Total ?? validTracks.Count,
                TotalDuration = FormatDurationLong(totalMs),
                AveragePopularity = Math.Round(avgPop, 1),
                TopArtists = topArtists,
                Tracks = trackSummaries
            };
        }

        // ─── Duplicate Finder ──────────────────────────────────────────────────

        public async Task<DuplicateFinderResult> FindDuplicatesAsync()
        {
            if (string.IsNullOrEmpty(_userId))
                await GetCurrentUserAsync();

            var client = await GetAuthenticatedClientAsync();

            // Get all user-owned playlists
            var playlistsResponse = await client.GetAsync("https://api.spotify.com/v1/me/playlists?limit=50");
            if (!playlistsResponse.IsSuccessStatusCode)
                return new DuplicateFinderResult();

            var playlistsJson = await playlistsResponse.Content.ReadAsStringAsync();
            var playlistsData = JsonSerializer.Deserialize<SpotifyPlaylistsResponse>(playlistsJson, _jsonOptions);
            if (playlistsData == null) return new DuplicateFinderResult();

            var userPlaylists = playlistsData.Items
                .Where(p => p.Owner != null && p.Owner.Id == _userId)
                .Take(25) // Limit to prevent rate limiting
                .ToList();

            // Track -> list of playlist names
            var trackLocations = new Dictionary<string, (string Name, string Artist, List<string> Playlists)>();

            foreach (var playlist in userPlaylists)
            {
                var tracksResponse = await client.GetAsync(
                    $"https://api.spotify.com/v1/playlists/{playlist.Id}/tracks?limit=100&fields=items(track(id,name,artists(name)))"
                );
                if (!tracksResponse.IsSuccessStatusCode) continue;

                var tracksJson = await tracksResponse.Content.ReadAsStringAsync();
                var tracksData = JsonSerializer.Deserialize<PlaylistTracksResponse>(tracksJson, _jsonOptions);
                if (tracksData?.Items == null) continue;

                foreach (var item in tracksData.Items)
                {
                    if (item.Track == null || string.IsNullOrEmpty(item.Track.Id)) continue;

                    if (!trackLocations.ContainsKey(item.Track.Id))
                    {
                        trackLocations[item.Track.Id] = (
                            item.Track.Name ?? "Unknown",
                            item.Track.Artists?.FirstOrDefault()?.Name ?? "Unknown",
                            new List<string>()
                        );
                    }
                    trackLocations[item.Track.Id].Playlists.Add(playlist.Name ?? "Unnamed");
                }

                // Small delay to avoid rate limiting
                await Task.Delay(50);
            }

            var duplicates = trackLocations
                .Where(kv => kv.Value.Playlists.Count > 1)
                .OrderByDescending(kv => kv.Value.Playlists.Count)
                .Select(kv => new DuplicateGroup
                {
                    TrackId = kv.Key,
                    TrackName = kv.Value.Name,
                    Artist = kv.Value.Artist,
                    FoundInPlaylists = kv.Value.Playlists
                })
                .ToList();

            return new DuplicateFinderResult
            {
                PlaylistsScanned = userPlaylists.Count,
                TotalDuplicates = duplicates.Count,
                Duplicates = duplicates
            };
        }

        // ─── Genre Breakdown ───────────────────────────────────────────────────

        public async Task<GenreBreakdownResult?> GetGenreBreakdownAsync(string timeRange = "medium_term")
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/artists?time_range={timeRange}&limit=50"
            );
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SpotifyTopArtistsResponse>(json, _jsonOptions);
            if (data == null) return null;

            var allGenres = data.Items
                .SelectMany(a => a.Genres ?? new())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.ToLower().Trim())
                .ToList();

            var genreCounts = allGenres
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Select(g => new GenreCount { Genre = g.Key, Count = g.Count() })
                .ToList();

            return new GenreBreakdownResult
            {
                ArtistsAnalyzed = data.Items.Count,
                TotalGenreEntries = allGenres.Count,
                UniqueGenres = genreCounts.Count,
                Genres = genreCounts
            };
        }

        // ─── Share Card ────────────────────────────────────────────────────────

        public async Task<ShareCardData?> GetShareCardDataAsync(string timeRange = "medium_term")
        {
            if (string.IsNullOrEmpty(_userId))
                await GetCurrentUserAsync();

            var tracks = await GetTopTracksAsync(timeRange, 5);
            var artists = await GetTopArtistsAsync(timeRange, 5);
            var mood = await GetMoodSummaryAsync(timeRange);

            return new ShareCardData
            {
                DisplayName = _userDisplayName,
                TimeRange = timeRange switch
                {
                    "short_term" => "Last 4 Weeks",
                    "long_term" => "All Time",
                    _ => "Last 6 Months"
                },
                OverallMood = mood?.OverallMood ?? "Unknown",
                AveragePopularity = mood?.AveragePopularity ?? 0,
                TopTrackNames = tracks.Select(t => $"{t.Name} — {t.Artist}").ToList(),
                TopArtistNames = artists.Select(a => a.Name).ToList(),
                TopGenres = mood?.TopGenres?.Take(5).ToList() ?? new(),
                MoodScores = mood?.MoodScores ?? new()
            };
        }

        // ─── Mood Analysis ─────────────────────────────────────────────────────

        public async Task<MoodSummary?> GetMoodSummaryAsync(string timeRange = "medium_term")
        {
            var client = await GetAuthenticatedClientAsync();

            var tracksResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/tracks?time_range={timeRange}&limit=50"
            );
            if (!tracksResponse.IsSuccessStatusCode) return null;

            var tracksJson = await tracksResponse.Content.ReadAsStringAsync();
            var tracksData = JsonSerializer.Deserialize<SpotifyTopTracksResponse>(tracksJson, _jsonOptions);
            if (tracksData == null || tracksData.Items.Count == 0) return null;

            var artistsResponse = await client.GetAsync(
                $"https://api.spotify.com/v1/me/top/artists?time_range={timeRange}&limit=50"
            );
            if (!artistsResponse.IsSuccessStatusCode) return null;

            var artistsJson = await artistsResponse.Content.ReadAsStringAsync();
            var artistsData = JsonSerializer.Deserialize<SpotifyTopArtistsResponse>(artistsJson, _jsonOptions);
            if (artistsData == null || artistsData.Items.Count == 0) return null;

            var avgPopularity = tracksData.Items.Average(t => t.Popularity);

            var allGenres = artistsData.Items
                .SelectMany(a => a.Genres ?? new List<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.ToLower().Trim())
                .ToList();

            if (allGenres.Count == 0) return null;

            double energeticScore = CalculateGenreScore(allGenres, EnergeticKeywords);
            double happyScore = CalculateGenreScore(allGenres, HappyKeywords);
            double calmScore = CalculateGenreScore(allGenres, CalmKeywords);
            double sadScore = CalculateGenreScore(allGenres, SadKeywords);

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
                energeticScore = happyScore = calmScore = sadScore = 25;
            }

            var topGenres = allGenres
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => new GenreCount { Genre = g.Key, Count = g.Count() })
                .ToList();

            string overallMood = DetermineMood(energeticScore, happyScore, calmScore, sadScore);
            string description = GenerateMoodDescription(energeticScore, happyScore, calmScore, sadScore, avgPopularity);

            return new MoodSummary
            {
                OverallMood = overallMood,
                MoodDescription = description,
                AveragePopularity = Math.Round(avgPopularity, 1),
                TracksAnalyzed = tracksData.Items.Count,
                ArtistsAnalyzed = artistsData.Items.Count,
                MoodScores = RoundToHundred(
                    ("Energetic", energeticScore),
                    ("Happy", happyScore),
                    ("Calm", calmScore),
                    ("Melancholic", sadScore)
                ),
                TopGenres = topGenres
            };
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private TrackSummary MapTrackSummary(SpotifyTrack t) => new()
        {
            Name = t.Name ?? "",
            Artist = t.Artists?.FirstOrDefault()?.Name ?? "Unknown",
            Album = t.Album?.Name ?? "",
            Popularity = t.Popularity,
            DurationFormatted = FormatDuration(t.DurationMs),
            AlbumArtUrl = t.Album?.Images?.FirstOrDefault()?.Url ?? "",
            PreviewUrl = t.PreviewUrl ?? "",
            SpotifyUrl = t.ExternalUrls?.Spotify ?? ""
        };

        private static string FormatDuration(int ms)
        {
            var ts = TimeSpan.FromMilliseconds(ms);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }

        private static string FormatDurationLong(long ms)
        {
            var ts = TimeSpan.FromMilliseconds(ms);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }

        private static string FormatTimeAgo(DateTime utcTime)
        {
            if (utcTime == default) return "";
            var diff = DateTime.UtcNow - utcTime;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return utcTime.ToString("MMM d");
        }

        private static double CalculateGenreScore(List<string> genres, HashSet<string> keywords)
        {
            double score = 0;
            foreach (var genre in genres)
            {
                if (keywords.Contains(genre)) { score += 1.0; continue; }
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

        private static List<MoodScore> RoundToHundred(params (string Label, double Value)[] items)
        {
            var floored = items.Select(i => (i.Label, Floor: Math.Floor(i.Value), Remainder: i.Value - Math.Floor(i.Value))).ToList();
            double gap = 100 - floored.Sum(f => f.Floor);
            int unitsToDistribute = (int)Math.Round(gap);

            var sorted = floored
                .Select((f, idx) => (f.Label, f.Floor, f.Remainder, Index: idx))
                .OrderByDescending(f => f.Remainder)
                .ToList();

            var results = new double[items.Length];
            for (int i = 0; i < sorted.Count; i++)
                results[sorted[i].Index] = sorted[i].Floor + (i < unitsToDistribute ? 1 : 0);

            return items.Select((item, i) => new MoodScore
            {
                Category = item.Label,
                Score = results[i]
            }).ToList();
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

            if (top.Score - second.Score < 8)
                return $"{top.Label.Split('&')[0].Trim()} & {second.Label.Split('&')[0].Trim()}";

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