import { Chart, LineController, LineElement, PointElement, CategoryScale, LinearScale, Legend, Tooltip } from 'chart.js';
Chart.register(LineController, LineElement, PointElement, CategoryScale, LinearScale, Legend, Tooltip);

const euro = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' });
const dateLabel = value => value.split('-').reverse().join('/');
function validDate(value) {
    return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(`${value}T00:00:00Z`)) &&
        new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value;
}
export function presetRange(points, period) {
    if (!points.length) return { from: '', to: '' };
    const from = points[0].date, to = points.at(-1).date;
    if (period === 'all') return { from, to };
    const months = Number(period);
    if (![1, 3, 6, 12].includes(months)) return { from, to };
    const [year, month, day] = to.split('-').map(Number);
    const index = year * 12 + month - 1 - months;
    const y = Math.floor(index / 12), m = ((index % 12) + 12) % 12;
    const d = Math.min(day, new Date(Date.UTC(y, m + 1, 0)).getUTCDate());
    const start = `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
    return { from: start < from ? from : start, to };
}
export function selectRange(points, from, to) {
    if (!validDate(from) || !validDate(to) || from > to) return { error: 'Indicare un intervallo di date valido, con Dal precedente o uguale ad Al.' };
    const selected = points.filter(point => point.date >= from && point.date <= to);
    if (!selected.length) return { error: 'Nessun dato disponibile nel periodo selezionato.' };
    return { points: selected, from: selected[0].date, to: selected.at(-1).date };
}
function colors() {
    const dark = document.documentElement.dataset.theme === 'dark';
    return { available: dark ? '#83d8b5' : '#16684e', total: dark ? '#aebdff' : '#5751a7', text: dark ? '#a4b8b5' : '#536765', grid: dark ? '#344644' : '#d4dedd', surface: dark ? '#182222' : '#ffffff' };
}
export function chartConfiguration(points) {
    const palette = colors();
    return {
        type: 'line',
        data: {
            labels: points.map(point => point.date),
            datasets: [
                { label: 'Saldo disponibile', data: points.map(point => point.available), borderColor: palette.available, backgroundColor: palette.available, stepped: 'after', borderWidth: 2, pointRadius: points.length === 1 ? 3 : 0 },
                { label: 'Saldo con interessi netti maturati', data: points.map(point => point.total), borderColor: palette.total, backgroundColor: palette.total, borderDash: [6, 4], borderWidth: 2, pointRadius: points.length === 1 ? 3 : 0, tension: 0 }
            ]
        },
        options: {
            responsive: true, maintainAspectRatio: false, animation: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: { position: 'bottom', labels: { color: palette.text, boxWidth: 20 } },
                tooltip: {
                    backgroundColor: palette.surface, titleColor: palette.text, bodyColor: palette.text, borderColor: palette.grid, borderWidth: 1,
                    callbacks: { title: items => items.length ? dateLabel(items[0].label) : '', label: item => `${item.dataset.label}: ${euro.format(item.parsed.y)}` }
                }
            },
            scales: {
                x: { grid: { display: false }, ticks: { color: palette.text, maxTicksLimit: 7, maxRotation: 0, callback: function (value) { return dateLabel(this.getLabelForValue(value)); } } },
                y: { grid: { color: palette.grid }, ticks: { color: palette.text, callback: value => euro.format(value) } }
            }
        }
    };
}
export function mountBalanceChart(root, createChart = (canvas, config) => new Chart(canvas, config)) {
    const points = JSON.parse(root.querySelector('[data-history]').textContent);
    const from = root.querySelector('[data-chart-from]'), to = root.querySelector('[data-chart-to]');
    const day = root.querySelector('[data-chart-day]'), output = root.querySelector('[data-chart-values]');
    const error = root.querySelector('[data-chart-error]');
    const buttons = [...root.querySelectorAll('[data-chart-period]')];
    if (!points.length) { error.textContent = 'Nessun dato disponibile.'; error.hidden = false; return null; }
    let visible = points;
    from.min = to.min = points[0].date; from.max = to.max = points.at(-1).date;
    from.value = from.min; to.value = to.max;
    day.min = from.value; day.max = to.value; day.value = day.max;
    function readDay() {
        const point = visible.find(item => item.date === day.value);
        output.textContent = point ? `${dateLabel(point.date)} · Saldo disponibile: ${euro.format(point.available)} · Saldo con interessi netti maturati: ${euro.format(point.total)}` : 'Scegli una data compresa nel periodo visualizzato.';
    }
    readDay();
    const chart = createChart(root.querySelector('[data-chart-canvas]'), chartConfiguration(visible));
    function redraw() {
        const config = chartConfiguration(visible);
        chart.data = config.data; chart.options = config.options; chart.update('none');
    }
    function applyRange(start, end, period = null) {
        const result = selectRange(points, start, end);
        error.hidden = !result.error; error.textContent = result.error || '';
        if (result.error) return false;
        visible = result.points; from.value = result.from; to.value = result.to;
        day.min = result.from; day.max = result.to;
        if (day.value < day.min || day.value > day.max || !day.value) day.value = day.max;
        buttons.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.chartPeriod === period)));
        readDay(); redraw(); return true;
    }
    root.querySelector('[data-chart-range]').addEventListener('submit', event => { event.preventDefault(); applyRange(from.value, to.value); });
    buttons.forEach(button => button.addEventListener('click', () => {
        const range = presetRange(points, button.dataset.chartPeriod); applyRange(range.from, range.to, button.dataset.chartPeriod);
    }));
    day.addEventListener('change', readDay);
    window.addEventListener('accantona:themechange', redraw);
    return { chart, applyRange, readDay, destroy() { window.removeEventListener('accantona:themechange', redraw); chart.destroy(); } };
}
export function initializeBalanceCharts() {
    return [...document.querySelectorAll('[data-balance-chart]')].map(root => mountBalanceChart(root));
}
