const collator = new Intl.Collator('it', { sensitivity: 'base' });
export function compareDecimal(left, right) {
    const parse = value => {
        const text = String(value).trim().replace(',', '.');
        if (!/^[+-]?\d+(\.\d+)?$/.test(text)) return null;
        const [whole, fraction = ''] = text.replace(/^[+-]/, '').split('.');
        const integer = whole.replace(/^0+(?=\d)/, '');
        return { negative: text.startsWith('-') && /[1-9]/.test(integer + fraction), integer, fraction };
    };
    const a = parse(left), b = parse(right);
    if (!a || !b) return a ? 1 : b ? -1 : 0;
    if (a.negative !== b.negative) return a.negative ? -1 : 1;
    const width = Math.max(a.fraction.length, b.fraction.length);
    const x = a.integer + a.fraction.padEnd(width, '0'), y = b.integer + b.fraction.padEnd(width, '0');
    const order = a.integer.length === b.integer.length ? (x < y ? -1 : x > y ? 1 : 0) : Math.sign(a.integer.length - b.integer.length);
    return a.negative ? -order : order;
}
function el(tag, attributes = {}, text) {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, value);
    if (text !== undefined) node.textContent = text;
    return node;
}
export class DataGrid {
    constructor(root) {
        this.root = root;
        this.table = root.querySelector('table'); this.body = this.table.tBodies[0];
        this.headers = [...this.table.tHead.rows[0].cells]; this.rows = [...this.body.rows];
        this.columns = this.headers.map(header => ({ type: header.dataset.type || 'text', label: header.textContent.trim(), filters: [] }));
        const [column, direction] = (root.dataset.sort || '0:desc').split(':');
        this.sortColumn = Number(column); this.direction = direction === 'asc' ? 1 : -1; this.page = 0; this.size = 10;
        this.filterRow = el('tr', { class: 'grid-filters', hidden: '', id: `filters-${crypto.randomUUID()}` });
        const toolbar = el('div', { class: 'grid-toolbar' });
        const filterButton = el('button', { type: 'button', class: 'button button-small', 'aria-expanded': 'false', 'aria-controls': this.filterRow.id }, 'Filtri per colonna');
        filterButton.addEventListener('click', () => { this.filterRow.hidden = !this.filterRow.hidden; filterButton.setAttribute('aria-expanded', String(!this.filterRow.hidden)); });
        const clear = el('button', { type: 'button', class: 'button button-small' }, 'Azzera filtri');
        clear.addEventListener('click', () => this.clearFilters());
        const actions = el('div', { class: 'flex flex-wrap gap-2' }); actions.append(filterButton, clear);
        this.status = el('span', { class: 'grid-status', role: 'status', 'aria-live': 'polite' }); toolbar.append(actions, this.status);
        root.querySelector('.table-scroll').before(toolbar);
        this.columns.forEach((column, index) => this.configureColumn(column, index)); this.table.tHead.append(this.filterRow);
        this.emptyRow = el('tr'); this.emptyRow.append(el('td', { colspan: this.columns.length, class: 'muted' }, 'Nessun risultato.')); this.body.append(this.emptyRow);
        this.configurePager(); this.configureEditing(); this.refreshChoices();
        root.addEventListener('change', event => {
            if (!event.target.matches('[data-grid-value]')) return;
            this.refreshChoices();
            const position = this.orderedRows().filter(row => this.matches(row)).indexOf(event.target.closest('tr'));
            this.page = position < 0 ? 0 : Math.floor(position / this.size);
            this.render();
        });
        root.addEventListener('invalid', event => {
            const row = event.target.closest('tr');
            if (this.rows.includes(row) && !this.revealingInvalid) {
                this.revealingInvalid = true;
                this.reveal(row);
                setTimeout(() => { this.revealingInvalid = false; }, 0);
            }
        }, true);
        root.addEventListener('keydown', event => {
            if (event.key === 'Enter' && event.target.closest('.grid-filters') && event.target.matches('input')) event.preventDefault();
        });
        this.render();
        const invalidRow = this.rows.find(row => row.querySelector('.input-validation-error, .field-validation-error'));
        if (invalidRow) this.reveal(invalidRow);
    }
    value(row, index) {
        const cell = row.cells[index], input = cell.querySelector('[data-grid-value]');
        return input ? input.value : cell.dataset.value ?? cell.textContent.trim();
    }
    configureColumn(column, index) {
        const cell = el('th'); this.filterRow.append(cell);
        if (column.type === 'none') return;
        const header = this.headers[index];
        const button = el('button', { type: 'button', class: 'grid-sort', 'aria-label': `Ordina per ${column.label}` });
        column.indicator = el('span', { 'aria-hidden': 'true' }); button.append(document.createTextNode(column.label), column.indicator);
        button.addEventListener('click', () => { this.direction = this.sortColumn === index ? -this.direction : 1; this.sortColumn = index; this.page = 0; this.render(); });
        header.replaceChildren(button);
        const add = (type, label, bound) => {
            const wrapper = el('label', { class: 'mb-1' }); wrapper.append(el('span', { class: 'sr-only' }, `${column.label}: ${label}`));
            const control = type === 'select' ? el('select', { class: 'field', 'aria-label': `${column.label}: ${label}` }) : el('input', { type, class: 'field', placeholder: label, 'aria-label': `${column.label}: ${label}` });
            if (column.type === 'number') control.setAttribute('inputmode', 'decimal');
            control.addEventListener(type === 'select' ? 'change' : 'input', () => { this.page = 0; this.render(); });
            wrapper.append(control); cell.append(wrapper); column.filters.push({ control, bound });
        };
        if (column.type === 'date' || column.type === 'number') {
            add(column.type === 'date' ? 'date' : 'text', column.type === 'date' ? 'Dal' : 'Minimo', 'min');
            add(column.type === 'date' ? 'date' : 'text', column.type === 'date' ? 'Al' : 'Massimo', 'max');
        } else add(column.type === 'select' ? 'select' : 'search', column.type === 'select' ? 'Tutti' : 'Cerca', 'match');
    }
    refreshChoices() {
        this.columns.forEach((column, index) => {
            if (column.type !== 'select') return;
            const select = column.filters[0].control, previous = select.value, choices = new Map();
            for (const row of this.rows) {
                const input = row.cells[index].querySelector('select[data-grid-value]');
                if (input) for (const option of input.options) choices.set(option.value, option.textContent);
                else choices.set(this.value(row, index), row.cells[index].textContent.trim());
            }
            select.replaceChildren(el('option', { value: '' }, 'Tutti'));
            for (const [value, text] of [...choices].sort((a, b) => collator.compare(a[1], b[1]))) select.append(el('option', { value }, text));
            select.value = previous;
        });
    }
    matches(row) {
        return this.columns.every((column, index) => column.filters.every(({ control, bound }) => {
            const filter = control.value.trim(); if (!filter) return true;
            const value = this.value(row, index);
            if (column.type === 'number') {
                if (!/^[+-]?\d+(?:[.,]\d+)?$/.test(filter) || !/^[+-]?\d+(?:[.,]\d+)?$/.test(value.trim())) return false;
                return bound === 'min' ? compareDecimal(value, filter) >= 0 : compareDecimal(value, filter) <= 0;
            }
            if (column.type === 'date') return value && (bound === 'min' ? value >= filter : value <= filter);
            if (column.type === 'select') return value === filter;
            return value.toLocaleLowerCase('it').includes(filter.toLocaleLowerCase('it'));
        }));
    }
    orderedRows() {
        const type = this.columns[this.sortColumn]?.type;
        return [...this.rows].sort((a, b) => {
            const left = this.value(a, this.sortColumn), right = this.value(b, this.sortColumn);
            const comparison = type === 'number' ? compareDecimal(left, right) : type === 'date' ? (left < right ? -1 : left > right ? 1 : 0) : collator.compare(left, right);
            return this.direction * comparison;
        });
    }
    render() {
        const active = document.activeElement;
        const ordered = this.orderedRows(), matching = ordered.filter(row => this.matches(row));
        const pages = Math.max(1, Math.ceil(matching.length / this.size)); this.page = Math.min(this.page, pages - 1);
        const visible = new Set(matching.slice(this.page * this.size, (this.page + 1) * this.size)), fragment = document.createDocumentFragment();
        for (const row of ordered) { row.hidden = !visible.has(row); fragment.append(row); }
        this.body.insertBefore(fragment, this.emptyRow); this.emptyRow.hidden = matching.length !== 0;
        this.columns.forEach((column, index) => {
            if (!column.indicator) return;
            this.headers[index].setAttribute('aria-sort', index === this.sortColumn ? (this.direction === 1 ? 'ascending' : 'descending') : 'none');
            column.indicator.textContent = index === this.sortColumn ? (this.direction === 1 ? '↑' : '↓') : '↕';
        });
        this.status.textContent = `${matching.length} risultati su ${this.rows.length}`; this.pageStatus.textContent = `Pagina ${this.page + 1} di ${pages}`;
        this.previous.disabled = this.page === 0; this.next.disabled = this.page >= pages - 1;
        if (active?.isConnected && this.root.contains(active) && !active.closest('tr')?.hidden && document.activeElement !== active)
            active.focus({ preventScroll: true });
    }
    clearFilters(render = true) {
        this.columns.forEach(column => column.filters.forEach(({ control }) => { control.value = ''; })); this.page = 0;
        if (render) this.render();
    }
    reveal(row) { this.clearFilters(false); this.page = Math.floor(this.orderedRows().indexOf(row) / this.size); this.render(); }
    configurePager() {
        const pager = el('div', { class: 'grid-pager' }), label = el('label', { class: 'flex items-center gap-2' }, 'Righe per pagina');
        const size = el('select', { class: 'field w-auto', 'aria-label': 'Righe per pagina' });
        for (const value of [10, 25, 50]) size.append(el('option', { value }, String(value)));
        size.addEventListener('change', () => { this.size = Number(size.value); this.page = 0; this.render(); }); label.append(size);
        const actions = el('div', { class: 'flex items-center gap-2' });
        this.previous = el('button', { type: 'button', class: 'button button-small', 'aria-label': 'Pagina precedente' }, 'Precedente');
        this.next = el('button', { type: 'button', class: 'button button-small', 'aria-label': 'Pagina successiva' }, 'Successiva');
        this.pageStatus = el('span', { class: 'grid-status' });
        this.previous.addEventListener('click', () => { this.page--; this.render(); }); this.next.addEventListener('click', () => { this.page++; this.render(); });
        actions.append(this.previous, this.pageStatus, this.next); pager.append(label, actions); this.root.append(pager);
    }
    configureEditing() {
        const add = this.root.querySelector('[data-add-row]');
        add?.addEventListener('click', () => {
            const content = this.root.querySelector('[data-row-template]').content.cloneNode(true), key = crypto.randomUUID().replaceAll('-', '');
            for (const node of content.querySelectorAll('*')) for (const attr of [...node.attributes])
                if (attr.value.includes('__key__')) node.setAttribute(attr.name, attr.value.replaceAll('__key__', key));
            const row = content.querySelector('tr'); this.body.insertBefore(row, this.emptyRow); this.rows.push(row);
            this.refreshChoices(); this.reveal(row); row.querySelector('[data-grid-value]')?.focus();
        });
        this.body.addEventListener('click', event => {
            const remove = event.target.closest('[data-remove-row]'); if (!remove) return;
            const row = remove.closest('tr'); this.rows = this.rows.filter(item => item !== row); row.remove();
            this.refreshChoices(); this.render(); add?.focus();
        });
    }
}
export function initializeGrids(scope = document) {
    return [...scope.querySelectorAll('[data-grid]')].filter(root => !root.dataset.initialized).map(root => {
        root.dataset.initialized = 'true'; return new DataGrid(root);
    });
}
