import { beforeEach, expect, it } from 'vitest';
import { initializeMovementModals } from '../wwwroot/js/movement-modal.js';

beforeEach(() => {
    document.body.innerHTML = `<a href="?add=true" data-movement-open>Nuovo</a>
    <a href="?edit=abc" data-movement-edit data-id="abc" data-date="2026-01-02" data-kind="Withdrawal" data-amount="50,25" data-note="Vacanze">Modifica</a>
    <dialog id="movement-form" data-modal data-auto-open="false"><h2 id="movement-title"></h2><button type="button" data-modal-close>Chiudi</button>
    <form data-movement-form data-today="2026-10-22" data-prevent-repeat><input type="hidden" name="Id"><input type="date" name="Date"><select name="Kind"><option>Deposit</option><option>Withdrawal</option></select><input name="Amount"><input name="Note"><button>Salva</button><button type="button" data-modal-close>Annulla</button></form></dialog>
    <dialog id="delete-movement" data-modal data-auto-open="false"><button type="button" data-modal-close>Annulla</button><form data-prevent-repeat><button>Conferma</button></form></dialog>`;
    for (const dialog of document.querySelectorAll('dialog')) {
        dialog.showModal = () => { dialog.open = true; };
        dialog.close = () => { dialog.open = false; dialog.dispatchEvent(new Event('close')); };
    }
});
it('opens a new movement, focuses the date and restores the opener on close', () => {
    initializeMovementModals(); const opener = document.querySelector('[data-movement-open]');opener.click();
    const dialog = document.querySelector('#movement-form'), form = dialog.querySelector('form');
    expect(dialog.open).toBe(true);expect(form.elements.Date.value).toBe('2026-10-22');expect(form.elements.Amount.value).toBe('');
    expect(document.activeElement).toBe(form.elements.Date);
    dialog.querySelector('[data-modal-close]').click();expect(dialog.open).toBe(false);expect(document.activeElement).toBe(opener);
});
it('populates editing and clears it when creating another movement', () => {
    initializeMovementModals();document.querySelector('[data-movement-edit]').click();
    const dialog = document.querySelector('#movement-form'), form = dialog.querySelector('form');
    expect(form.elements.Id.value).toBe('abc');expect(form.elements.Amount.value).toBe('50,25');expect(form.elements.Kind.value).toBe('Withdrawal');expect(form.elements.Note.value).toBe('Vacanze');
    dialog.close();document.querySelector('[data-movement-open]').click();expect(form.elements.Id.value).toBe('');expect(form.elements.Note.value).toBe('');
});
it('opens server errors without replacing values and ignores backdrop clicks', () => {
    const dialog = document.querySelector('#movement-form');dialog.dataset.autoOpen = 'true';
    const input = dialog.querySelector('[name=Amount]');input.value = 'invalid';input.className = 'input-validation-error';
    initializeMovementModals();expect(document.activeElement).toBe(input);expect(input.value).toBe('invalid');
    dialog.click();expect(dialog.open).toBe(true);
    // Native Escape dispatches cancel and close; the module leaves that default intact.
    const cancel = new Event('cancel', { cancelable: true });dialog.dispatchEvent(cancel);expect(cancel.defaultPrevented).toBe(false);
    dialog.close();expect(dialog.open).toBe(false);
});
it('blocks repeated submissions for save and delete while leaving cancellation usable', () => {
    initializeMovementModals();
    for (const form of document.querySelectorAll('[data-prevent-repeat]')) {
        const first = new Event('submit', { cancelable: true });form.dispatchEvent(first);expect(first.defaultPrevented).toBe(false);
        expect(form.querySelector('button:not([type=button])').disabled).toBe(true);
        const second = new Event('submit', { cancelable: true });form.dispatchEvent(second);expect(second.defaultPrevented).toBe(true);
    }
    expect(document.querySelector('[data-modal-close]').disabled).toBe(false);
});
