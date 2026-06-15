// Utils/DownloadLinkGenerator.cs
namespace ARS.Classess.Utils
{
    public class DownloadLinkGenerator
    {
        private readonly IConfiguration _config;
        private readonly DownloadTokenStore _tokenStore;

        public DownloadLinkGenerator(IConfiguration config, DownloadTokenStore tokenStore)
        {
            _config = config;
            _tokenStore = tokenStore;
        }

        public async Task<string> GenerateAsync(int executionId, string filePath, TimeSpan? ttl = null)
        {
            var token = await _tokenStore.GenerateAsync(executionId, filePath, ttl);
            var baseUrl = _config["App:BaseUrl"];
            return $"{baseUrl}/api/download/{token}";
        }
    }
}