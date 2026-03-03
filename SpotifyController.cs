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

        // ─── Auth Endpoints ────────────────────────────────────────────────────

        /// <summary>
        /// Step 1: Visit this URL in your browser to log in with Spotify.
        /// </summary>
        [HttpGet("login")]
        public IActionResult Login()
        {
            var url = _spotify.GetAuthorizationUrl();
            return Redirect(url);
        }

        /// <summary>
        /// Step 2: Spotify redirects here after login. Exchanges the code for an access token.
        /// </summary>
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

        // ─── User Endpoints ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the authenticated user's Spotify profile.
        /// </summary>
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

        // ─── Top Tracks Endpoint ───────────────────────────────────────────────

        /// <summary>
        /// Returns the user's top tracks.
        /// </summary>
        /// <param name="timeRange">short_term (4 weeks), medium_term (6 months), long_term (all time)</param>
        /// <param name="limit">Number of tracks to return (1–50)</param>
        [HttpGet("top-tracks")]
        public async Task<IActionResult> GetTopTracks(
            [FromQuery] string timeRange = "medium_term",
            [FromQuery] int limit = 20)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in. Visit /api/spotify/login first." });

            var validRanges = new[] { "short_term", "medium_term", "long_term" };
            if (!validRanges.Contains(timeRange))
                return BadRequest(new { message = "Invalid timeRange. Use: short_term, medium_term, or long_term." });

            var tracks = await _spotify.GetTopTracksAsync(timeRange, limit);
            return Ok(new
            {
                timeRange,
                count = tracks.Count,
                averagePopularity = tracks.Count > 0
                    ? Math.Round(tracks.Average(t => t.Popularity), 1)
                    : 0,
                tracks
            });
        }

        // ─── Top Artists Endpoint ──────────────────────────────────────────────

        /// <summary>
        /// Returns the user's top artists.
        /// </summary>
        /// <param name="timeRange">short_term, medium_term, or long_term</param>
        /// <param name="limit">Number of artists to return (1–50)</param>
        [HttpGet("top-artists")]
        public async Task<IActionResult> GetTopArtists(
            [FromQuery] string timeRange = "medium_term",
            [FromQuery] int limit = 20)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in. Visit /api/spotify/login first." });

            var validRanges = new[] { "short_term", "medium_term", "long_term" };
            if (!validRanges.Contains(timeRange))
                return BadRequest(new { message = "Invalid timeRange. Use: short_term, medium_term, or long_term." });

            var artists = await _spotify.GetTopArtistsAsync(timeRange, limit);

            // Aggregate all genres across top artists
            var topGenres = artists
                .SelectMany(a => a.Genres)
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { genre = g.Key, count = g.Count() })
                .ToList();

            return Ok(new
            {
                timeRange,
                count = artists.Count,
                topGenres,
                artists
            });
        }

        // ─── Playlists Endpoint ────────────────────────────────────────────────

        /// <summary>
        /// Returns the user's playlists.
        /// </summary>
        /// <param name="sortBy">Sort by "name" or "size" (track count)</param>
        [HttpGet("playlists")]
        public async Task<IActionResult> GetPlaylists([FromQuery] string? sortBy = null)
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in. Visit /api/spotify/login first." });

            var playlists = await _spotify.GetPlaylistsAsync(sortBy);
            return Ok(new
            {
                count = playlists.Count,
                totalTracks = playlists.Sum(p => p.TrackCount),
                sortedBy = sortBy ?? "default",
                playlists
            });
        }

        // ─── Mood Analysis Endpoint ────────────────────────────────────────────

        /// <summary>
        /// Analyzes the mood of a user's top tracks using Spotify's audio features.
        /// Returns energy, valence, danceability, tempo, and an overall mood label.
        /// </summary>
        /// <param name="timeRange">short_term, medium_term, or long_term</param>
        [HttpGet("mood")]
        public async Task<IActionResult> GetMoodSummary([FromQuery] string timeRange = "medium_term")
        {
            if (!_spotify.IsAuthenticated())
                return Unauthorized(new { message = "Not logged in. Visit /api/spotify/login first." });

            var validRanges = new[] { "short_term", "medium_term", "long_term" };
            if (!validRanges.Contains(timeRange))
                return BadRequest(new { message = "Invalid timeRange. Use: short_term, medium_term, or long_term." });

            var mood = await _spotify.GetMoodSummaryAsync(timeRange);
            return mood == null
                ? StatusCode(500, new { message = "Failed to analyze mood. Make sure you have listening history." })
                : Ok(mood);
        }
    }
}