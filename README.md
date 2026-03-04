# 🎵 Spotify Stats API

A REST API built with **ASP.NET Core (C#)** that connects to the Spotify Web API to return personalized music statistics including top tracks, top artists, playlists, and mood analysis.

---

## 📋 Requirements Met

1. ✅ **OAuth 2.0 Authentication** — Log in with your real Spotify account
2. ✅ **Top Tracks endpoint** — Returns your most listened to songs with average popularity
3. ✅ **Top Artists endpoint** — Returns your top artists with genre aggregation
4. ✅ **Playlists endpoint** — Returns all playlists with track counts
5. ✅ **Filter & Sort** — Query params for time range and sort order
6. ✅ **Mood Analysis (Stretch)** — Uses Spotify's audio features to analyze energy, valence, and danceability

---

## 🚀 Setup & Running

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- A Spotify account (free works fine)

### Steps

1. **Clone or download this project**

2. **Set your Spotify credentials** in `appsettings.json`:
   ```json
   "Spotify": {
     "ClientId": "YOUR_CLIENT_ID",
     "ClientSecret": "YOUR_CLIENT_SECRET",
     "RedirectUri": "http://localhost:5000/api/spotify/callback"
   }
   ```

3. **Add the redirect URI in your Spotify Developer Dashboard:**
   - Go to https://developer.spotify.com/dashboard
   - Click your app → Edit Settings
   - Add `http://localhost:5000/api/spotify/callback` to Redirect URIs
   - Save

4. **Run the project:**
   ```bash
   dotnet run
   ```

5. **Open your browser to:** `http://localhost:5000`
   - Swagger UI will load automatically

---

## 🔗 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/spotify/login` | Start Spotify OAuth login |
| GET | `/api/spotify/callback` | OAuth callback (handled automatically) |
| GET | `/api/spotify/me` | Your Spotify profile |
| GET | `/api/spotify/top-tracks` | Your top tracks |
| GET | `/api/spotify/top-artists` | Your top artists + genre breakdown |
| GET | `/api/spotify/playlists` | Your playlists |
| GET | `/api/spotify/mood` | Mood analysis of your listening |

### Query Parameters

**`/api/spotify/top-tracks`** and **`/api/spotify/top-artists`**:
- `timeRange` — `short_term` (4 weeks), `medium_term` (6 months), `long_term` (all time). Default: `medium_term`
- `limit` — Number of results (1–50). Default: `20`

**`/api/spotify/playlists`**:
- `sortBy` — `name` or `size`. Default: Spotify default order

---

## ⚠️ Security Note

Never commit your `ClientSecret` to GitHub. Before pushing:
- Move credentials to environment variables, or
- Use `dotnet user-secrets` for local development

---

## 🧠 How the Mood Analysis Works

Spotify provides "audio features" for every track — numerical scores for:
- **Energy** (0–1): How intense/active the track feels
- **Valence** (0–1): How positive/happy the track sounds
- **Danceability** (0–1): How suitable it is for dancing
- **Tempo**: BPM of the track

This API fetches your top 50 tracks, pulls their audio features, averages them, and assigns a mood label: 

| Energy | Valence | Mood |
|--------|---------|------|
| High | High | Happy & Energetic |
| High | Low | Intense & Angry |
| Low | High | Calm & Content |
| Low | Low | Sad & Melancholic |
