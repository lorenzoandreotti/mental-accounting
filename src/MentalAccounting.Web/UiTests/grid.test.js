import { beforeEach, describe, expect, it } from 'vitest';
import { compareDecimal, initializeGrids } from '../wwwroot/js/grid.js';

function setup(rows, editor = false) {
    document.body.innerHTML = `<form><section data-grid data-sort="0:desc">${editor ? '<button type="button" data-add-row>Aggiungi</button>' : ''}<div class="table-scroll"><table><thead><tr><th data-type="date">Data</th><th data-type="number">Importo</th><th data-type="select">Tipo</th><th data-type="text">Nota</th>${editor ? '<th data-type="none">Azioni</th>' : ''}</tr></thead><tbody>${rows}</tbody></table></div>${editor ? `<template data-row-template>${editRow('__key__', '2026-01-01', '0')}</template>` : ''}</section></form>`;
    return initializeGrids()[0];
}
const readRow = (date, amount, type, note) => `<tr><td data-value="${date}">${date.split('-').reverse().join('/')}</td><td data-value="${amount}">${amount.replace('.', ',')} €</td><td>${type}</td><td>${note}</td></tr>`;
const editRow = (key, date, amount) => `<tr data-row-key="${key}"><td><input type="hidden" name="Rows.Index" value="${key}"><input data-grid-value type="date" name="Rows[${key}].Date" value="${date}" required></td><td><input data-grid-value name="Rows[${key}].Amount" value="${amount}" required></td><td><select data-grid-value name="Rows[${key}].Kind"><option>Versamento</option><option>Prelievo</option></select></td><td><input data-grid-value name="Rows[${key}].Note" value="nota"></td><td><button type="button" data-remove-row>Rimuovi</button></td></tr>`;
const visible = grid => [...grid.body.rows].filter(row => !row.hidden && row !== grid.emptyRow);
function filter(grid, column, value, bound = 0) {
    const control = grid.columns[column].filters[bound].control;
    control.value = value;
    control.dispatchEvent(new Event(control.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true }));
}
beforeEach(() => { document.body.innerHTML = ''; });
describe('decimal ordering', () => {
    it('compares signed Italian decimals exactly', () => {
        expect(compareDecimal('2,5', '12')).toBe(-1);
        expect(compareDecimal('-12', '-2.50')).toBe(-1);
        expect(compareDecimal('9007199254740992.001', '9007199254740992.002')).toBe(-1);
        expect(compareDecimal('00.10', '0,1')).toBe(0);
    });
});
describe('read grids', () => {
    it('sorts dates chronologically and numeric values rather than displayed strings', () => {
        const grid = setup(readRow('2025-12-31', '12', 'Prelievo', 'A') + readRow('2026-01-01', '2.5', 'Versamento', 'B'));
        expect(visible(grid)[1].cells[0].dataset.value).toBe('2025-12-31');
        grid.headers[1].querySelector('button').click();
        expect([...grid.body.rows][0].cells[1].dataset.value).toBe('2.5');
        expect(grid.headers[1].getAttribute('aria-sort')).toBe('ascending');
    });
    it('combines text, numeric, category and inclusive date filters', () => {
        const grid = setup(readRow('2026-01-10', '10.5', 'Versamento', 'Vacanze') + readRow('2026-01-11', '20', 'Versamento', 'VACANZE') + readRow('2026-01-10', '10', 'Prelievo', 'Vacanze'));
        filter(grid, 3, 'vacanze'); filter(grid, 0, '2026-01-10'); filter(grid, 0, '2026-01-10', 1);
        filter(grid, 1, '10,5'); filter(grid, 2, 'Versamento');
        expect(visible(grid)).toHaveLength(1);
        filter(grid, 1, '100'); expect(visible(grid)).toHaveLength(0); expect(grid.emptyRow.hidden).toBe(false);
        grid.clearFilters(); expect(visible(grid)).toHaveLength(3);
    });
    it('filters before pagination and clamps pages after deleting records', () => {
        const grid = setup(Array.from({ length: 31 }, (_, i) => readRow('2026-01-01', String(i), 'Versamento', i < 16 ? 'A' : 'B')).join(''));
        expect(visible(grid)).toHaveLength(10); grid.next.click(); expect(grid.page).toBe(1);
        filter(grid, 3, 'A'); expect(grid.status.textContent).toBe('16 risultati su 31'); expect(grid.page).toBe(0);
        grid.next.click(); expect(visible(grid)).toHaveLength(6);
        const size = grid.root.querySelector('[aria-label="Righe per pagina"]'); size.value = '25'; size.dispatchEvent(new Event('change'));
        expect(visible(grid)).toHaveLength(16);
    });
});
describe('editable grids', () => {
    it('submits hidden and paginated rows, preserving stable keys when sorted', () => {
        const grid = setup(Array.from({ length: 12 }, (_, i) => editRow(`key${i}`, '2026-01-01', String(i))).join(''), true);
        grid.headers[1].querySelector('button').click(); filter(grid, 1, '10');
        const data = new FormData(document.querySelector('form'));
        expect(data.getAll('Rows.Index')).toHaveLength(12);
        expect(data.get('Rows[key0].Amount')).toBe('0'); expect(data.get('Rows[key11].Amount')).toBe('11');
    });
    it('adds a visible focused row and removes its submitted fields', () => {
        const grid = setup(editRow('existing', '2026-01-01', '1'), true); filter(grid, 1, '999');
        grid.root.querySelector('[data-add-row]').click();
        const row = grid.rows[1], key = row.dataset.rowKey;
        expect(key).not.toBe('__key__'); expect(row.hidden).toBe(false); expect(document.activeElement).toBe(row.querySelector('[data-grid-value]'));
        row.querySelector('[data-remove-row]').click();
        expect(new FormData(document.querySelector('form')).getAll('Rows.Index')).toEqual(['existing']);
    });
    it('reveals an invalid field even on another page or behind filters', () => {
        const grid = setup(Array.from({ length: 12 }, (_, i) => editRow(`k${i}`, '2026-01-01', String(i))).join(''), true);
        const row = grid.rows[11]; filter(grid, 1, '100');
        row.querySelector('[data-grid-value]').dispatchEvent(new Event('invalid', { bubbles: false }));
        expect(row.hidden).toBe(false); expect(grid.page).toBe(1);
    });
    it('keeps a changed row visible and focused after numeric sorting', () => {
        const grid = setup(Array.from({ length: 12 }, (_, i) => editRow(`k${i}`, '2026-01-01', String(i))).join(''), true);
        grid.headers[1].querySelector('button').click();
        const row = grid.rows[0], input = row.cells[1].querySelector('input');
        input.focus(); input.value = '999'; input.dispatchEvent(new Event('change', { bubbles: true }));
        expect(row.hidden).toBe(false); expect(grid.page).toBe(1); expect(document.activeElement).toBe(input);
    });
});
