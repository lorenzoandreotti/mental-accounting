import { initializeMovementModals } from './movement-modal.js';
﻿import { initializeGrids } from './grid.js';
initializeGrids();
initializeMovementModals();
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
    const target = new URL(link.href).pathname;
    if (target === location.pathname || (link.hasAttribute('data-settings-link') && location.pathname.startsWith('/Settings/')))
        link.setAttribute('aria-current', 'page');
}
const balanceChart = document.querySelector('[data-balance-chart]');
if (balanceChart) {
    import(balanceChart.dataset.chartModule || './balance-chart.bundle.js').then(module => module.initializeBalanceCharts()).catch(() => {
        const error = document.querySelector('[data-chart-error]');
        error.hidden = false;
        error.textContent = 'Grafico non disponibile. I riepiloghi e la cronologia restano consultabili.';
    });
}
