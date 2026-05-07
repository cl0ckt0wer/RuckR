using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public class PitchCreatePage : BasePage
{
    private const string PitchNameInput = "input[name='Name']";
    private const string PitchTypeSelect = "select[name='Type']";
    private const string LatitudeInput = "input[name='Latitude']";
    private const string LongitudeInput = "input[name='Longitude']";
    private const string SubmitButton = "button[type='submit']";

    public PitchCreatePage(IPage page, string baseUrl) : base(page, baseUrl) { }

    public async Task GoToAsync() => await NavigateToAsync("/pitches/create");

    /// <summary>
    /// Wait for the pitch creation form to fully render (name input visible).
    /// </summary>
    public async Task WaitForFormLoadedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForSelectorAsync(PitchNameInput, new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });
    }

    /// <summary>Fill the pitch name input field.</summary>
    public async Task FillPitchNameAsync(string name)
    {
        await Page.FillAsync(PitchNameInput, name);
    }

    /// <summary>Select a pitch type from the dropdown.</summary>
    public async Task SelectPitchTypeAsync(string type)
    {
        await Page.SelectOptionAsync(PitchTypeSelect, type);
    }

    /// <summary>Fill the latitude and longitude coordinate inputs.</summary>
    public async Task FillCoordinatesAsync(double lat, double lng)
    {
        await Page.FillAsync(LatitudeInput, lat.ToString("F6"));
        await Page.FillAsync(LongitudeInput, lng.ToString("F6"));
    }

    /// <summary>Click the Create Pitch submit button and wait for result.</summary>
    public async Task SubmitAsync()
    {
        await Page.ClickAsync(SubmitButton);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Get the validation or API error message text, if visible.
    /// Returns null if no error is shown.
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()
    {
        var element = await Page.QuerySelectorAsync(".alert-danger, .validation-message");
        if (element is null) return null;

        return (await element.TextContentAsync())?.Trim();
    }

    /// <summary>
    /// Check whether the form was submitted successfully.
    /// Detects either a redirect away from the create page or a success message.
    /// </summary>
    public async Task<bool> GetSuccessMessageAsync()
    {
        // Success is indicated by either a redirect or a success alert
        var successAlert = await Page.QuerySelectorAsync(".alert-success");
        if (successAlert is not null)
            return true;

        // Check if we navigated away (URL no longer contains /pitches/create)
        var url = Page.Url;
        return !url.Contains("/pitches/create", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Check whether a 429 rate-limit message is displayed.</summary>
    public async Task<bool> IsRateLimitedAsync()
    {
        return await ExistsAsync("text=429")
            || await Page.GetByText("too many", new() { Exact = false }).IsVisibleAsync()
            || await Page.GetByText("rate limit", new() { Exact = false }).IsVisibleAsync();
    }

    /// <summary>Check whether a 409 duplicate pitch error is displayed.</summary>
    public async Task<bool> IsDuplicateErrorAsync()
    {
        return await ExistsAsync("text=409")
            || await Page.GetByText("already exists", new() { Exact = false }).IsVisibleAsync()
            || await Page.GetByText("duplicate", new() { Exact = false }).IsVisibleAsync();
    }

    // ── State checks ────────────────────────────────────────────────

    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync(".spinner-border");
    }

    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync(".alert-danger");
    }
}
