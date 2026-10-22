import { beforeEach, expect, it, vi } from 'vitest';
import { chartConfiguration, mountBalanceChart, presetRange, selectRange } from '../Client/balance-chart.js';

const points = [
    { date: '2024-01-01', available: 1000, accruedNet: .1, total: 1000.1 },
    { date: '2024-02-29', available: 1002, accruedNet: .5, total: 1002.5 },
    { date: '2024-03-31', available: 1005, accruedNet: .25, total: 1005.25 }
];
let controllers = [];
beforeEach(() => { controllers.forEach(controller => controller.destroy()); controllers = []; document.body.innerHTML = ''; });
it('clamps calendar presets including leap February', () => {
    expect(presetRange(points, '1')).toEqual({ from: '2024-02-29', to: '2024-03-31' });
    expect(presetRange(points, 'all')).toEqual({ from: '2024-01-01', to: '2024-03-31' });
    expect(presetRange(points, '12')).toEqual(presetRange(points, 'all'));
});
it('uses inclusive intervals and rejects invalid or empty ranges', () => {
    expect(selectRange(points, '2024-02-29', '2024-03-31').points).toHaveLength(2);
    expect(selectRange(points, '2024-03-31', '2024-02-29').error).toBeTruthy();
    expect(selectRange(points, '2024-02-30', '2024-03-31').error).toBeTruthy();
    expect(selectRange(points, '2023-01-01', '2023-12-31').error).toBeTruthy();
});
it('distinguishes the two lines and updates palette for the dark theme', () => {
    document.documentElement.dataset.theme = 'light'; const light = chartConfiguration(points);
    document.documentElement.dataset.theme = 'dark'; const dark = chartConfiguration(points);
    expect(light.data.datasets[0].data).toEqual([1000, 1002, 1005]);
    expect(light.data.datasets[1].data).toEqual([1000.1, 1002.5, 1005.25]);
    expect(dark.data.datasets[1].borderDash).toEqual([6, 4]); expect(dark.data.datasets[0].borderColor).not.toBe(light.data.datasets[0].borderColor);
});
it('changes only chart range and accessible readout, independently from grid filters', () => {
    document.body.innerHTML = `<input id="grid-filter" value="Prelievo"><section data-balance-chart><script type="application/json" data-history>${JSON.stringify(points)}</script>
        <button data-chart-period="1"></button><button data-chart-period="all"></button><form data-chart-range><input data-chart-from><input data-chart-to></form>
        <p data-chart-error hidden></p><canvas data-chart-canvas></canvas><input data-chart-day><output data-chart-values></output></section>`;
    const chart = { update: vi.fn(), destroy: vi.fn() };
    const controller = mountBalanceChart(document.querySelector('section'), () => chart); controllers.push(controller);
    const gridFilter = document.querySelector('#grid-filter');
    document.querySelector('[data-chart-period="1"]').click(); expect(chart.data.labels).toEqual(['2024-02-29', '2024-03-31']);
    expect(gridFilter.value).toBe('Prelievo');
    const day = document.querySelector('[data-chart-day]'); day.value = '2024-02-29'; day.dispatchEvent(new Event('change'));
    expect(document.querySelector('output').textContent).toContain('29/02/2024'); expect(document.querySelector('output').textContent.replaceAll('.', '')).toContain('1002,50');
    gridFilter.value = 'Versamento'; gridFilter.dispatchEvent(new Event('input')); expect(chart.data.labels).toHaveLength(2);
    window.dispatchEvent(new CustomEvent('accantona:themechange')); expect(chart.update).toHaveBeenCalledWith('none');
    expect(controller.applyRange('2025-01-01', '2025-02-01')).toBe(false); expect(document.querySelector('[data-chart-error]').hidden).toBe(false);
});
