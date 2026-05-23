---
antigravity_version: "2.0"
mode: "planning"
system_directives:
  - "Maintain a strict logical separation of concerns between backend and frontend."
  - "Target .NET 10 and Node.js v24 LTS runtime environments strictly."
  - "Execute tasks sequentially as defined in Section 10. Pause and build/verify after each step."
---

# MusicMigrator — Complete Implementation Blueprint
### For Antigravity 2.0 — Full Project from 0 to 100%


You are implementing the MusicMigrator project from scratch.
Work through the implementation sequence in Section 10 in order.
After each step, verify it builds or runs before proceeding.
Do not add anything not specified in this document.
---

## 1. PROJECT OVERVIEW

Build a web application called **MusicMigrator** that lets users migrate playlists and music libraries between three platforms: **Spotify**, **YouTube Music**, and **Anghami**.

The user authenticates with at least two platforms via OAuth, picks a source playlist, picks a destination platform, and the system matches and transfers every track. The UI shows live per-track progress.

---

## 2. TECHNOLOGY STACK

| Layer | Technology | Version |
|---|---|---|
| Backend runtime | .NET | 10 |
| Backend framework | ASP.NET Core Minimal API | 10 |
| Backend language | C# | 13 |
| Frontend framework | React | 19 |
| Frontend build tool | Vite | 6 |
| Frontend routing | react-router-dom | 7 |
| Frontend HTTP client | axios | latest |
| Spotify SDK | SpotifyAPI.Web NuGet | latest |
| YouTube SDK | Google.Apis.YouTube.v3 NuGet | latest |
| Browser automation | Microsoft.Playwright NuGet | latest |

No TypeScript. No test projects in this phase. No Docker.

---

## 3. REPOSITORY STRUCTURE

Single Git repository. Two top-level folders.

```
/ (repository root)
├── .gitignore
├── .git/
├── backend/
│   ├── MusicMigrator.sln
│   ├── MusicMigrator.Core/
│   ├── MusicMigrator.Providers/
│   └── MusicMigrator.API/
└── frontend/
    ├── package.json
    ├── vite.config.js
    ├── index.html
    └── src/
        ├── main.jsx
        ├── App.jsx
        ├── services/
        ├── pages/
        └── components/
```

---

## 4. GIT SETUP

### 4.1 Initialize

Run `git init` in the root. Set default branch to `main`.

### 4.2 .gitignore

The root `.gitignore` must cover both ecosystems:

**Ignore for .NET:**
`bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`, `TestResults/`, `[Pp]ublish/`, `*.nupkg`, `appsettings.*.json` (but NOT `appsettings.json` itself), `**/secrets.json`

**Ignore for Node:**
`node_modules/`, `dist/`, `dist-ssr/`, `.env`, `.env.*` (but NOT `.env.example`), `.cache/`, `*.local`

**Ignore for OS:**
`.DS_Store`, `Thumbs.db`

**Ignore for Playwright:**
`/playwright-report/`, `/test-results/`

### 4.3 Final commit

After everything is implemented, built, and verified: stage all non-ignored files and commit with the message:
`Initial commit: MusicMigrator full implementation`

---

## 5. BACKEND ARCHITECTURE

### 5.1 Solution Structure

Three .NET 10 projects inside `/backend`:

| Project | Type | Purpose |
|---|---|---|
| `MusicMigrator.Core` | Class Library (`net10.0`) | Domain models, interfaces, and orchestration logic. No framework dependencies. |
| `MusicMigrator.Providers` | Class Library (`net10.0`) | Concrete implementations for Spotify, YouTube, and Anghami. |
| `MusicMigrator.API` | Web API (`net10.0`) | Minimal API host: endpoints, DI registration, middleware. |

**Project references:**
- `MusicMigrator.API` → references both `Core` and `Providers`
- `MusicMigrator.Providers` → references `Core` only
- `MusicMigrator.Core` → no project references

All three are added to `MusicMigrator.sln`.

---

### 5.2 NuGet Packages

**MusicMigrator.Core** requires:
- `Microsoft.Extensions.Logging.Abstractions` — for `ILogger<T>` in class libraries

**MusicMigrator.Providers** requires:
- `SpotifyAPI.Web` — official .NET Spotify client
- `Google.Apis.YouTube.v3` — official Google YouTube Data API v3 client
- `Microsoft.Playwright` — headless browser for Anghami write operations
- `Microsoft.Extensions.Configuration.Abstractions` — for `IConfiguration` injection
- `Microsoft.Extensions.Http` — for `HttpClient` typed clients

**MusicMigrator.API** requires no additional NuGet packages beyond what comes with the Web API template.

After `dotnet restore`, Playwright's Chromium browser must be installed before the app can run. See **Section 10 Step 11** for the correct platform-specific install command.

#### API Port Configuration

The API must run on port **5000** — this is what all OAuth redirect URIs and the Vite proxy target. After scaffolding `MusicMigrator.API`, open `MusicMigrator.API/Properties/launchSettings.json` and set the `applicationUrl` for the `http` profile to `http://localhost:5000`. Remove any HTTPS profile entries. The relevant section should look like:

```json
"MusicMigrator.API": {
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": false,
  "applicationUrl": "http://localhost:5000",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

Do not add `UseHttpsRedirection()` to `Program.cs`.

---

### 5.3 Domain Models (in `MusicMigrator.Core/Models/`)

Define all models in a single file or split by type. All models live in the `MusicMigrator.Core.Models` namespace.

**Playlist**
A record with: `Id` (string), `Name` (string), `Description` (string, nullable), `TrackCount` (int), `CoverUrl` (string, nullable).

**Track**
A record with: `Id` (string), `Title` (string), `Artist` (string), `Album` (string, nullable), `DurationMs` (int), `IsrcCode` (string, nullable).

The `IsrcCode` field is the International Standard Recording Code — the most reliable cross-platform track identifier. Spotify provides it. YouTube does not. Anghami does not currently expose it via their API.

**MigrationJob**
A class (not record — it is mutated during the job lifecycle) with:
- `Id` (string) — generated as a new GUID on construction
- `SourceProvider` (string) — e.g. "spotify"
- `DestinationProvider` (string) — e.g. "anghami"
- `SourcePlaylistId` (string)
- `SourcePlaylistName` (string)
- `Status` (`MigrationStatus` enum)
- `Results` (`List<TrackMigrationResult>`) — grows as tracks are processed
- `TotalTracks` (int) — set after fetching source tracks
- `ProcessedTracks` (int, computed) — returns `Results.Count`
- `DestinationPlaylistId` (string, nullable) — set after playlist is created on destination
- `ErrorMessage` (string, nullable)
- `CreatedAt` (DateTime, UTC)
- `CompletedAt` (DateTime, nullable, UTC)

**MigrationStatus** enum: `Pending`, `Running`, `Completed`, `Failed`

**TrackMigrationResult**
A record with: `SourceTrack` (Track), `MatchedTrack` (Track, nullable), `Status` (MatchStatus enum), `ConfidenceScore` (double, 0.0–1.0), `FailReason` (string, nullable).

**MatchStatus** enum: `Matched`, `PartialMatch`, `NotFound`

**ProviderToken**
A record with: `AccessToken` (string), `RefreshToken` (string, nullable), `ExpiresAt` (DateTime, UTC).
Add a computed property `IsExpired` that returns true when the current UTC time is within 2 minutes of `ExpiresAt`.

**OAuthState**
A record with: `Provider` (string), `CodeVerifier` (string), `ReturnUrl` (string).
This is serialized to JSON and stored in session during the OAuth flow to survive the browser redirect.

---

### 5.4 Core Interfaces (in `MusicMigrator.Core/Interfaces/`)

#### IMusicProvider

Every platform (Spotify, YouTube, Anghami) implements this interface. The orchestrator only ever talks to this interface — never to concrete provider types.

Methods:
- `string ProviderName { get; }` — returns lowercase provider name: "spotify", "youtube", or "anghami"
- `Task<IEnumerable<Playlist>> GetPlaylistsAsync(string accessToken, CancellationToken ct)` — returns the authenticated user's playlists (created and saved). Implementations must handle pagination internally.
- `Task<IEnumerable<Track>> GetTracksAsync(string accessToken, string playlistId, CancellationToken ct)` — returns all tracks in the specified playlist. Implementations must handle pagination internally.
- `Task<string> CreatePlaylistAsync(string accessToken, string name, string? description, CancellationToken ct)` — creates an empty playlist on the platform and returns its newly assigned ID.
- `Task AddTracksAsync(string accessToken, string playlistId, IEnumerable<Track> tracks, CancellationToken ct)` — adds tracks to an existing playlist. Implementations must batch as required by the platform's API limits.
- `Task<Track?> SearchTrackAsync(string accessToken, Track sourceTrack, CancellationToken ct)` — searches the platform for the best match for a given track. Returns null if nothing suitable is found. Implementations should attempt ISRC lookup first when available, then fall back to title + artist query.

#### ITrackMatcher

Decoupled from providers so it can be tested independently.

Methods:
- `Task<(Track? Match, double Score, MatchStatus Status)> FindBestMatchAsync(IMusicProvider targetProvider, string accessToken, Track sourceTrack, CancellationToken ct)` — calls `SearchTrackAsync` on the target provider, scores the result, and returns the match with confidence score and status classification.

#### ITokenStore

Stores and retrieves OAuth tokens per session per provider. Keyed by the compound string `"{sessionId}:{providerName}"`.

Methods:
- `void Store(string sessionId, string provider, ProviderToken token)`
- `ProviderToken? Get(string sessionId, string provider)`
- `void Remove(string sessionId, string provider)`
- `bool IsConnected(string sessionId, string provider)`

#### IMigrationJobStore

Tracks all migration jobs. Also maintains a session index so a user can list their own jobs.

Methods:
- `MigrationJob Create(string sessionId, string sourceProvider, string destProvider, string playlistId, string playlistName)` — creates a job, stores it, indexes it under the session, and returns it.
- `MigrationJob? Get(string jobId)` — returns the job or null.
- `void Update(MigrationJob job)` — persists the latest job state.
- `IEnumerable<MigrationJob> GetBySession(string sessionId)` — returns all jobs created by this session.

---

### 5.5 Core Services (in `MusicMigrator.Core/Services/`)

#### InMemoryTokenStore

Implements `ITokenStore` using `ConcurrentDictionary<string, ProviderToken>`.
Register as **Singleton** in DI — tokens must survive across HTTP requests.

#### InMemoryMigrationJobStore

Implements `IMigrationJobStore` using two `ConcurrentDictionary` instances: one keyed by `jobId`, one keyed by `sessionId` mapping to a `List<string>` of job IDs.
Register as **Singleton** in DI.

#### FuzzyTrackMatcher

Implements `ITrackMatcher`. Register as **Scoped**.

Scoring logic (applied after calling `SearchTrackAsync` on the target provider):

| Condition | Confidence Score |
|---|---|
| ISRC codes present and match exactly (case-insensitive) | 1.00 |
| Normalized title + normalized artist match exactly | 0.95 |
| Normalized title matches + one artist name contains the other | 0.85 |
| One title contains the other + one artist contains the other | 0.75 |
| Durations within 3000ms + one title contains the other | 0.65 |
| Title matches exactly, artists differ | 0.50 |
| Everything else | 0.10 |

Normalization rules: lowercase, strip `(feat. ...)` / `(ft. ...)` / `(with ...)` parenthetical patterns, remove all non-word non-space characters, collapse multiple spaces to one, trim.

Status classification from score:
- Score >= 0.80 → `Matched`
- Score >= 0.50 → `PartialMatch`
- Score < 0.50 → `NotFound` (null match returned, track skipped)

#### MigrationOrchestrator

Implements the full migration lifecycle. Register as **Scoped**.

Takes via constructor: `IEnumerable<IMusicProvider>` (injected as all registered providers), `ITrackMatcher`, `IMigrationJobStore`, `ITokenStore`, `ILogger<MigrationOrchestrator>`.

On construction, build a `Dictionary<string, IMusicProvider>` from the injected collection, keyed by `ProviderName`.

`RunAsync(string jobId, string sessionId, CancellationToken ct)` execution steps:
1. Load job from store. Set status to `Running`. Save.
2. Load source access token from `ITokenStore`. Throw if missing.
3. Load destination access token from `ITokenStore`. Throw if missing.
4. Resolve source and destination `IMusicProvider` by name.
5. Fetch all tracks from source using `GetTracksAsync`. Set `job.TotalTracks`. Save.
6. Create destination playlist using `CreatePlaylistAsync`. Store the returned ID in `job.DestinationPlaylistId`. Save.
7. For each source track:
   a. Call `ITrackMatcher.FindBestMatchAsync` against the destination provider.
   b. Append a `TrackMigrationResult` to `job.Results`. Save after each track so the frontend poll reflects real-time progress.
   c. Collect matched tracks into a list.
   d. Apply a 150ms delay between tracks to respect API rate limits.
8. After all tracks processed, call `AddTracksAsync` on destination with all matched tracks.
9. Set `job.Status = Completed`, `job.CompletedAt = DateTime.UtcNow`. Save.
10. On any unhandled exception: set `job.Status = Failed`, `job.ErrorMessage = exception.Message`. Save. Do not rethrow (this runs in a background task).
11. On `OperationCanceledException`: set `job.Status = Failed`, `job.ErrorMessage = "Migration cancelled."`. Save. Rethrow.

---

### 5.6 Providers (in `MusicMigrator.Providers/`)

#### Spotify Provider (`/Spotify/` subfolder)

**SpotifyService** implements `IMusicProvider`. Register as **Scoped**.

Uses `SpotifyAPI.Web.SpotifyClient` constructed from the access token.

- `GetPlaylistsAsync` — call `client.Playlists.CurrentUsers` with limit 50 and use `client.Paginate` to collect all pages. Map each item to a `Playlist` record.
- `GetTracksAsync` — call `client.Playlists.GetItems` with fields narrowed to track fields and use `client.Paginate`. Filter items where the track is a `FullTrack`. Map each to a `Track` record including the ISRC from `ExternalIds["isrc"]`.
- `CreatePlaylistAsync` — call `client.UserProfile.Current()` to get the user ID, then call `client.Playlists.Create` with visibility set to private.
- `AddTracksAsync` — build a list of `spotify:track:{Id}` URIs. Chunk into batches of 100 (Spotify's limit). Call `client.Playlists.AddItems` per batch with a 200ms delay between batches.
- `SearchTrackAsync` — if ISRC is present, call `client.Search.Item` with query `isrc:{IsrcCode}` first. If that returns a result, return it. Otherwise call `client.Search.Item` with query `track:{Title} artist:{Artist}` and return the first result.

**SpotifyAuthHandler**. Register as **Scoped**.

Uses `SpotifyAPI.Web.PKCEUtil.GenerateCodes()` to produce the verifier/challenge pair.
Uses `SpotifyAPI.Web.LoginRequest` to build the authorization URL.
Uses `SpotifyAPI.Web.OAuthClient` to exchange code for tokens and to refresh.

OAuth scopes required:
`playlist-read-private`, `playlist-read-collaborative`, `playlist-modify-public`, `playlist-modify-private`, `user-library-read`, `user-read-email`

**Important naming constraint:** do not name the field that holds these scope strings `Scopes`. That name collides with the `SpotifyAPI.Web.Scopes` static class at compile time and produces an ambiguous reference error. Name the field `RequiredScopes` instead.

Authorization endpoint: standard Spotify PKCE flow (the library handles the URL).
The `BuildAuthorizationUrl(string state)` method returns a tuple of `(string AuthUrl, string CodeVerifier)`.
The `ExchangeCodeAsync(string code, string codeVerifier)` method returns `(string AccessToken, string RefreshToken, DateTime ExpiresAt)`.
The `RefreshAsync(string refreshToken)` method returns the same tuple.

Config keys read from `appsettings.json`:
- `Spotify:ClientId`
- `Spotify:RedirectUri` (value: `http://localhost:5000/auth/spotify/callback`)

No `ClientSecret` — Spotify's PKCE flow for public clients does not require it.

---

#### YouTube Music Provider (`/YouTube/` subfolder)

**YouTubeMusicService** implements `IMusicProvider`. Register as **Scoped**.

Uses `Google.Apis.YouTube.v3.YouTubeService` initialized with `GoogleCredential.FromAccessToken(accessToken)`.

Important limitation: the YouTube Data API v3 does not expose music-specific metadata such as ISRC codes or album names. Tracks are YouTube videos, and the "artist" is the channel name. This is a known platform constraint — do not attempt to work around it.

- `GetPlaylistsAsync` — call `service.Playlists.List("snippet,contentDetails")` with `Mine = true` and `MaxResults = 50`. Handle `NextPageToken` manually in a loop. Map each item to a `Playlist` record.
- `GetTracksAsync` — two-step process:
  1. Call `service.PlaylistItems.List("contentDetails")` with the playlist ID, paging through all results to collect all video IDs.
  2. Batch the video IDs in groups of 50 and call `service.Videos.List("snippet,contentDetails")` per batch. Map each video to a `Track`. Duration comes from ISO 8601 duration string in `ContentDetails.Duration` — convert to milliseconds using `System.Xml.XmlConvert.ToTimeSpan`.
- `CreatePlaylistAsync` — call `service.Playlists.Insert` with `PrivacyStatus = "private"`.
- `AddTracksAsync` — call `service.PlaylistItems.Insert` once per track (YouTube has no batch insert). Apply a 100ms delay between calls — YouTube API quota is strict.
- `SearchTrackAsync` — call `service.Search.List("snippet")` with query `"{Title} {Artist} official audio"`, type `video`, `VideoCategoryId = "10"` (Music), and `MaxResults = 5`. Return the first result mapped to a Track.

**YouTubeAuthHandler**. Register as **Scoped**. Requires a named `HttpClient`.

Implements Authorization Code + PKCE manually (no Google library — keep it consistent with the other providers).

Authorization endpoint: `https://accounts.google.com/o/oauth2/v2/auth`
Token endpoint: `https://oauth2.googleapis.com/token`

Required query parameters for authorization URL: `client_id`, `redirect_uri`, `response_type=code`, `scope`, `state`, `code_challenge`, `code_challenge_method=S256`, `access_type=offline`, `prompt=consent`. The `prompt=consent` ensures Google always returns a refresh token.

Scopes required: `https://www.googleapis.com/auth/youtube`, `https://www.googleapis.com/auth/youtube.force-ssl`

PKCE implementation: generate a 64-byte random verifier using `RandomNumberGenerator.GetBytes(64)`, base64url-encode it (trim `=`, replace `+` with `-`, replace `/` with `_`). Generate challenge by SHA-256 hashing the verifier bytes then base64url-encoding.

Token exchange: POST to token endpoint with form body containing `code`, `client_id`, `client_secret`, `redirect_uri`, `grant_type=authorization_code`, `code_verifier`. Parse `access_token`, `refresh_token`, `expires_in` from JSON response.

Refresh: POST to same token endpoint with `refresh_token`, `client_id`, `client_secret`, `grant_type=refresh_token`.

Config keys: `YouTube:ClientId`, `YouTube:ClientSecret`, `YouTube:RedirectUri` (value: `http://localhost:5000/auth/youtube/callback`).

Note: Google requires a `ClientSecret` even with PKCE for web server applications. This is unlike Spotify.

---

#### Anghami Provider (`/Anghami/` subfolder)

The Anghami provider is split into three classes because the platform has two distinct integration modes: an official OAuth API for reading, and a headless browser for writing.

**Important context:** Anghami launched an official public SDK at `https://sdk.anghami.com/v1`. This SDK supports OAuth 2.0 + PKCE and provides read access to playlists, library, and search. The write API (creating playlists, adding tracks) is reserved for a future release. Until then, `Microsoft.Playwright` handles write operations by automating the Anghami web player at `https://open.anghami.com`.

---

**AnghamiApiClient** — handles all read operations against the official API. Register as **Scoped** with a named `HttpClient` whose base address is `https://sdk.anghami.com` and default Accept header `application/json`.

All requests use `Authorization: Bearer {accessToken}` header.

Methods:
- `GetUserPlaylistsAsync(string accessToken, CancellationToken ct)` — GET `/v1/playlists/user?page_size=50`. Handle cursor-based pagination via `next_page_token` field in response. Each playlist item is a JSON object — extract `id.value`, `title`, `description`, `item_count`, `artwork_url`. Return `List<Playlist>`.
- `GetPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken ct)` — GET `/v1/playlists/{playlistId}?page_size=50`. Handle pagination. Each item in the `items` array is a Content envelope — extract `song_id.value`, `title`, `artist_name`, `album_name`, `duration_ms`. Skip items that do not have a `song_id` (podcasts, non-song content). Return `List<Track>`.
- `SearchTracksAsync(string accessToken, string query, string market, CancellationToken ct)` — GET `/v1/discovery/search?query={encoded}&page_size=5&market={market}`. Default market is `"SA"`. Extract results from the `results` array using the same Content mapping. Return `List<Track>`.

**AnghamiAuthHandler**. Register as **Scoped**. Requires a named `HttpClient`.

Uses the official Anghami SDK OAuth endpoints:
- Authorization: `GET https://sdk.anghami.com/v1/auth/authorize`
- Token exchange: `POST https://sdk.anghami.com/v1/auth/token` (JSON body)
- Token refresh: `POST https://sdk.anghami.com/v1/auth/token/refresh` (JSON body)

PKCE: same implementation as YouTube (64-byte verifier, SHA-256 challenge, base64url encoding).

Authorization URL parameters: `client_id`, `redirect_uri`, `response_type=code`, `scope=read`, `state`, `code_challenge`, `code_challenge_method=S256`.

Token exchange body (JSON): `grant_type=authorization_code`, `code`, `redirect_uri`, `client_id`, `code_verifier`.

Refresh body (JSON): `refresh_token`, `client_id`.

Config keys: `Anghami:ClientId`, `Anghami:RedirectUri` (value: `http://localhost:5000/auth/anghami/callback`).

No `ClientSecret` — Anghami's PKCE flow does not require one.

**AnghamiPlaywrightWriter** — handles write operations via headless browser. Register as **Singleton** because it owns the `IPlaywright` and `IBrowser` process lifecycle. Implements `IAsyncDisposable`.

**Important — no shared browser context:** Do NOT store an `IBrowserContext` as a singleton field. Doing so creates a race condition where concurrent migration jobs overwrite each other's session cookies, potentially writing one user's tracks to another user's Anghami account. Instead, each write operation creates its own isolated `IBrowserContext`, injects the token cookie into it, performs its work, then disposes the context. The browser process itself (`IBrowser`) is shared and long-lived; only contexts are per-invocation.

Holds private fields: `IPlaywright?`, `IBrowser?` only. No `IBrowserContext` field.

`InitializeAsync()` — launches Chromium headless. Call this lazily on first use. Does not create a browser context.

`CreatePlaylistAsync(string accessToken, string name, string? description)` — accepts the access token directly. Returns the new playlist ID as a string.
Behavior:
1. Call `_browser!.NewContextAsync()` to create a fresh isolated context.
2. Add the session cookie to this context: name `anghami_access_token`, value = accessToken, domain `.anghami.com`, path `/`, secure true, SameSite None.
3. Open a new page from this context, navigate to `https://open.anghami.com/library`, wait for `NetworkIdle`.
4. Click the "New Playlist" button (locate by test ID `create-playlist-button` or by text content "New Playlist").
5. Fill the playlist name input.
6. If a description textarea is present, fill it.
7. Click the confirm/create button.
8. Wait for the URL to match `**/playlist/**`.
9. Extract the playlist ID from the URL (last path segment, before any query string).
10. Dispose the context (`await context.DisposeAsync()`) and return the ID.

`AddTrackToPlaylistAsync(string accessToken, string playlistId, string songId)` — accepts the access token directly. Adds a single track.
Behavior:
1. Call `_browser!.NewContextAsync()` to create a fresh isolated context.
2. Inject the session cookie into this context (same fields as above).
3. Open a new page, navigate to `https://open.anghami.com/song/{songId}`, wait for `NetworkIdle`.
4. Click the song options button (locate by test ID or aria-label "more").
5. Click "Add to playlist" option.
6. Find and click the target playlist by its ID in the resulting list.
7. Wait for a success toast notification.
8. Dispose the context.
9. If any step throws, log a warning, dispose the context in a finally block, and do not rethrow — individual track failures should not abort the migration.

`DisposeAsync()` — disposes the browser and playwright instance only (no context to dispose here).

**AnghamiService** implements `IMusicProvider`. Register as **Scoped**.

Takes `AnghamiApiClient` and `AnghamiPlaywrightWriter` via constructor.

Routes:
- `GetPlaylistsAsync` → delegates to `AnghamiApiClient.GetUserPlaylistsAsync`
- `GetTracksAsync` → delegates to `AnghamiApiClient.GetPlaylistTracksAsync`
- `SearchTrackAsync` → builds query `"{Title} {Artist}"`, delegates to `AnghamiApiClient.SearchTracksAsync`, returns first result
- `CreatePlaylistAsync` → passes accessToken directly to `AnghamiPlaywrightWriter.CreatePlaylistAsync(accessToken, name, description)`. No separate `SetSessionAsync` call — the writer handles cookie injection internally per invocation.
- `AddTracksAsync` → for each track, calls `AnghamiPlaywrightWriter.AddTrackToPlaylistAsync(accessToken, playlistId, track.Id)` with a 500ms delay between calls. Catch and log exceptions per track — never abort the whole batch.

---

### 5.7 API Layer (in `MusicMigrator.API/`)

#### Program.cs — DI Registration and Middleware

Register services in this order:

**Session:**
`AddDistributedMemoryCache()` + `AddSession()` with cookie name `.MusicMigrator.Session`, `HttpOnly = true`, `SameSite = Lax`, `IdleTimeout = 2 hours`.

**CORS:**
`AddCors()` allowing origins `http://localhost:5173` and `http://localhost:3000`, with `AllowCredentials()`, `AllowAnyHeader()`, `AllowAnyMethod()`.

**Singletons (one instance per app lifetime):**
- `ITokenStore` → `InMemoryTokenStore`
- `IMigrationJobStore` → `InMemoryMigrationJobStore`
- `AnghamiPlaywrightWriter` (owns the browser process)

**Scoped (one instance per HTTP request):**
- `IMusicProvider` → `SpotifyService` (registered three times — once per provider)
- `IMusicProvider` → `YouTubeMusicService`
- `IMusicProvider` → `AnghamiService`
- `ITrackMatcher` → `FuzzyTrackMatcher`
- `MigrationOrchestrator`
- `SpotifyAuthHandler`
- `YouTubeAuthHandler`
- `AnghamiAuthHandler`

Note: `AnghamiApiClient` is **not** registered here — it is registered exclusively via `AddHttpClient<AnghamiApiClient>()` below. Registering it in both places creates a conflicting DI registration.

**Named HttpClients:**
- `AddHttpClient<YouTubeAuthHandler>()`
- `AddHttpClient<AnghamiAuthHandler>()`
- `AddHttpClient<AnghamiApiClient>()` with base address `https://sdk.anghami.com` and default Accept header `application/json`

**Swagger:** `AddEndpointsApiExplorer()` + `AddSwaggerGen()`

**Middleware pipeline order:**
`UseSwagger()` → `UseSwaggerUI()` → `UseCors()` → `UseSession()` → map endpoints

**Endpoint registration:** call three extension methods: `app.MapAuthEndpoints()`, `app.MapPlaylistEndpoints()`, `app.MapMigrationEndpoints()`

---

#### AuthEndpoints.cs

Group prefix: `/auth`. Tag: "Auth".

**Session helper (private static):**
`GetOrCreateSession(HttpContext ctx)` — reads `"session_id"` from `ctx.Session`. If absent, generates a new GUID, stores it, and returns it. Called at the start of every auth endpoint.

**OAuthState helper (private static):**
`GetOAuthState(HttpContext ctx, string state)` — reads and deserializes the JSON stored in session under key `"oauth_state_{state}"`. Removes the key after reading (one-time use). Returns null if not found.

**Endpoints:**

`GET /auth/status`
Returns JSON: `{ "spotify": bool, "youtube": bool, "anghami": bool }` indicating which providers are connected for this session.

`GET /auth/spotify/start`
Calls `SpotifyAuthHandler.BuildAuthorizationUrl(state)` where `state` is a new GUID. Serializes `OAuthState` (provider="spotify", codeVerifier, returnUrl="/") to JSON and stores in session under key `"oauth_state_{state}"`. Returns a redirect to the Spotify authorization URL.

`GET /auth/spotify/callback?code=&state=`
Validates state via `GetOAuthState`. Calls `SpotifyAuthHandler.ExchangeCodeAsync`. Stores the resulting `ProviderToken` in `ITokenStore`. Returns redirect to `http://localhost:5173/?connected=spotify`.

`GET /auth/youtube/start` — same pattern as Spotify, using `YouTubeAuthHandler`. Redirects to `http://localhost:5173/?connected=youtube` on callback.

`GET /auth/youtube/callback?code=&state=` — same pattern.

`GET /auth/anghami/start` — same pattern using `AnghamiAuthHandler`. Redirects to `http://localhost:5173/?connected=anghami` on callback.

`GET /auth/anghami/callback?code=&state=` — same pattern.

`DELETE /auth/{provider}`
Calls `ITokenStore.Remove(sessionId, provider)`. Returns 200 OK.

---

#### PlaylistEndpoints.cs

Group prefix: `/playlists`. Tag: "Playlists".

`GET /playlists/{provider}`
1. Get session ID — return 401 if missing.
2. Get token from `ITokenStore` — return 401 if missing.
3. Resolve `IMusicProvider` by matching `ProviderName` (case-insensitive) from the injected `IEnumerable<IMusicProvider>` — return 400 if unknown.
4. Call `GetPlaylistsAsync` with the access token.
5. Return 200 with the playlist array.

---

#### MigrationEndpoints.cs

Group prefix: `/migrate`. Tag: "Migration".

**Request model:** `StartMigrationRequest` with properties: `SourceProvider` (string), `DestinationProvider` (string), `PlaylistId` (string), `PlaylistName` (string).

`POST /migrate`
Body: `StartMigrationRequest`.
1. Get session ID — return 401 if missing.
2. Call `IMigrationJobStore.Create(...)` to create the job.
3. Fire a background task using `IServiceScopeFactory` (inject this into the endpoint, not `HttpContext.RequestServices`). Use the `_ = Task.Run(...)` pattern. Inside the lambda, create an async scope with `await using var scope = scopeFactory.CreateAsyncScope()`, then resolve `MigrationOrchestrator` from `scope.ServiceProvider`, and call `RunAsync(job.Id, sessionId)`. **Important:** the scope must be created INSIDE the `Task.Run` lambda, not before it. Creating the scope before `Task.Run` causes it to be disposed when the HTTP request ends, which crashes the background job.
4. Return `Results.Accepted($"/migrate/{job.Id}", new { jobId = job.Id })`.

`GET /migrate/{jobId}`
1. Get session ID — return 401 if missing.
2. Load job from store — return 404 if not found.
3. Return 200 with full job state: Id, Status, SourceProvider, DestinationProvider, SourcePlaylistName, TotalTracks, ProcessedTracks, DestinationPlaylistId, ErrorMessage, CreatedAt, CompletedAt, and a `Results` array. Each result in the array: SourceTrack (Title, Artist, Album), MatchedTrack (Title, Artist, Album — or null), Status, ConfidenceScore, FailReason.

`GET /migrate`
Returns a summary list of all jobs for this session: Id, Status, SourceProvider, DestinationProvider, SourcePlaylistName, TotalTracks, ProcessedTracks, CreatedAt.

---

#### appsettings.json

Must contain these keys with placeholder values (users fill in their real credentials):

```json
{
  "Spotify": {
    "ClientId": "YOUR_SPOTIFY_CLIENT_ID",
    "RedirectUri": "http://localhost:5000/auth/spotify/callback"
  },
  "YouTube": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5000/auth/youtube/callback"
  },
  "Anghami": {
    "ClientId": "YOUR_ANGHAMI_CLIENT_ID",
    "RedirectUri": "http://localhost:5000/auth/anghami/callback"
  }
}
```

Do not commit any real credentials. The `appsettings.Development.json` override file is gitignored.

---

## 6. FRONTEND ARCHITECTURE

### 6.1 Scaffolding

Use `npm create vite@latest frontend -- --template react` from the repository root.

After scaffolding, update `package.json` dependencies to:
- `react`: `^19.0.0`
- `react-dom`: `^19.0.0`
- `react-router-dom`: `^7.0.0`
- `axios`: `^1.7.7`

Dev dependencies:
- `@vitejs/plugin-react`: `^4.3.1`
- `vite`: `^6.0.0`

Run `npm install` after updating `package.json`.

Remove all generated boilerplate: `App.css`, `assets/react.svg`, `public/vite.svg`. The `index.css` can also be removed.

### 6.2 index.html

Minimal HTML shell. Sets `background: #0f0f13` on `body` via a style tag in head. Global reset: `box-sizing: border-box`, `margin: 0`, `padding: 0`. Font: `-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`. Text color: `#f0f0f0`. The root div is `<div id="root"></div>`.

### 6.3 vite.config.js

Configure the dev server proxy so all API calls from the frontend are forwarded to the backend without CORS issues:

Server port: `5173`.

Proxy rules (all with `changeOrigin: true`, targeting `http://localhost:5000`):
- `/auth` → backend
- `/playlists` → backend
- `/migrate` → backend

### 6.4 main.jsx

Renders `<App />` inside `<StrictMode>` using `createRoot`. No global CSS imports needed.

### 6.5 App.jsx

Sets up `BrowserRouter` with three routes:
- `/` → `ConnectAccounts` page
- `/select` → `SelectPlaylists` page
- `/progress/:jobId` → `MigrationProgress` page

### 6.6 services/api.js

Creates an `axios` instance with:
- `baseURL: ''` (empty — Vite proxy handles routing)
- `withCredentials: true` (sends session cookie with every request)

Exports these functions (no default export — named exports only):

- `getAuthStatus()` — GET `/auth/status` → returns `{ spotify, youtube, anghami }` booleans
- `connectProvider(provider)` — sets `window.location.href = /auth/{provider}/start` (triggers browser redirect to OAuth flow, no axios call)
- `disconnectProvider(provider)` — DELETE `/auth/{provider}`
- `getPlaylists(provider)` — GET `/playlists/{provider}` → returns array of playlist objects
- `startMigration({ sourceProvider, destinationProvider, playlistId, playlistName })` — POST `/migrate` with JSON body → returns `{ jobId }`
- `getMigrationStatus(jobId)` — GET `/migrate/{jobId}` → returns full job object
- `getMigrationHistory()` — GET `/migrate` → returns array of job summaries

---

### 6.7 pages/ConnectAccounts.jsx (Step 1)

This is the first page users see. It shows three platform cards and a "Continue" button.

**State:** `status` object with shape `{ spotify: bool, youtube: bool, anghami: bool }` initialized to all false.

**On mount:**
- Call `getAuthStatus()` and set `status`.
- Check if `?connected={provider}` is in the URL (redirected back from OAuth). If present, clean the URL using `window.history.replaceState({}, '', '/')`.

**Layout:** centered column, max-width 520px. Title "🎵 MusicMigrator". Subtitle explaining the need to connect two platforms.

Three `ProviderCard` components rendered in a column, one per provider, each receiving `provider`, `connected` (bool from status), and `onStatusChange` (calls `getAuthStatus` again).

**Continue button:** disabled when fewer than 2 platforms are connected. When disabled, shows "Connect {N} more platform(s) to continue". When enabled, navigates to `/select`. Style: full-width, rounded, purple `#7c3aed` when enabled, dark gray when disabled.

---

### 6.8 pages/SelectPlaylists.jsx (Step 2)

Allows user to pick source platform, destination platform, and which playlist to migrate.

**State:** `connectedProviders` (array), `source` (string), `destination` (string), `playlists` (array), `selectedPlaylist` (object or null), `loading` (bool), `starting` (bool).

**On mount:** call `getAuthStatus()`, extract connected providers. Set `source` to first connected provider. Set `destination` to second.

**When `source` changes:** call `getPlaylists(source)` to load that platform's playlists. Clear `selectedPlaylist`. Show loading state.

**Layout:**
- Back button → navigate to `/`
- Heading "Choose what to migrate"
- Source/destination selector row: two `<select>` elements with an arrow in between. The destination dropdown only shows platforms that are not currently selected as source.
- Playlist list: scrollable area (max-height 360px), one clickable button per playlist showing cover thumbnail (if available), name, and track count. Selected playlist highlighted with purple border.
- Migrate button: full-width, enabled only when a playlist and valid destination are selected. Text: `Migrate "{name}" →`. On click, call `startMigration(...)` and navigate to `/progress/{jobId}`.

---

### 6.9 pages/MigrationProgress.jsx (Step 3)

Live progress view. Polls the backend every 2 seconds while the job is running.

**State:** `job` (the full job object returned by backend, null initially).

**On mount:** start polling with `setInterval(fetchStatus, 2000)`. Also call `fetchStatus` immediately. Clear the interval when the component unmounts or when the job reaches `Completed` or `Failed` status.

`fetchStatus` calls `getMigrationStatus(jobId)` and sets `job`.

**Layout sections (render conditionally based on job state):**

Back button → navigate to `/`.

**Header row:** playlist name + status badge. Status colors: Pending=#888, Running=#7c3aed, Completed=#1DB954, Failed=#ff4d4d. Route info: "spotify → anghami" in muted text.

**Progress bar** (visible only when status is `Running`): shows `{processedTracks} / {totalTracks}` and a percentage. The filled portion transitions width smoothly.

**Summary stat tiles** (visible once results start appearing): three side-by-side tiles showing count of Matched (green), PartialMatch (amber), NotFound (red) tracks.

**Error message box** (visible when `errorMessage` is set): red-tinted background box.

**Destination confirmation** (visible when Completed and `destinationPlaylistId` is set): green-tinted box confirming playlist was created and showing the ID.

**Track results table:** a column-header row ("Source Track", "Matched Track", "Status") followed by one `TrackStatusRow` per result.

---

### 6.10 components/ProviderCard.jsx

Displays a single platform connection card. Props: `provider` (string), `connected` (bool), `onStatusChange` (function).

Internal config per provider:
- spotify: label "Spotify", accent `#1DB954`, background `#191414`, icon 🎵
- youtube: label "YouTube Music", accent `#FF0000`, background `#1a0000`, icon ▶️
- anghami: label "Anghami", accent `#ED1C24`, background `#1a0002`, icon 🎶

Layout: horizontal flex row. Left side: icon + label + connected status text (colored with accent when connected). Right side: "Connect" button (accent fill) when disconnected, "Disconnect" button (subtle outline) when connected.

"Connect" calls `connectProvider(provider)` from api.js (triggers redirect).
"Disconnect" calls `disconnectProvider(provider)` then `onStatusChange()`.

---

### 6.11 components/TrackStatusRow.jsx

Displays a single track's migration result. Props: `result` (TrackMigrationResult object).

Layout: three-column CSS grid (1fr 1fr 100px). Columns: Source track info, Matched track info, Status indicator.

Source column: track title (medium weight) + artist (muted).
Match column: matched title + artist if match exists, or em dash if not.
Status column: icon + label + confidence percentage.

Status display:
- `Matched`: icon ✓, color `#1DB954` (green)
- `PartialMatch`: icon ~, color `#f0a500` (amber)
- `NotFound`: icon ✗, color `#ff4d4d` (red)

Confidence shown as integer percentage (e.g., `87%`).

---

## 7. OAUTH FLOWS (ALL THREE PROVIDERS)

All three providers use Authorization Code + PKCE. The mechanics are identical — only the endpoints and parameters differ.

### 7.1 Flow sequence

```
1. User clicks "Connect" on ProviderCard
2. Frontend: window.location.href = /auth/{provider}/start
3. Backend /auth/{provider}/start:
   a. Generate random state GUID
   b. Generate PKCE verifier + challenge
   c. Store OAuthState in session (keyed by state GUID)
   d. Redirect browser to provider authorization URL
4. User approves on provider's login page
5. Provider redirects browser to /auth/{provider}/callback?code=...&state=...
6. Backend /auth/{provider}/callback:
   a. Read OAuthState from session using state param (and delete it)
   b. Exchange code + verifier for tokens
   c. Store ProviderToken in ITokenStore
   d. Redirect browser to http://localhost:5173/?connected={provider}
7. Frontend ConnectAccounts page detects ?connected= param, refreshes auth status, cleans URL
```

### 7.2 PKCE Implementation (same logic for all three)

Code verifier: 64 cryptographically random bytes, base64url-encoded (trim trailing `=`, replace `+` with `-`, replace `/` with `_`).

Code challenge: SHA-256 of the UTF-8 bytes of the verifier, then base64url-encoded the same way.

Method: `S256`.

### 7.3 Provider-specific endpoints

| | Spotify | YouTube/Google | Anghami |
|---|---|---|---|
| Auth URL | via SpotifyAPI.Web library | `https://accounts.google.com/o/oauth2/v2/auth` | `https://sdk.anghami.com/v1/auth/authorize` |
| Token URL | via SpotifyAPI.Web library | `https://oauth2.googleapis.com/token` | `https://sdk.anghami.com/v1/auth/token` |
| Refresh URL | via SpotifyAPI.Web library | `https://oauth2.googleapis.com/token` | `https://sdk.anghami.com/v1/auth/token/refresh` |
| Client secret required | No | Yes | No |
| Token body format | handled by library | form-encoded | JSON |

---

## 8. DATA FLOW: END-TO-END MIGRATION

```
User selects playlist + destination
    ↓
POST /migrate → creates MigrationJob (status: Pending)
    ↓
Background Task.Run (IServiceScopeFactory scope)
    ↓
MigrationOrchestrator.RunAsync
    ├── Fetch source tracks (GetTracksAsync)
    ├── Create destination playlist (CreatePlaylistAsync)
    └── For each source track:
            ├── FuzzyTrackMatcher.FindBestMatchAsync
            │       └── targetProvider.SearchTrackAsync
            │               ├── ISRC search (if available)
            │               └── title + artist search (fallback)
            ├── Score result (confidence algorithm)
            ├── Append TrackMigrationResult to job
            └── Save job (frontend poll sees update)
    ↓
AddTracksAsync (all matched tracks in one batch)
    ↓
Job status → Completed
```

Frontend polls `GET /migrate/{jobId}` every 2 seconds.
Each poll returns the current results count — the frontend re-renders the table as new rows appear.
Polling stops when status is `Completed` or `Failed`.

---

## 9. CONFIGURATION REQUIREMENTS

Before the application can run, the developer must register apps on each platform's developer console and fill in `appsettings.json`.

**Spotify:**
Register at `https://developer.spotify.com/dashboard`. Create an app. Add `http://localhost:5000/auth/spotify/callback` to the allowed redirect URIs. Copy the Client ID.

**YouTube/Google:**
Register at `https://console.cloud.google.com`. Enable the YouTube Data API v3. Create OAuth 2.0 credentials (Web application type). Add `http://localhost:5000/auth/youtube/callback` as an authorized redirect URI. Copy Client ID and Client Secret.

**Anghami:**
The Anghami Developer Portal is not yet publicly open (as of the time of this planning document). Access requires contacting the Anghami partnership team via the SDK GitHub repository at `https://github.com/anghami/sdk`. Once access is granted, register the app and add `http://localhost:5000/auth/anghami/callback` as the redirect URI. Copy the Client ID.

---

## 10. IMPLEMENTATION SEQUENCE

The agent must implement in this order to avoid dependency issues at build time:

1. Git init + `.gitignore`
2. Backend solution and project scaffold — run these exact commands from inside `/backend`:
   ```
   dotnet new sln -n MusicMigrator
   dotnet new classlib -n MusicMigrator.Core -f net10.0
   dotnet new classlib -n MusicMigrator.Providers -f net10.0
   dotnet new webapi -n MusicMigrator.API -f net10.0 --no-https
   dotnet sln MusicMigrator.sln add MusicMigrator.Core/MusicMigrator.Core.csproj
   dotnet sln MusicMigrator.sln add MusicMigrator.Providers/MusicMigrator.Providers.csproj
   dotnet sln MusicMigrator.sln add MusicMigrator.API/MusicMigrator.API.csproj
   dotnet add MusicMigrator.API/MusicMigrator.API.csproj reference MusicMigrator.Core/MusicMigrator.Core.csproj
   dotnet add MusicMigrator.API/MusicMigrator.API.csproj reference MusicMigrator.Providers/MusicMigrator.Providers.csproj
   dotnet add MusicMigrator.Providers/MusicMigrator.Providers.csproj reference MusicMigrator.Core/MusicMigrator.Core.csproj
   ```
   Then add NuGet packages:
   ```
   dotnet add MusicMigrator.Core/MusicMigrator.Core.csproj package Microsoft.Extensions.Logging.Abstractions
   dotnet add MusicMigrator.Providers/MusicMigrator.Providers.csproj package SpotifyAPI.Web
   dotnet add MusicMigrator.Providers/MusicMigrator.Providers.csproj package Google.Apis.YouTube.v3
   dotnet add MusicMigrator.Providers/MusicMigrator.Providers.csproj package Microsoft.Playwright
   dotnet add MusicMigrator.Providers/MusicMigrator.Providers.csproj package Microsoft.Extensions.Configuration.Abstractions
   dotnet add MusicMigrator.Providers/MusicMigrator.Providers.csproj package Microsoft.Extensions.Http
   ```
   Delete the generated boilerplate from the API project: remove `WeatherForecast.cs` if it exists, and clear the weather forecast route from `Program.cs` — the entire `Program.cs` will be replaced with the content specified in Section 5.7.
3. `MusicMigrator.Core` — models, interfaces, services (Stores, Matcher, Orchestrator)
4. `MusicMigrator.Providers` — Spotify, YouTube, Anghami (all three sub-folders)
5. `MusicMigrator.API` — `Program.cs`, `appsettings.json`, all three endpoint files
6. Backend build verification: `dotnet build MusicMigrator.sln` → 0 errors
7. Frontend scaffold + package.json + vite.config.js + index.html
8. Frontend source files: `main.jsx`, `App.jsx`, `services/api.js`, all components, all pages
9. Frontend install: `npm install`
10. Frontend build verification: `npm run build` → 0 errors
11. Playwright browser install — run from inside the `backend/` folder 
    after a successful build:
    `dotnet run --project MusicMigrator.API -- playwright install chromium`
    This works on all platforms and is auto-approved by the agent config.
12. Final git commit

---

## 11. SUCCESS CRITERIA

The implementation is complete when ALL of the following are true:

| # | Criterion |
|---|---|
| 1 | `git status` shows a clean working tree — no untracked files, no unstaged changes, and all source files committed to `main` |
| 2 | `dotnet build backend/MusicMigrator.sln` exits with `Build succeeded. 0 Error(s)` |
| 3 | `npm run build` inside `/frontend` exits with no errors |
| 4 | `frontend/package.json` shows `"react": "^19.0.0"` |
| 5 | `backend/MusicMigrator.Core` contains: Models, Interfaces (IMusicProvider, ITrackMatcher, ITokenStore, IMigrationJobStore), Services (Stores, Matcher, Orchestrator) |
| 6 | `backend/MusicMigrator.Providers` contains all three provider folders, each with a Service, AuthHandler, and any supporting classes |
| 7 | `backend/MusicMigrator.API` contains `Program.cs` with all DI registrations, and three endpoint files covering auth, playlists, and migration |
| 8 | `frontend/src` contains `App.jsx`, `main.jsx`, `services/api.js`, three pages, and two components |
| 9 | `node_modules/`, `bin/`, `obj/`, `dist/` are absent from `git status` |
| 10 | `appsettings.json` contains all required config keys with placeholder values |

---

## 12. KNOWN CONSTRAINTS AND DECISIONS

- **No TypeScript.** The entire frontend is plain JavaScript with JSX. Do not add TypeScript, ESLint, or Prettier in this phase.
- **No database.** All state (tokens, jobs) is in-memory. A server restart clears everything. This is intentional for Phase 1.
- **No authentication layer.** There is no login system — users are identified by their session cookie only.
- **No token refresh automation.** Token refresh logic exists in the Auth handlers but is not wired to auto-refresh on 401 responses. That is Phase 2 work.
- **Anghami write uses Playwright by design.** This is a deliberate architectural choice, not a gap. The AnghamiPlaywrightWriter selector strings (CSS/text) may need adjustment once tested against the live Anghami web player, as the selectors were written against the current DOM structure of `open.anghami.com`.
- **YouTube tracks have no ISRC.** The matcher falls through to title+artist for all YouTube-sourced tracks. This is expected and acceptable.
- **Anghami requires a partnership contact for API access.** The application code is complete but cannot be tested end-to-end until credentials are obtained from Anghami.
