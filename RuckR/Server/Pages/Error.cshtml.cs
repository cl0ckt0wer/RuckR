using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace RuckR.Server.Pages
{
    /// <summary>Renders the shared error page and captures request identifiers.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    /// <summary>Defines the server-side class ErrorModel.</summary>
    public class ErrorModel : PageModel
    {
        /// <summary>Gets or sets the request ID.</summary>
        public string? RequestId { get; set; }
        /// <summary>Gets a value indicating whether a request ID is available.</summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        private readonly ILogger<ErrorModel> _logger;
    /// <summary>Initializes a new instance of <see cref="ErrorModel"/>.</summary>
    /// <param name="logger">The logger.</param>
    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }
    /// <summary>Handles GET requests and sets the request ID from trace context.</summary>
    public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }
    }
}
