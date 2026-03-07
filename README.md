# Spotify Stats API

A REST API and dashboard built with ASP.NET Core (C#) that connects to the Spotify Web API to return personalized music statistics. Users can log in with their Spotify account and explore their top tracks, top artists, playlists, recently played history, genre breakdowns, and a genre-based mood analysis. The app also includes a duplicate track finder across playlists, a shareable stats card, CSV export, and snapshot comparison to track how your taste changes over time. A full frontend dashboard with dark/light theme support is included.

## Instructions for Build and Use

Steps to build and/or run the software:

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download)
2. Create a Spotify app at https://developer.spotify.com/dashboard and add `http://localhost:5000/api/spotify/callback` as a Redirect URI
3. Set your `ClientId`, `ClientSecret`, and `RedirectUri` in `appsettings.json` under the `"Spotify"` section
4. Run `dotnet build SpotifyAPI.csproj --configuration Release` to build
5. Run `dotnet run` to start the server

Instructions for using the software:

1. Open your browser to `http://localhost:5000` and click "Connect with Spotify" to log in with your Spotify account
2. Use the tabs at the top (Tracks, Artists, Recent, Playlists, Mood, Share) to explore your stats — each section has time range and limit controls
3. Click on any artist card to see a detailed view with their top tracks, or click on any playlist to see its full track listing and stats
4. Use the "Export CSV" buttons to download your data, or visit the Share tab to generate a shareable card of your music taste
5. Swagger API documentation is available at `http://localhost:5000/swagger`

## Development Environment

To recreate the development environment, you need the following software and/or libraries with the specified versions:

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [Swashbuckle.AspNetCore](https://www.nuget.org/packages/Swashbuckle.AspNetCore) v6.5.0 (Swagger/OpenAPI support)
* [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) v8.0.0
* A Spotify Developer account with a registered application (free tier works)
* Azure App Service (optional, for deployment via the included GitHub Actions workflow)

## Useful Websites to Learn More

I found these websites useful in developing this software:

* [Spotify Web API Documentation](https://developer.spotify.com/documentation/web-api)
* [Spotify Authorization Guide](https://developer.spotify.com/documentation/web-api/concepts/authorization)
* [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/?view=aspnetcore-8.0)
* [Swashbuckle (Swagger) Getting Started](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle)

## Future Work

The following items I plan to fix, improve, and/or add to this project in the future:

* [ ] Add per-user session support so multiple users can be authenticated at the same time (currently uses a singleton token)
* [ ] Integrate Spotify's audio features endpoint for more accurate mood analysis based on energy, valence, danceability, and tempo
* [ ] Add listening history trends over time with charts showing how top tracks and artists shift week to week
* [ ] Move client credentials to environment variables or Azure Key Vault instead of appsettings.json for better security