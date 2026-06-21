// ─────────────────────────────────────────────────────────────────────────────
// theme.js — theme persistence + layout helpers (no external libraries)
//
// The single source of truth for the active theme is the data-dark / data-theme
// attributes on <html>. They are first applied by the inline boot script in
// App.razor (before first paint, so there is no flash of the wrong theme); this
// module is the runtime API the Blazor ThemeService calls when the user toggles.
// ─────────────────────────────────────────────────────────────────────────────
window.dhlTheme = {
    apply: function (theme) {
        var dark = theme === 'dark';
        var el = document.documentElement;
        el.setAttribute('data-dark', dark ? 'true' : 'false');
        el.setAttribute('data-theme', dark ? 'dark' : 'light');
    },
    set: function (theme) {
        try { localStorage.setItem('dhl-theme', theme); } catch (e) { /* private mode */ }
        this.apply(theme);
    },
    current: function () {
        try { return localStorage.getItem('dhl-theme') === 'dark' ? 'dark' : 'light'; }
        catch (e) { return 'light'; }
    },
    isDark: function () { return this.current() === 'dark'; }
};

// Layout helpers — keep the hamburger doing the right thing per breakpoint without
// a continuous resize listener (one cheap call per click → minimal interop chatter).
window.dhlLayout = {
    isMobile: function () { return window.matchMedia('(max-width: 768px)').matches; },

    // Remembered desktop collapse state (mini-rail). Mobile drawer is never persisted.
    getCollapsed: function () {
        try { return localStorage.getItem('dhl-sidebar') === 'collapsed'; }
        catch (e) { return false; }
    },
    setCollapsed: function (collapsed) {
        try { localStorage.setItem('dhl-sidebar', collapsed ? 'collapsed' : 'expanded'); }
        catch (e) { /* private mode */ }
    }
};
