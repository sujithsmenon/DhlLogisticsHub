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

// Opens PDF bytes (base64 from Blazor) in a new tab — used by the invoice Preview, which builds the
// document in memory and persists nothing. Falls back to a download if the popup is blocked.
window.dhlOpenPdf = function (base64, fileName) {
    try {
        var bin = atob(base64);
        var buf = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
        var url = URL.createObjectURL(new Blob([buf], { type: 'application/pdf' }));

        var w = window.open(url, '_blank');
        if (!w) {                       // popup blocked → download instead of silently doing nothing
            var a = document.createElement('a');
            a.href = url;
            a.download = fileName || 'invoice.pdf';
            document.body.appendChild(a);
            a.click();
            a.remove();
        }
        // Revoke late: revoking immediately can kill the tab's load in some browsers.
        setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
    } catch (e) {
        console.error('dhlOpenPdf failed', e);
    }
};

// Universal Search — global shortcut. Ctrl+K (and Ctrl+/) focus the search box from anywhere in the app;
// Escape blurs it. Bound once; re-binding is a no-op so a Blazor re-render cannot stack listeners.
window.dhlSearch = {
    _bound: false,
    bindShortcut: function (inputId) {
        if (this._bound) return;
        this._bound = true;

        document.addEventListener('keydown', function (e) {
            var input = document.getElementById(inputId);
            if (!input) return;

            var isK     = (e.key === 'k' || e.key === 'K');
            var isSlash = (e.key === '/');
            if ((e.ctrlKey || e.metaKey) && (isK || isSlash)) {
                e.preventDefault();          // don't let the browser take Ctrl+K for its own search
                input.focus();
                input.select();
                return;
            }
            if (e.key === 'Escape' && document.activeElement === input) input.blur();
        });
    }
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
