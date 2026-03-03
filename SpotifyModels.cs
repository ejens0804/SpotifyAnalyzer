namespace SpotifyAPI.Models
{
    // --- Auth ---
    public class SpotifyTokenResponse
    {
        public string AccessToken { get; set; } = "";
        public string TokenType { get; set; } = "";
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = "";
        public string Scope { get; set; } = "";
    }

    // --- User Profile ---
    public class SpotifyUser
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public List<SpotifyImage> Images { get; set; } = new();
    }

    public class SpotifyImage
    {
        public string Url { get; set; } = "";
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    // --- Top Tracks ---
    public class SpotifyTopTracksResponse
    {
        public List<SpotifyTrack> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class SpotifyTrack
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Popularity { get; set; }
        public int DurationMs { get; set; }
        public SpotifyAlbum Album { get; set; } = new();
        public List<SpotifyArtist> Artists { get; set; } = new();
        public string PreviewUrl { get; set; } = "";
    }

    public class SpotifyAlbum
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public List<SpotifyImage> Images { get; set; } = new();
    }

    // --- Top Artists ---
    public class SpotifyTopArtistsResponse
    {
        public List<SpotifyArtist> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class SpotifyArtist
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Popularity { get; set; }
        public List<string> Genres { get; set; } = new();
        public List<SpotifyImage> Images { get; set; } = new();
    }

    // --- Playlists ---
    public class SpotifyPlaylistsResponse
    {
        public List<SpotifyPlaylist> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class SpotifyPlaylist
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public SpotifyPlaylistOwner? Owner { get; set; }
        public SpotifyPlaylistTracks? Tracks { get; set; }
        public List<SpotifyImage>? Images { get; set; }
        public bool? Public { get; set; }
    }

    public class SpotifyPlaylistOwner
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    public class SpotifyPlaylistTracks
    {
        public int Total { get; set; }
    }

    // --- Processed/Summary Models ---
    public class TrackSummary
    {
        public string Name { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public int Popularity { get; set; }
        public string DurationFormatted { get; set; } = "";
        public string AlbumArtUrl { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
    }

    public class ArtistSummary
    {
        public string Name { get; set; } = "";
        public int Popularity { get; set; }
        public List<string> Genres { get; set; } = new();
        public string ImageUrl { get; set; } = "";
    }

    public class PlaylistSummary
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int TrackCount { get; set; }
        public bool IsPublic { get; set; }
        public string ImageUrl { get; set; } = "";
    }

    // --- Genre-Based Mood Analysis (replaces deprecated Audio Features) ---
    public class MoodSummary
    {
        public string OverallMood { get; set; } = "";
        public string MoodDescription { get; set; } = "";
        public double AveragePopularity { get; set; }
        public int TracksAnalyzed { get; set; }
        public int ArtistsAnalyzed { get; set; }
        public List<MoodScore> MoodScores { get; set; } = new();
        public List<GenreCount> TopGenres { get; set; } = new();
    }

    public class MoodScore
    {
        public string Category { get; set; } = "";
        public double Score { get; set; }
    }

    public class GenreCount
    {
        public string Genre { get; set; } = "";
        public int Count { get; set; }
    }
}