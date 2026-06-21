using Microsoft.JSInterop;

namespace DhlLogistics.Web.Service;

/// <summary>
/// Light/Dark theme engine for the dashboard shell.
///
/// The actual paint is driven entirely by the <c>data-dark</c> / <c>data-theme</c>
/// attributes on the <c>&lt;html&gt;</c> element. Those attributes are applied
/// <i>before first paint</i> by a tiny inline script in <c>App.razor</c> that reads
/// <c>localStorage</c> synchronously — so a hard refresh never flashes the wrong theme
/// (no flicker). This service is the C# side: it mirrors that state into the Blazor
/// circuit so components (e.g. the theme toggle) can render the correct active state,
/// and it persists + re-applies the theme when the user switches it at runtime.
///
/// Registered <b>Scoped</b> (one instance per Blazor Server circuit). JS interop is
/// only available after the first interactive render, so <see cref="InitializeAsync"/>
/// must be called from <c>OnAfterRenderAsync(firstRender: true)</c>, never from
/// <c>OnInitializedAsync</c> (which also runs during server prerender, where there is
/// no JS runtime).
/// </summary>
public sealed class ThemeService
{
    private readonly IJSRuntime _js;
    private bool _initialized;

    public ThemeService(IJSRuntime js) => _js = js;

    /// <summary>True when the dark theme is active.</summary>
    public bool IsDark { get; private set; }

    /// <summary>The current theme as the string persisted in localStorage.</summary>
    public string Current => IsDark ? "dark" : "light";

    /// <summary>Raised after the theme changes so subscribed components can re-render.</summary>
    public event Action? OnChange;

    /// <summary>
    /// Sync the C# state with the attribute the no-flicker head script already applied.
    /// Safe to call more than once; only the first call hits JS.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            IsDark = await _js.InvokeAsync<bool>("dhlTheme.isDark");
        }
        catch
        {
            // Prerender / JS not ready — stay on the light default; OnAfterRender re-tries.
            _initialized = false;
        }
        OnChange?.Invoke();
    }

    /// <summary>Switch to the given theme, persist it, and re-paint without a reload.</summary>
    public async Task SetDarkAsync(bool dark)
    {
        IsDark = dark;
        await _js.InvokeVoidAsync("dhlTheme.set", dark ? "dark" : "light");
        OnChange?.Invoke();
    }

    /// <summary>Toggle between light and dark.</summary>
    public Task ToggleAsync() => SetDarkAsync(!IsDark);
}
