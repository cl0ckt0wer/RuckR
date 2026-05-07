using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public class PitchCreatePage : BasePage
{
    public PitchCreatePage(IPage page, string baseUrl) : base(page, baseUrl) { }

    public async Task GoToAsync() => await NavigateToAsync("/pitches/create");

    /// <summary>
    /// Wait for the pitch creation form to fully render (name input visible).
    /// </summary>
    public async Task WaitForFormLoadedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForSelectorAsync("[data-testid='pitch-name']", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });
    }

    /// <summary>Fill the pitch name input field.</summary>
    public async Task FillPitchNameAsync(string name)
    {
        await Page.GetByTestId("pitch-name").FillAsync(name);
    }

    /// <summary>Select a pitch type from the dropdown.</summary>
    public async Task SelectPitchTypeAsync(string type)
    {
        await Page.GetByTestId("pitch-type").SelectOptionAsync(type);
    }

    /// <summary>Fill the latitude and longitude coordinate inputs.</summary>
    public async Task FillCoordinatesAsync(double lat, double lng)
    {
        await Page.GetByTestId("pitch-latitude").FillAsync(lat.ToString("F6"));
        await Page.GetByTestId("pitch-longitude").FillAsync(lng.ToString("F6"));
    }

    /// <summary>Click the Create Pitch submit button and wait for result.</summary>
    public async Task SubmitAsync()
    {
        await Page.GetByTestId("pitch-submit").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Get the validation or API error message text, if visible.
    /// Returns null if no error is shown.
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()
    {
        var element = await Page.QuerySelectorAsync("[data-testid='pitch-error'], .validation-message");
        if (element is null) return null;

        return (await element.TextContentAsync())?.Trim();
    }

    /// <summary>
    /// Check whether the form was submitted successfully.
    /// Detects either a redirect away from the create page or a success message.
    /// </summary>
    public async Task<bool> GetSuccessMessageAsync()
    {
        var successAlert = await Page.QuerySelectorAsync(".alert-success");
        if (successAlert is not null)
            return true;

        var url = Page.Url;
        return !url.Contains("/pitches/create", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Check whether a 429 rate-limit message is displayed.</summary>
    public async Task<bool> IsRateLimitedAsync()
    {
        return await ExistsAsync("[data-testid='pitch-error']")
            && (await Page.GetByText("5 pitches per day", new() { Exact = false }).IsVisibleAsync());
    }

    /// <summary>Check whether a 409 duplicate pitch error is displayed.</summary>
    public async Task<bool> IsDuplicateErrorAsync()
    {
        return await ExistsAsync("[data-testid='pitch-error']")
            && await Page.GetByText("already exists", new() { Exact = false }).IsVisibleAsync();
    }

    // ── State checks ────────────────────────────────────────────────

    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync(".spinner-border");
    }

    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync("[data-testid='pitch-error']");
    }
}
