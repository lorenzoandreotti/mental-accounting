export function initializeMovementModals(root = document) {
    const editor = root.querySelector('#movement-form');
    const form = editor?.querySelector('[data-movement-form]');
    const openers = new Map();
    function show(dialog, opener) {
        if (!dialog || dialog.open) return;
        openers.set(dialog, opener || root.getElementById(dialog.dataset.returnFocus) || root.querySelector('[data-movement-edit][data-id="' + form?.elements.Id.value + '"]') || root.querySelector('[data-movement-open]'));
        dialog.showModal();
        (dialog.querySelector('.input-validation-error') || dialog.querySelector('input:not([type=hidden])') || dialog.querySelector('[data-modal-close]'))?.focus();
    }
    for (const dialog of root.querySelectorAll('[data-modal]')) {
        dialog.querySelectorAll('[data-modal-close]').forEach(button => button.addEventListener('click', () => dialog.close()));
        dialog.addEventListener('close', () => openers.get(dialog)?.focus());
        if (dialog.dataset.autoOpen === 'true') show(dialog);
    }
    for (const opener of root.querySelectorAll('[data-movement-open], [data-movement-edit]')) {
        opener.addEventListener('click', event => {
            if (!form || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            const edit = opener.hasAttribute('data-movement-edit');
            form.elements.Id.value = edit ? opener.dataset.id : '';
            form.elements.Date.value = edit ? opener.dataset.date : form.dataset.today;
            form.elements.Kind.value = edit ? opener.dataset.kind : 'Deposit';
            form.elements.Amount.value = edit ? opener.dataset.amount : '';
            form.elements.Note.value = edit ? opener.dataset.note : '';
            editor.querySelector('#movement-title').textContent = edit ? 'Modifica movimento' : 'Nuovo movimento';
            editor.querySelectorAll('.validation, .field-validation-error').forEach(element => element.textContent = '');
            editor.querySelectorAll('.input-validation-error').forEach(element => element.classList.remove('input-validation-error'));
            show(editor, opener);
        });
    }
    for (const submitting of root.querySelectorAll('[data-prevent-repeat]')) {
        submitting.addEventListener('submit', event => {
            if (submitting.dataset.submitting === 'true') { event.preventDefault(); return; }
            if (event.defaultPrevented) return;
            submitting.dataset.submitting = 'true';
            submitting.querySelectorAll('button:not([type=button])').forEach(button => button.disabled = true);
        });
    }
}
