namespace SpotifyAPI.Models
{
    // ─── Auth ──────────────────────────────────────────────────────────────
    public class SpotifyTokenResponse
    {
        public string AccessToken { get; set; } = "";
        public string TokenType { get; set; } = "";
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = "";
        public string Scope { get; set; } = "";
    }

    // ─── User Profile ──────────────────────────────────────────────────────
    public class SpotifyUser
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Country { get; set; }
        public List<SpotifyImage> Images { get; set; } = new();
    }

    public class SpotifyImage
    {
        public string Url { get; set; } = "";
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    // ─── Tracks ────────────────────────────────────────────────────────────
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
        public SpotifyAlbum? Album { get; set; }
        public List<SpotifyArtist>? Artists { get; set; }
        public string? PreviewUrl { get; set; }
        public SpotifyExternalUrls? ExternalUrls { get; set; }
    }

    public class SpotifyAlbum
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? ReleaseDate { get; set; }
        public List<SpotifyImage>? Images { get; set; }
    }

    public class SpotifyExternalUrls
    {
        public string? Spotify { get; set; }
    }

    // ─── Artists ───────────────────────────────────────────────────────────
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
        public List<string>? Genres { get; set; }
        public List<SpotifyImage>? Images { get; set; }
        public SpotifyFollowers? Followers { get; set; }
        public SpotifyExternalUrls? ExternalUrls { get; set; }
    }

    public class SpotifyFollowers
    {
        public int Total { get; set; }
    }

    // ─── Artist Top Tracks ─────────────────────────────────────────────────
    public class ArtistTopTracksResponse
    {
        public List<SpotifyTrack> Tracks { get; set; } = new();
    }

    // ─── Recently Played ───────────────────────────────────────────────────
    public class RecentlyPlayedResponse
    {
        public List<PlayHistoryItem> Items { get; set; } = new();
    }

    public class PlayHistoryItem
    {
        public SpotifyTrack? Track { get; set; }
        public string? PlayedAt { get; set; }
    }

    // ─── Playlists ─────────────────────────────────────────────────────────
    public class SpotifyPlaylistsResponse
    {
        public List<SpotifyPlaylist> Items { get; set; } = new();
        public int Total { get; set; }
        public string? Next { get; set; }
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
        public string? DisplayName { get; set; }
    }

    public class SpotifyPlaylistTracks
    {
        public int Total { get; set; }
    }

    // ─── Playlist Tracks (detail fetch) ────────────────────────────────────
    public class PlaylistTracksResponse
    {
        public List<PlaylistTrackItem>? Items { get; set; }
        public int Total { get; set; }
        public string? Next { get; set; }
    }

    public class PlaylistTrackItem
    {
        public SpotifyTrack? Track { get; set; }
        public string? AddedAt { get; set; }
        public SpotifyPlaylistOwner? AddedBy { get; set; }
    }

    // ─── Output / Summary Models ───────────────────────────────────────────

    public class TrackSummary
    {
        public string Name { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public int Popularity { get; set; }
        public string DurationFormatted { get; set; } = "";
        public string AlbumArtUrl { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
        public string SpotifyUrl { get; set; } = "";
    }

    public class ArtistSummary
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Popularity { get; set; }
        public List<string> Genres { get; set; } = new();
        public string ImageUrl { get; set; } = "";
    }

    public class PlaylistSummary
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int TrackCount { get; set; }
        public bool IsPublic { get; set; }
        public string ImageUrl { get; set; } = "";
    }

    // ─── Recently Played Summary ───────────────────────────────────────────
    public class RecentlyPlayedSummary
    {
        public string Name { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string AlbumArtUrl { get; set; } = "";
        public string PlayedAt { get; set; } = "";
        public string TimeAgo { get; set; } = "";
        public string DurationFormatted { get; set; } = "";
        public string SpotifyUrl { get; set; } = "";
    }

    // ─── Artist Detail (Deep Dive) ─────────────────────────────────────────
    public class ArtistDetailResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Popularity { get; set; }
        public int Followers { get; set; }
        public List<string> Genres { get; set; } = new();
        public string ImageUrl { get; set; } = "";
        public string SpotifyUrl { get; set; } = "";
        public List<TrackSummary> TopTracks { get; set; } = new();
    }

    // ─── Playlist Detail ───────────────────────────────────────────────────
    public class PlaylistDetailResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public int TotalTracks { get; set; }
        public string TotalDuration { get; set; } = "";
        public double AveragePopularity { get; set; }
        public List<TopArtistCount> TopArtists { get; set; } = new();
        public List<TrackSummary> Tracks { get; set; } = new();
    }

    public class TopArtistCount
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    // ─── Duplicate Finder ──────────────────────────────────────────────────
    public class DuplicateGroup
    {
        public string TrackId { get; set; } = "";
        public string TrackName { get; set; } = "";
        public string Artist { get; set; } = "";
        public List<string> FoundInPlaylists { get; set; } = new();
    }

    public class DuplicateFinderResult
    {
        public int PlaylistsScanned { get; set; }
        public int TotalDuplicates { get; set; }
        public List<DuplicateGroup> Duplicates { get; set; } = new();
    }

    // ─── Genre Breakdown ───────────────────────────────────────────────────
    public class GenreBreakdownResult
    {
        public int ArtistsAnalyzed { get; set; }
        public int TotalGenreEntries { get; set; }
        public int UniqueGenres { get; set; }
        public List<GenreCount> Genres { get; set; } = new();
    }

    // ─── Genre-Based Mood Analysis ─────────────────────────────────────────
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

    // ─── Share Card Data ───────────────────────────────────────────────────
    public class ShareCardData
    {
        public string DisplayName { get; set; } = "";
        public string TimeRange { get; set; } = "";
        public string OverallMood { get; set; } = "";
        public double AveragePopularity { get; set; }
        public List<string> TopTrackNames { get; set; } = new();
        public List<string> TopArtistNames { get; set; } = new();
        public List<GenreCount> TopGenres { get; set; } = new();
        public List<MoodScore> MoodScores { get; set; } = new();
    }
}