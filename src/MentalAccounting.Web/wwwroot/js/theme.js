// Runs before first paint; the stored values remain compatible with earlier versions.
(function () {
    const root = document.documentElement;
    let preference = 'system';
    try { preference = localStorage.getItem('accantona.theme') || 'system'; } catch { /* Optional storage. */ }
    if (!['system', 'light', 'dark'].includes(preference)) preference = 'system';
    const media = window.matchMedia?.('(prefers-color-scheme: dark)');
    function syncControls() {
        const automatic = preference === 'system';
        document.querySelectorAll('[data-theme-mode-select]').forEach(select => { select.value = automatic ? 'automatic' : 'manual'; });
        document.querySelectorAll('[data-theme-toggle]').forEach(toggle => {
            toggle.hidden = automatic;
            toggle.textContent = root.dataset.theme === 'dark' ? 'Chiaro' : 'Scuro';
            toggle.setAttribute('aria-label', `Passa al tema ${root.dataset.theme === 'dark' ? 'chiaro' : 'scuro'}`);
            toggle.setAttribute('aria-pressed', String(root.dataset.theme === 'dark'));
        });
        document.querySelectorAll('[data-theme-description]').forEach(output => {
            output.textContent = `${automatic ? 'Modalità automatica' : 'Modalità manuale'} · Tema ${root.dataset.theme === 'dark' ? 'scuro' : 'chiaro'}.`;
        });
    }
    function apply() {
        root.dataset.themeMode = preference === 'system' ? 'automatic' : 'manual';
        root.dataset.theme = preference === 'system' ? (media?.matches ? 'dark' : 'light') : preference;
        syncControls();
        window.dispatchEvent(new CustomEvent('accantona:themechange', { detail: { theme: root.dataset.theme, mode: root.dataset.themeMode } }));
    }
    function set(value) {
        preference = value;
        try { localStorage.setItem('accantona.theme', preference); } catch { /* Keep the in-memory choice. */ }
        apply();
    }
    apply();
    media?.addEventListener('change', () => { if (preference === 'system') apply(); });
    document.addEventListener('DOMContentLoaded', () => {
        syncControls();
        document.querySelectorAll('[data-theme-mode-select]').forEach(select => {
            select.addEventListener('change', () => set(select.value === 'automatic' ? 'system' : root.dataset.theme));
        });
        document.querySelectorAll('[data-theme-toggle]').forEach(toggle => {
            toggle.addEventListener('click', () => { if (preference !== 'system') set(root.dataset.theme === 'dark' ? 'light' : 'dark'); });
        });
    }, { once: true });
})();
