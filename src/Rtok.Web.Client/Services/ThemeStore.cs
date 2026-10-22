using Microsoft.JSInterop;
using Radzen;

namespace Rtok.Web.Client.Services;

/// <summary>The light/dark toggle: one free Radzen theme each, set through Radzen's
/// <see cref="ThemeService"/> and kept in localStorage as <c>rtok-theme</c>.</summary>
public sealed class ThemeStore
{
    /// <summary>The localStorage key.</summary>
    public const string Key = "rtok-theme";

    /// <summary>The light Radzen theme.</summary>
    public const string LightTheme = "standard";

    /// <summary>The dark Radzen theme.</summary>
    public const string DarkTheme = "standard-dark";

    private readonly ThemeService _themes;
    private readonly IJSRuntime _js;

    /// <summary>Creates the store.</summary>
    /// <param name="themes">Radzen's theme service.</param>
    /// <param name="js">localStorage.</param>
    public ThemeStore(ThemeService themes, IJSRuntime js)
    {
        _themes = themes;
        _js = js;
    }

    /// <summary>True while the dark theme is on.</summary>
    public bool IsDark => string.Equals(_themes.Theme, DarkTheme, StringComparison.Ordinal);

    /// <summary>Applies the saved choice; light when there is none.</summary>
    /// <returns>A task that completes when the theme is set.</returns>
    public async Task LoadAsync()
    {
        string? saved = null;
        try
        {
            saved = await _js.InvokeAsync<string?>("localStorage.getItem", Key)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Storage blocked: the default stays.
        }
        _themes.SetTheme(
            string.Equals(saved, "dark", StringComparison.Ordinal) ? DarkTheme : LightTheme
        );
    }

    /// <summary>Switches light and dark and saves the choice.</summary>
    /// <returns>A task that completes when the choice is saved.</returns>
    public async Task ToggleAsync()
    {
        var dark = !IsDark;
        _themes.SetTheme(dark ? DarkTheme : LightTheme);
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", Key, dark ? "dark" : "light")
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Storage blocked: the choice lasts this page only.
        }
    }
}
