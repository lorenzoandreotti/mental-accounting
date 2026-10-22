import { beforeEach, expect, it, vi } from 'vitest';
import source from '../wwwroot/js/theme.js?raw';
function load(dark = false) {
    const media = { matches: dark, addEventListener: vi.fn() };
    window.matchMedia = vi.fn(() => media);
    window.eval(source);
    document.dispatchEvent(new Event('DOMContentLoaded'));
    return media;
}
beforeEach(() => {
    const values = new Map();
    const storage = { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), clear: () => values.clear() };
    Object.defineProperty(window, 'localStorage', { value: storage, configurable: true });
    vi.stubGlobal('localStorage', storage);
    localStorage.clear(); document.documentElement.removeAttribute('data-theme');
    document.body.innerHTML = '<select data-theme-select><option value="system">Sistema</option><option value="light">Chiaro</option><option value="dark">Scuro</option></select>';
});
it('uses the system preference before DOMContentLoaded and responds to changes', () => {
    const media = load(true); expect(document.documentElement.dataset.theme).toBe('dark');
    media.matches = false; media.addEventListener.mock.calls[0][1](); expect(document.documentElement.dataset.theme).toBe('light');
});
it('restores a choice and persists manual changes', () => {
    localStorage.setItem('accantona.theme', 'dark'); const media = load(false);
    expect(document.documentElement.dataset.theme).toBe('dark');
    const select = document.querySelector('select'); select.value = 'light'; select.dispatchEvent(new Event('change'));
    expect(localStorage.getItem('accantona.theme')).toBe('light');
    media.matches = true; media.addEventListener.mock.calls[0][1](); expect(document.documentElement.dataset.theme).toBe('light');
});
it('works when browser storage is blocked', () => {
    const get = vi.spyOn(localStorage, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    const set = vi.spyOn(localStorage, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
    load(false); const select = document.querySelector('select'); select.value = 'dark'; select.dispatchEvent(new Event('change'));
    expect(document.documentElement.dataset.theme).toBe('dark'); get.mockRestore(); set.mockRestore();
});
