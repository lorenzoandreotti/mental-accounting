// Runs synchronously in <head>, before styles are painted. Storage is optional.
(function () {
    const root = document.documentElement;
    let preference = 'system';
    try { preference = localStorage.getItem('accantona.theme') || 'system'; } catch { /* Private mode. */ }
    if (!['light', 'dark', 'system'].includes(preference)) preference = 'system';
    const media = window.matchMedia?.('(prefers-color-scheme: dark)');
    function apply() { root.dataset.theme = preference === 'system' ? (media?.matches ? 'dark' : 'light') : preference; }
    function set(value) {
        preference = ['light', 'dark', 'system'].includes(value) ? value : 'system';
        try { localStorage.setItem('accantona.theme', preference); } catch { /* The selector still works. */ }
        apply();
    }
    apply();
    media?.addEventListener('change', () => { if (preference === 'system') apply(); });
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-theme-select]').forEach(select => {
            select.value = preference;
            select.addEventListener('change', () => set(select.value));
        });
    }, { once: true });
})();
