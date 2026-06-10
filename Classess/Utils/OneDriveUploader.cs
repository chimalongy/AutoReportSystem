using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;


namespace ARS.Classess.Utils
{
    /// <summary>
    /// Uploads a file to OneDrive via Microsoft Graph API (app-only / client credentials).
    /// 
    /// Required appsettings.json section:
    /// "OneDrive": {
    ///   "TenantId":     "your-tenant-id",
    ///   "ClientId":     "your-app-client-id",
    ///   "ClientSecret": "your-app-client-secret",
    ///   "UserId":       "upn-or-object-id of the OneDrive owner, e.g. reports@company.com",
    ///   "UploadFolder": "ARS_Reports"   // folder name inside the drive root (created if absent)
    /// }
    /// </summary>
    public class OneDriveUploader
    {
        // ── config keys ────────────────────────────────────────────────────
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _userId;        // OneDrive owner UPN / object-id
        private readonly string _uploadFolder;  // target folder in drive root

        private static readonly HttpClient _http = new();

        public OneDriveUploader(IConfiguration configuration)
        {
            var cfg = configuration.GetSection("OneDrive");
            _tenantId = cfg["TenantId"] ?? throw new InvalidOperationException("OneDrive:TenantId is missing from appsettings.json");
            _clientId = cfg["ClientId"] ?? throw new InvalidOperationException("OneDrive:ClientId is missing from appsettings.json");
            _clientSecret = cfg["ClientSecret"] ?? throw new InvalidOperationException("OneDrive:ClientSecret is missing from appsettings.json");
            _userId = cfg["UserId"] ?? throw new InvalidOperationException("OneDrive:UserId is missing from appsettings.json");
            _uploadFolder = cfg["UploadFolder"] ?? "ARS_Reports";
        }

        // ──────────────────────────────────────────────────────────────────
        // PUBLIC ENTRY POINT
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Uploads <paramref name="localFilePath"/> to OneDrive and returns a shareable link.
        /// </summary>
        public async Task<OneDriveUploadResult> UploadAndShareAsync(string localFilePath)
        {
            try
            {
                // 1. Acquire token
                string token = await GetAccessTokenAsync();

                // 2. Upload file (uses large-file upload session so any size is fine)
                string fileName = Path.GetFileName(localFilePath);
                string driveItemId = await UploadFileAsync(token, localFilePath, fileName);

                // 3. Create an anonymous view link
                string shareUrl = await CreateShareLinkAsync(token, driveItemId);

                return new OneDriveUploadResult
                {
                    Success = true,
                    Url = shareUrl,
                    Message = $"File '{fileName}' uploaded successfully."
                };
            }
            catch (Exception ex)
            {
                return new OneDriveUploadResult
                {
                    Success = false,
                    Url = null,
                    Message = $"Upload failed: {ex.Message}"
                };
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // STEP 1 — Get OAuth2 access token (client credentials flow)
        // ──────────────────────────────────────────────────────────────────
        private async Task<string> GetAccessTokenAsync()
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["scope"] = "https://graph.microsoft.com/.default"
            });

            using var response = await _http.PostAsync(tokenEndpoint, body);
            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Token request failed ({response.StatusCode}): {json}");

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString()
                   ?? throw new Exception("access_token missing from token response.");
        }

        // ──────────────────────────────────────────────────────────────────
        // STEP 2 — Upload via resumable upload session (handles any file size)
        // ──────────────────────────────────────────────────────────────────
        private async Task<string> UploadFileAsync(string token, string localFilePath, string fileName)
        {
            // Destination path inside OneDrive: UploadFolder/filename
            string destPath = Uri.EscapeDataString($"{_uploadFolder}/{fileName}");
            string createSessionUrl =
                $"https://graph.microsoft.com/v1.0/users/{_userId}/drive/root:/{destPath}:/createUploadSession";

            // Create upload session
            var sessionPayload = JsonSerializer.Serialize(new
            {
                item = new
                {
                    // "@microsoft.graph.conflictBehavior" options: fail | replace | rename
                    conflictBehavior = "replace",
                    name = fileName
                }
            });

            using var sessionRequest = new HttpRequestMessage(HttpMethod.Post, createSessionUrl);
            sessionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            sessionRequest.Content = new StringContent(sessionPayload, Encoding.UTF8, "application/json");

            using var sessionResponse = await _http.SendAsync(sessionRequest);
            string sessionJson = await sessionResponse.Content.ReadAsStringAsync();

            if (!sessionResponse.IsSuccessStatusCode)
                throw new Exception($"Failed to create upload session ({sessionResponse.StatusCode}): {sessionJson}");

            using var sessionDoc = JsonDocument.Parse(sessionJson);
            string uploadUrl = sessionDoc.RootElement.GetProperty("uploadUrl").GetString()
                               ?? throw new Exception("uploadUrl missing from session response.");

            // Stream file in 10 MB chunks
            const int chunkSize = 10 * 1024 * 1024; // 10 MB
            await using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            long fileSize = fileStream.Length;
            byte[] buffer = new byte[chunkSize];
            long offset = 0;
            string? driveItemId = null;

            while (offset < fileSize)
            {
                int bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize);
                long rangeEnd = offset + bytesRead - 1;

                using var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
                chunkContent.Headers.ContentLength = bytesRead;
                chunkContent.Headers.ContentRange =
                    new ContentRangeHeaderValue(offset, rangeEnd, fileSize);
                chunkContent.Headers.ContentType =
                    new MediaTypeHeaderValue("application/octet-stream");

                using var chunkResponse = await _http.PutAsync(uploadUrl, chunkContent);
                string chunkJson = await chunkResponse.Content.ReadAsStringAsync();

                // 201 Created / 200 OK = final chunk done; 202 Accepted = more chunks needed
                if (chunkResponse.StatusCode == System.Net.HttpStatusCode.Created ||
                    chunkResponse.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    using var resultDoc = JsonDocument.Parse(chunkJson);
                    driveItemId = resultDoc.RootElement.GetProperty("id").GetString();
                    break;
                }

                if (chunkResponse.StatusCode != System.Net.HttpStatusCode.Accepted)
                    throw new Exception($"Chunk upload failed ({chunkResponse.StatusCode}): {chunkJson}");

                offset += bytesRead;
            }

            return driveItemId ?? throw new Exception("Drive item ID was not returned after upload.");
        }

        // ──────────────────────────────────────────────────────────────────
        // STEP 3 — Create an anonymous shareable view link
        // ──────────────────────────────────────────────────────────────────
        private async Task<string> CreateShareLinkAsync(string token, string driveItemId)
        {
            string linkUrl =
                $"https://graph.microsoft.com/v1.0/users/{_userId}/drive/items/{driveItemId}/createLink";

            var payload = JsonSerializer.Serialize(new
            {
                type = "view",     // read-only
                scope = "anonymous" // anyone with the link; use "organization" to restrict to your tenant
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, linkUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request);
            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to create sharing link ({response.StatusCode}): {json}");

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                      .GetProperty("link")
                      .GetProperty("webUrl")
                      .GetString()
                   ?? throw new Exception("webUrl missing from createLink response.");
        }
    }

    // ── Result DTO ─────────────────────────────────────────────────────────
    public class OneDriveUploadResult
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Message { get; set; }
    }
}