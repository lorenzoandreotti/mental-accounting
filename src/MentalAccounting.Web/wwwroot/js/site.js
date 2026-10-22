import { initializeGrids } from './grid.js';
initializeGrids();
document.querySelector('.input-validation-error')?.focus();
const toggle = document.querySelector('[data-nav-toggle]');
const navigation = document.querySelector('[data-navigation]');
toggle?.addEventListener('click', () => {
    const expanded = toggle.getAttribute('aria-expanded') !== 'true';
    toggle.setAttribute('aria-expanded', String(expanded));
    navigation.classList.toggle('hidden', !expanded);
    navigation.classList.toggle('flex', expanded);
});
for (const link of document.querySelectorAll('nav a[href]')) {
    if (new URL(link.href).pathname === location.pathname) link.setAttribute('aria-current', 'page');
}
