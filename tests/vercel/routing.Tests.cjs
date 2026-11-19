const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { getTransformedRoutes } = require('@vercel/routing-utils');
const { resolveAllConfiguredServicesV2, LocalFileSystemDetector } = require('@vercel/fs-detectors');

const root = path.resolve(__dirname, '../..');
const config = JSON.parse(fs.readFileSync(path.join(root, 'vercel.json'), 'utf8'));
const transformed = getTransformedRoutes(config);
assert.equal(transformed.error, null);
const routes = transformed.routes;

function destination(url) {
    const request = new URL(url, 'https://wedding.example');
    const route = routes.find(route => route.destination && new RegExp(route.src).test(request.pathname));
    return route?.destination;
}

function cacheControl(url) {
    const request = new URL(url, 'https://wedding.example');
    const matches = condition => {
        assert.equal(condition.type, 'query');
        const value = request.searchParams.get(condition.key);
        return value !== null && (condition.value === undefined || new RegExp('^(?:' + condition.value + ')$').test(value));
    };
    const route = routes.find(route => route.headers && new RegExp(route.src).test(request.pathname) &&
        (route.has || []).every(matches) && (route.missing || []).every(condition => !matches(condition)));
    return route?.headers['Cache-Control'];
}

function filesIn(directory) {
    return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry =>
        entry.isDirectory() ? filesIn(path.join(directory, entry.name)) : [path.join(directory, entry.name)]);
}

test('Vercel resolves a static assets service and a container service without configuration errors', async () => {
    const result = await resolveAllConfiguredServicesV2(config.services, new LocalFileSystemDetector(root));
    assert.deepEqual(result.errors, []);
    const assets = result.services.find(service => service.name === 'assets');
    const site = result.services.find(service => service.name === 'site');
    assert.equal(assets.builder.use, '@vercel/static');
    assert.equal(path.resolve(root, assets.root), path.join(root, 'wwwroot'));
    assert.equal(site.builder.use, '@vercel/container');
    assert.ok(fs.existsSync(path.resolve(root, site.root, site.entrypoint)));
    for (const key of ['framework', 'functions', 'buildCommand', 'installCommand', 'devCommand', 'ignoreCommand', 'outputDirectory']) {
        assert.equal(config[key], undefined, key + ' must belong to a service rather than the top-level configuration');
    }
});

test('every public asset keeps its original path and bypasses the application container', () => {
    const webRoot = path.join(root, 'wwwroot');
    const files = filesIn(webRoot).map(file => '/' + path.relative(webRoot, file).split(path.sep).join('/'))
        .filter(url => /^\/(?:css|js|images|lib)\//.test(url) || url === '/favicon.ico');
    assert.ok(files.length > 20, 'verify the actual asset tree, not only a few sample filenames');
    for (const url of files) {
        const target = destination(url + '?v=content-hash');
        assert.equal(target.service, 'assets', url);
        assert.equal(target.path, undefined, url + ' must not be renamed or have its prefix stripped');
    }
});

test('public pages, admin, health, RSVP and upload endpoints keep their container routes and private cache behavior', () => {
    for (const url of ['/', '/Information', '/Information/Calendar', '/Faq', '/Home/Error', '/health', '/health/live',
        '/Admin/Login', '/Admin/Guests/Save', '/Admin/Photos/Settings', '/Rsvp', '/Rsvp/Select/7', '/Rsvp/EditStatus',
        '/Rsvp/Submit', '/Rsvp/Receipt', '/Photos', '/Photos/BeginUpload', '/Photos/UploadChunk', '/.env', '/appsettings.json']) {
        assert.equal(destination(url).service, 'site', url);
        assert.equal(destination(url).path, undefined, url);
        assert.equal(cacheControl(url), undefined, url + ' must not acquire public asset cache headers');
        assert.equal(cacheControl(url + '?v=anything'), undefined, url + ' must stay private even with a version query');
    }
});

test('versioned assets can be immutable while ordinary images and scripts can refresh', () => {
    for (const url of ['/images/navbar.png', '/images/home/carousel-01.jpeg-thumb.webp', '/js/site.js', '/css/site.css', '/lib/jquery/dist/jquery.min.js']) {
        assert.equal(cacheControl(url), 'public,max-age=3600');
        assert.equal(cacheControl(url + '?v=actual-content-hash'), 'public,max-age=31536000,immutable');
    }
    assert.equal(cacheControl('/favicon.ico'), 'public,max-age=3600');
});
