using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;

namespace CalendarPi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhotosController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public PhotosController(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _config = config;
        }

        [HttpGet]
        public IActionResult Get()
        {
            // configured folder (relative to content root or absolute). Default to wwwroot/photos
            var folder = _config.GetValue<string>("PhotoGallery:Folder") ?? "wwwroot/photos";
            folder = folder.Replace('\\', '/').Trim();

            string physical;
            if (Path.IsPathRooted(folder))
            {
                // absolute path provided -> use as-is
                physical = folder;
            }
            else
            {
                // non-root path: consider relative to the executable (AppContext.BaseDirectory)
                physical = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, folder));
            }

            if (!Directory.Exists(physical)) return Ok(new string[0]);

            var supported = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var files = Directory.GetFiles(physical)
                .Where(f => supported.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();

            // Convert physical paths to web paths relative to wwwroot (WebRootPath)
            var urls = files.Select(f =>
            {
                var webRoot = _env.WebRootPath;
                if (!string.IsNullOrEmpty(webRoot))
                {
                    try
                    {
                        var webRootFull = Path.GetFullPath(webRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var fileFull = Path.GetFullPath(f).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (fileFull.StartsWith(webRootFull, System.StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = Path.GetRelativePath(webRootFull, fileFull).Replace('\\', '/');
                            return "/" + rel;
                        }
                    }
                    catch
                    {
                        // ignore and fall back to raw endpoint
                    }
                }

                // not under webroot or webroot not configured: expose via raw endpoint with a token
                var token = ToToken(Path.GetRelativePath(physical, f));
                return Url.Action("Raw", "Photos", new { token });
            })
            .Where(u => !string.IsNullOrEmpty(u))
            .ToArray();

            return Ok(urls);
        }

        [HttpGet("raw/{token}")]
        public IActionResult Raw(string token)
        {
            if (string.IsNullOrEmpty(token)) return NotFound();

            var folder = _config.GetValue<string>("PhotoGallery:Folder") ?? "wwwroot/photos";
            folder = folder.Replace('\\', '/').Trim();

            string physical;
            if (Path.IsPathRooted(folder))
            {
                physical = folder;
            }
            else
            {
                physical = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, folder));
            }

            var relPath = FromToken(token);
            if (relPath == null) return NotFound();

            var file = Path.Combine(physical, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(file)) return NotFound();

            // ensure file is under the physical folder
            var full = Path.GetFullPath(file);
            var fullFolder = Path.GetFullPath(physical);
            if (!full.StartsWith(fullFolder, System.StringComparison.OrdinalIgnoreCase)) return NotFound();

            var ext = Path.GetExtension(full).ToLowerInvariant();
            var ct = ext switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            return PhysicalFile(full, ct);
        }

        private static string ToToken(string relativePath)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(relativePath);
            return System.Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string? FromToken(string token)
        {
            try
            {
                var padded = token.Replace('-', '+').Replace('_', '/');
                switch (padded.Length % 4)
                {
                    case 2: padded += "=="; break;
                    case 3: padded += "="; break;
                }
                var bytes = System.Convert.FromBase64String(padded);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
