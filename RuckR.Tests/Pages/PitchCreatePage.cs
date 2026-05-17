using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class PitchCreatePage : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""PitchCreatePage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public PitchCreatePage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Verifies go To Async.
    /// </summary>
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
            && (await Page.GetByText("5 pitches per 24 hours", new() { Exact = false }).IsVisibleAsync());
    }

    /// <summary>Check whether a 409 duplicate pitch error is displayed.</summary>
    public async Task<bool> IsDuplicateErrorAsync()
    {
        return await ExistsAsync("[data-testid='pitch-error']")
            && await Page.GetByText("already exists", new() { Exact = false }).IsVisibleAsync();
    }

    // ── State checks ────────────────────────────────────────────────

    /// <summary>
    /// Verifies is Loading Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync(".spinner-border");
    }

    /// <summary>
    /// Verifies is Error Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync("[data-testid='pitch-error']");
    }
}


