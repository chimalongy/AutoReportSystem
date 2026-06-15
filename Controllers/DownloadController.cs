using ARS.Classess.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ARS.Controllers
{
    [ApiController]
    [Route("api/download")]
    public class DownloadController : ControllerBase
    {
        private readonly DownloadTokenStore _tokenStore;

        public DownloadController(DownloadTokenStore tokenStore) => _tokenStore = tokenStore;

        [HttpGet("{token}")]
        public async Task<IActionResult> Get(string token)
        {
            var filePath = await _tokenStore.ResolveAsync(token);

            if (filePath == null) return NotFound("Link expired or invalid.");
            if (!System.IO.File.Exists(filePath)) return NotFound("File not found.");

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var contentType = filePath.EndsWith(".csv")
                ? "text/csv"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(stream, contentType, Path.GetFileName(filePath));
        }
    }
}