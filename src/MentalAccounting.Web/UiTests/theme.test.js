import { beforeEach, expect, it, vi } from 'vitest';
import source from '../wwwroot/js/theme.js?raw';
function load(dark = false, ready = true) {
    const media = { matches: dark, addEventListener: vi.fn() };
    window.matchMedia = vi.fn(() => media); window.eval(source);
    if (ready) document.dispatchEvent(new Event('DOMContentLoaded'));
    return media;
}
beforeEach(() => {
    const values = new Map();
    const storage = { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
    Object.defineProperty(window, 'localStorage', { value: storage, configurable: true }); vi.stubGlobal('localStorage', storage);
    document.documentElement.removeAttribute('data-theme'); document.documentElement.removeAttribute('data-theme-mode');
    document.body.innerHTML = '<select data-theme-mode-select><option value="automatic">Automatica</option><option value="manual">Manuale</option></select><button data-theme-toggle></button><output data-theme-description></output>';
});
it('applies automatic theme and toggle visibility before DOMContentLoaded', () => {
    const media = load(true, false);
    expect(document.documentElement.dataset.theme).toBe('dark'); expect(document.documentElement.dataset.themeMode).toBe('automatic');
    expect(document.querySelector('button').hidden).toBe(true);
    document.dispatchEvent(new Event('DOMContentLoaded'));
    media.matches = false; media.addEventListener.mock.calls[0][1](); expect(document.documentElement.dataset.theme).toBe('light');
});
it('restores an existing manual choice and lets the header toggle change it', () => {
    localStorage.setItem('accantona.theme', 'dark'); const media = load(false);
    const toggle = document.querySelector('button'); expect(toggle.hidden).toBe(false); expect(toggle.textContent).toBe('Chiaro');
    toggle.click(); expect(localStorage.getItem('accantona.theme')).toBe('light'); expect(document.documentElement.dataset.theme).toBe('light');
    media.matches = true; media.addEventListener.mock.calls[0][1](); expect(document.documentElement.dataset.theme).toBe('light');
});
it('keeps effective color when switching to manual and follows system again in automatic', () => {
    const media = load(true); const select = document.querySelector('select'); select.value = 'manual'; select.dispatchEvent(new Event('change'));
    expect(localStorage.getItem('accantona.theme')).toBe('dark'); expect(document.querySelector('button').hidden).toBe(false);
    select.value = 'automatic'; select.dispatchEvent(new Event('change')); expect(document.querySelector('button').hidden).toBe(true);
    media.matches = false; media.addEventListener.mock.calls[0][1](); expect(document.documentElement.dataset.theme).toBe('light');
});
it('works without storage and emits a theme event for the chart', () => {
    const get = vi.spyOn(localStorage, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    const set = vi.spyOn(localStorage, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
    const changed = vi.fn(); window.addEventListener('accantona:themechange', changed);
    load(false); const select = document.querySelector('select'); select.value = 'manual'; select.dispatchEvent(new Event('change'));
    document.querySelector('button').click(); expect(document.documentElement.dataset.theme).toBe('dark'); expect(changed).toHaveBeenCalled();
    window.removeEventListener('accantona:themechange', changed); get.mockRestore(); set.mockRestore();
});
