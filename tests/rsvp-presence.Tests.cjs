const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const vm = require('node:vm');

const source = readFileSync(join(__dirname, '../wwwroot/js/rsvp-select.js'), 'utf8');

function events(extra = {}) {
    const listeners = new Map();
    return Object.assign({
        addEventListener(name, callback) {
            if (!listeners.has(name)) listeners.set(name, []);
            listeners.get(name).push(callback);
        },
        emit(name) {
            const event = { defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
            for (const callback of listeners.get(name) || []) callback(event);
            return event;
        }
    }, extra);
}

async function browser({ answerFields = [], savedDraft = null } = {}) {
    let now = 1_000_000;
    let timerId = 0;
    const timers = new Map();
    const requests = [];
    let reply = async () => ({ otherEditor: false, hasChanged: false });
    const submit = { disabled: false };
    const form = events({
        dataset: { editStatusUrl: '/Rsvp/EditStatus', rsvpDraft: 'family' },
        elements: { namedItem: name => ({ value: { GuestId: '7', EditSessionId: 'session', __RequestVerificationToken: 'csrf' }[name] }) },
        querySelectorAll: () => answerFields, querySelector: () => submit
    });
    const controls = {
        'guest-status': events({ form, value: 'Attending' }),
        'rsvp-other-editor': { hidden: true },
        'rsvp-concurrency-conflict': { hidden: true },
        'rsvp-presence-unavailable': { hidden: true },
        'rsvp-draft-restored': { hidden: true }
    };
    const document = events({ hidden: false, getElementById: id => controls[id], querySelectorAll: () => [], querySelector: () => null });
    const window = events();
    const context = vm.createContext({
        document, window, URLSearchParams, Date: { now: () => now },
        sessionStorage: { getItem: () => savedDraft, setItem(key, value) { savedDraft = value; }, removeItem() { savedDraft = null; } },
        setTimeout(callback, delay) { timers.set(++timerId, { callback, at: now + delay }); return timerId; },
        clearTimeout: id => timers.delete(id),
        async fetch(url, options) {
            requests.push({ url, options, at: now });
            const status = await reply();
            return { ok: true, headers: { get: () => 'application/json' }, json: async () => status };
        }
    });
    async function flush() { for (let n = 0; n < 12; n++) await Promise.resolve(); }
    vm.runInContext(source, context);
    await flush();
    return {
        form, document, window, controls, requests, submit,
        savedDraft: () => savedDraft,
        reply: callback => { reply = callback; },
        async advance(milliseconds) {
            const end = now + milliseconds;
            let count = 0;
            while (true) {
                const next = [...timers.entries()].sort((a, b) => a[1].at - b[1].at)[0];
                if (!next || next[1].at > end) break;
                assert.ok(++count < 1000, 'timer must not spin without advancing time');
                now = next[1].at;
                timers.delete(next[0]);
                next[1].callback();
                await flush();
            }
            now = end;
            await flush();
        }
    };
}

test('preserves editable RSVP answers when changing language and restores them in the translated form', async () => {
    const field = value => ({ id: 'message', type: 'textarea', value, closest: () => null });
    const original = await browser({ answerFields: [field('Vegetariano, por favor')] });
    original.window.emit('wedding:before-language-change');
    const saved = original.savedDraft();
    assert.equal(JSON.parse(saved).answers.message, 'Vegetariano, por favor');
    const translatedField = field('');
    const translated = await browser({ answerFields: [translatedField], savedDraft: saved });
    assert.equal(translatedField.value, 'Vegetariano, por favor');
    assert.equal(translated.controls['rsvp-draft-restored'].hidden, false);
    assert.equal(translated.savedDraft(), null);
});

test('checks once per minute and deduplicates activity and initial page events', async () => {
    const page = await browser();
    assert.equal(page.requests.length, 1);
    page.window.emit('pageshow');
    page.document.emit('visibilitychange');
    for (let n = 0; n < 100; n++) page.form.emit('input');
    await page.advance(59_999);
    assert.equal(page.requests.length, 1);
    await page.advance(1);
    assert.equal(page.requests.length, 2);
    const request = page.requests[0];
    assert.equal(request.url, '/Rsvp/EditStatus');
    assert.equal(request.options.method, 'POST');
    assert.equal(request.options.credentials, 'same-origin');
    assert.equal(request.options.cache, 'no-store');
    assert.equal(request.options.body.get('__RequestVerificationToken'), 'csrf');
});

test('stops after three idle minutes and resumes on interaction or returning to the page', async () => {
    const page = await browser();
    await page.advance(10 * 60_000);
    assert.equal(page.requests.length, 3);
    page.form.emit('input');
    await page.advance(0);
    assert.equal(page.requests.length, 4);
    page.document.hidden = true;
    page.document.emit('visibilitychange');
    await page.advance(5 * 60_000);
    assert.equal(page.requests.length, 4);
    page.document.hidden = false;
    page.document.emit('visibilitychange');
    await page.advance(0);
    assert.equal(page.requests.length, 5);
    page.window.emit('pagehide');
    await page.advance(5 * 60_000);
    assert.equal(page.requests.length, 5);
    page.window.emit('pageshow');
    await page.advance(0);
    assert.equal(page.requests.length, 6);
});

test('continues at minute intervals while the guest is actively editing', async () => {
    const page = await browser();
    for (let minute = 0; minute < 10; minute++) {
        page.form.emit('keydown');
        await page.advance(60_000);
    }
    assert.equal(page.requests.length, 11);
});

test('a detected conflict stops polling and prevents submitting a stale answer', async () => {
    const page = await browser();
    page.reply(async () => ({ otherEditor: true, hasChanged: true }));
    await page.advance(60_000);
    assert.equal(page.controls['rsvp-other-editor'].hidden, false);
    assert.equal(page.controls['rsvp-concurrency-conflict'].hidden, false);
    assert.equal(page.submit.disabled, true);
    assert.equal(page.form.emit('submit').defaultPrevented, true);
    page.form.emit('input');
    await page.advance(5 * 60_000);
    assert.equal(page.requests.length, 2);
});

test('pauses during form submission and resumes when restored from browser history', async () => {
    const page = await browser();
    assert.equal(page.form.emit('submit').defaultPrevented, false);
    await page.advance(5 * 60_000);
    assert.equal(page.requests.length, 1);
    page.window.emit('pageshow');
    await page.advance(0);
    assert.equal(page.requests.length, 2);
});

test('a temporary failure keeps saving available and retries after a minute', async () => {
    const page = await browser();
    page.reply(async () => { throw new Error('offline'); });
    await page.advance(60_000);
    assert.equal(page.controls['rsvp-presence-unavailable'].hidden, false);
    assert.equal(page.submit.disabled, false);
    page.reply(async () => ({ otherEditor: false, hasChanged: false }));
    await page.advance(60_000);
    assert.equal(page.controls['rsvp-presence-unavailable'].hidden, true);
    assert.equal(page.requests.length, 3);
});

test('a slow request cannot overlap or restart polling after leaving the page', async () => {
    const page = await browser();
    let complete;
    page.reply(() => new Promise(resolve => { complete = resolve; }));
    await page.advance(60_000);
    assert.equal(page.requests.length, 2);
    for (let n = 0; n < 5; n++) {
        page.form.emit('input');
        page.document.emit('visibilitychange');
        await page.advance(60_000);
    }
    assert.equal(page.requests.length, 2);
    page.window.emit('pagehide');
    complete({ otherEditor: false, hasChanged: false });
    await page.advance(5 * 60_000);
    assert.equal(page.requests.length, 2);
    page.reply(async () => ({ otherEditor: false, hasChanged: false }));
    page.window.emit('pageshow');
    await page.advance(0);
    assert.equal(page.requests.length, 3);
});
