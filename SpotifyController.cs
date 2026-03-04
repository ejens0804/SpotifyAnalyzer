using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Services;

namespace SpotifyAPI.Controllers
{
    [ApiController]
    [Route("api/spotify")]
    public class SpotifyController : ControllerBase
    {
        private readonly SpotifyService _spotify;

        public SpotifyController(SpotifyService spotify)
        {
            _spotify = spotify;
        }

        // ─── Auth ──────────────────────────────────────────────────────────────

        [HttpGet("login")]
        public IActionResult Login()
        {
            var url = _spotify.GetAuthorizationUrl();
            return Redirect(url);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
                return BadRequest(new { message = $"Spotify auth error: {error}" });

            var success = await _spotify.ExchangeCodeForTokenAsync(code);
            if (!success)
                return StatusCode(500, new { message = "Failed to exchange code for token." });

            return Redirect("/?authenticated=true");
        }

        // ─── User ──────────────────────────────────────────────────────────────

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in. Visit /api/spotify/login first." });

            var user = await _spotify.GetCurrentUserAsync();
            return user == null
                ? StatusCode(500, new { message = "Failed to fetch user profile." })
                : Ok(user);
        }

        // ─── Top Tracks ────────────────────────────────────────────────────────

        [HttpGet("top-tracks")]
        public async Task<IActionResult> GetTopTracks(
            [FromQuery] string timeRange = "medium_term",
            [FromQuery] int limit = 20)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var validRanges = new[] { "short_term", "medium_term", "long_term" };
            if (!validRanges.Contains(timeRange))
                return BadRequest(new { message = "Invalid timeRange." });

            var tracks = await _spotify.GetTopTracksAsync(timeRange, limit);
            return Ok(new
            {
                timeRange,
                count = tracks.Count,
                averagePopularity = tracks.Count > 0
                    ? Math.Round(tracks.Average(t => t.Popularity), 1) : 0,
                tracks
            });
        }

        // ─── Top Artists ───────────────────────────────────────────────────────

        [HttpGet("top-artists")]
        public async Task<IActionResult> GetTopArtists(
            [FromQuery] string timeRange = "medium_term",
            [FromQuery] int limit = 20)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var validRanges = new[] { "short_term", "medium_term", "long_term" };
            if (!validRanges.Contains(timeRange))
                return BadRequest(new { message = "Invalid timeRange." });

            var artists = await _spotify.GetTopArtistsAsync(timeRange, limit);

            var topGenres = artists
                .SelectMany(a => a.Genres)
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { genre = g.Key, count = g.Count() })
                .ToList();

            return Ok(new { timeRange, count = artists.Count, topGenres, artists });
        }

        // ─── Artist Detail (Deep Dive) ─────────────────────────────────────────

        [HttpGet("artist/{id}")]
        public async Task<IActionResult> GetArtistDetail(string id)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var detail = await _spotify.GetArtistDetailAsync(id);
            return detail == null
                ? NotFound(new { message = "Artist not found." })
                : Ok(detail);
        }

        // ─── Recently Played ───────────────────────────────────────────────────

        [HttpGet("recently-played")]
        public async Task<IActionResult> GetRecentlyPlayed([FromQuery] int limit = 50)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var items = await _spotify.GetRecentlyPlayedAsync(limit);
            return Ok(new { count = items.Count, items });
        }

        // ─── Playlists ─────────────────────────────────────────────────────────

        [HttpGet("playlists")]
        public async Task<IActionResult> GetPlaylists(
            [FromQuery] string? sortBy = null,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var playlists = await _spotify.GetPlaylistsAsync(sortBy, offset, limit);
            return Ok(new
            {
                count = playlists.Count,
                totalTracks = playlists.Sum(p => p.TrackCount),
                sortedBy = sortBy ?? "default",
                playlists
            });
        }

        // ─── Playlist Detail ───────────────────────────────────────────────────

        [HttpGet("playlist/{id}")]
        public async Task<IActionResult> GetPlaylistDetail(string id)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var detail = await _spotify.GetPlaylistDetailAsync(id);
            return detail == null
                ? NotFound(new { message = "Playlist not found." })
                : Ok(detail);
        }

        // ─── Duplicate Finder ──────────────────────────────────────────────────

        [HttpGet("playlists/duplicates")]
        public async Task<IActionResult> FindDuplicates()
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var result = await _spotify.FindDuplicatesAsync();
            return Ok(result);
        }

        // ─── Genre Breakdown ───────────────────────────────────────────────────

        [HttpGet("genre-breakdown")]
        public async Task<IActionResult> GetGenreBreakdown([FromQuery] string timeRange = "medium_term")
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var validRanges = new[] { "short_term", "medium_term", "long_term" };
            if (!validRanges.Contains(timeRange))
                return BadRequest(new { message = "Invalid timeRange." });

            var result = await _spotify.GetGenreBreakdownAsync(timeRange);
            return result == null
                ? StatusCode(500, new { message = "Failed to analyze genres." })
                : Ok(result);
        }

        // ─── Mood Analysis ─────────────────────────────────────────────────────

        [HttpGet("mood")]
        public async Task<IActionResult> GetMoodSummary([FromQuery] string timeRange = "medium_term")
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var validRanges = new[] { "short_term", "medium_term", "long_term" };
            if (!validRanges.Contains(timeRange))
                return BadRequest(new { message = "Invalid timeRange." });

            var mood = await _spotify.GetMoodSummaryAsync(timeRange);
            return mood == null
                ? StatusCode(500, new { message = "Failed to analyze mood." })
                : Ok(mood);
        }

        // ─── Share Card ────────────────────────────────────────────────────────

        [HttpGet("share-card")]
        public async Task<IActionResult> GetShareCard([FromQuery] string timeRange = "medium_term")
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in." });

            var card = await _spotify.GetShareCardDataAsync(timeRange);
            return card == null
                ? StatusCode(500, new { message = "Failed to generate share card." })
                : Ok(card);
        }
    }
}