const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const vm = require('node:vm');

const source = readFileSync(join(__dirname, '../wwwroot/js/localization.js'), 'utf8');

function browser() {
    const events = [];
    const handlers = {};
    const select = { addEventListener: (name, callback) => { handlers[name] = callback; } };
    const button = { hidden: false };
    const form = {
        elements: { returnUrl: { value: '/Rsvp/Select/1?editSessionId=abc' } },
        querySelector: selector => selector === 'select' ? select : button,
        addEventListener: (name, callback) => { handlers['form-' + name] = callback; },
        requestSubmit() { handlers['form-submit'](); events.push('submitted'); }
    };
    const document = {
        documentElement: { lang: 'en-GB' },
        getElementById: () => ({ textContent: JSON.stringify({ 'Foto {0} de {1}': 'Photo {0} of {1}' }) }),
        querySelectorAll: selector => selector === '[data-language-form]' ? [form] : []
    };
    const window = { dispatchEvent: event => { events.push(event.type); } };
    vm.runInNewContext(source, { document, window, location: { hash: '#family' }, Event: class { constructor(type) { this.type = type; } } });
    return { window, document, form, button, handlers, events };
}

test('formats translated browser messages and preserves unrecognised text', () => {
    const { window } = browser();
    assert.equal(window.weddingText('Foto {0} de {1}', 2, 5), 'Photo 2 of 5');
    assert.equal(window.weddingText('Unknown text'), 'Unknown text');
});

test('saves drafts before posting the language preference and retains the current section', () => {
    const page = browser();
    assert.equal(page.button.hidden, true);
    page.handlers.change();
    assert.deepEqual(page.events, ['wedding:before-language-change', 'submitted']);
    assert.equal(page.form.elements.returnUrl.value, '/Rsvp/Select/1?editSessionId=abc#family');
});

test('updates the music shell language and message catalog without reloading the player', () => {
    const { window, document } = browser();
    window.setWeddingLanguage('fr-FR', { Test: 'Essai' });
    assert.equal(document.documentElement.lang, 'fr-FR');
    assert.equal(window.weddingText('Test'), 'Essai');
});
