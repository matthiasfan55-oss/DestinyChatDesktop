/**
 * embeDGG WebView2 Polyfill
 *
 * Replaces Chrome extension APIs used by content.js so the extension works
 * inside DestinyChatDesktop's WebView2 (no extension context).
 *
 * APIs polyfilled:
 *   chrome.runtime.id / lastError / sendMessage / onMessage
 *   chrome.storage.sync.get / set / onChanged
 *
 * Cross-origin HTTP fetches (bgFetch, fetchTweet, oembed) are bridged to
 * C# via postMessage → window.chrome.webview.postMessage({type:'edgg-fetch',...})
 * C# replies with {type:'edgg-fetch-response', id, result:{ok,status,body,contentType,finalUrl}}
 */
(() => {
    if (window.__edggPolyfillLoaded) return;
    window.__edggPolyfillLoaded = true;

    // ─── Constants ───────────────────────────────────────────────────────────
    const EDGG_DEFAULTS = {
        enableTweets:   true,
        enableMedia:    true,
        enableYouTube:  true,
        enableTwitch:   true,
        enableKick:     true,
        enableInstagram:true,
        mediaWidth:     500,
        blurMedia:      false,
        mediaMaxHeight: 700
    };
    const STORAGE_PREFIX = 'codex-ext.edgg.';

    // ─── C# fetch bridge ─────────────────────────────────────────────────────
    let _fetchIdCounter = 0;
    const _fetchPending = {};

    const EDGG_FETCH_TIMEOUT_MS = 45000;

    function _edggFetch(url) {
        return new Promise((resolve, reject) => {
            const id = ++_fetchIdCounter;
            const timeoutId = window.setTimeout(function() {
                const pending = _fetchPending[id];
                if (!pending) return;
                delete _fetchPending[id];
                pending.reject(new Error('edgg-fetch timed out after ' + EDGG_FETCH_TIMEOUT_MS + 'ms'));
            }, EDGG_FETCH_TIMEOUT_MS);

            _fetchPending[id] = {
                resolve: function(result) {
                    window.clearTimeout(timeoutId);
                    resolve(result);
                },
                reject: function(err) {
                    window.clearTimeout(timeoutId);
                    reject(err);
                }
            };
            try {
                window.chrome.webview.postMessage({ type: 'edgg-fetch', id: id, url: url });
            } catch (err) {
                window.clearTimeout(timeoutId);
                delete _fetchPending[id];
                reject(err);
            }
        });
    }

    // Listen for fetch responses from C#
    try {
        window.chrome.webview.addEventListener('message', function(e) {
            let msg;
            try { msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data; } catch (_) { return; }
            if (!msg || msg.type !== 'edgg-fetch-response') return;
            const pending = _fetchPending[msg.id];
            if (!pending) return;
            delete _fetchPending[msg.id];
            pending.resolve(msg.result || { ok: false, error: 'no result' });
        });
    } catch (_) {}

    // ─── localStorage helpers ─────────────────────────────────────────────────
    function edggGetSetting(key) {
        try {
            const raw = localStorage.getItem(STORAGE_PREFIX + key);
            if (raw !== null) return JSON.parse(raw);
        } catch (_) {}
        return key in EDGG_DEFAULTS ? EDGG_DEFAULTS[key] : undefined;
    }

    function edggSetSetting(key, val) {
        try { localStorage.setItem(STORAGE_PREFIX + key, JSON.stringify(val)); } catch (_) {}
    }

    function edggGetAllSettings() {
        const result = {};
        Object.keys(EDGG_DEFAULTS).forEach(function(k) { result[k] = edggGetSetting(k); });
        return result;
    }

    function edggClampInt(v, min, max, fallback) {
        const n = Number(v);
        if (!Number.isFinite(n)) return fallback;
        return Math.min(max, Math.max(min, Math.round(n)));
    }

    function edggApplyMediaSizeVars() {
        try {
            const width = edggClampInt(edggGetSetting('mediaWidth'), 220, 800, 500);
            const height = edggClampInt(edggGetSetting('mediaMaxHeight'), 120, 1200, 700);
            const root = document.documentElement;
            if (!root) return;
            root.style.setProperty('--edgg-media-width', width + 'px');
            root.style.setProperty('--edgg-media-max-height', height + 'px');
        } catch (_) {}
    }

    edggApplyMediaSizeVars();

    // ─── Message handling ─────────────────────────────────────────────────────
    const _messageListeners  = [];
    const _storageListeners  = [];

    async function handleFetchTweet(rawUrl) {
        function extractTweetId(u) {
            try {
                const url = new URL(u);
                const m = url.pathname.match(/\/status\/(\d+)/) || url.pathname.match(/\/i\/web\/status\/(\d+)/);
                return m ? m[1] : null;
            } catch (_) { return null; }
        }

        const id = extractTweetId(rawUrl);
        const cdnById  = id ? ('https://cdn.syndication.twimg.com/widgets/tweet?id=' + encodeURIComponent(id) + '&dnt=true') : null;
        const cdnByUrl = 'https://cdn.syndication.twimg.com/widgets/tweet?url=' + encodeURIComponent(rawUrl) + '&dnt=true';
        const oembedUrl = 'https://publish.twitter.com/oembed?omit_script=1&hide_thread=1&align=left&dnt=true&url=' + encodeURIComponent(rawUrl);

        async function enrichFromFxVx(tid) {
            const out = { photos: [], videos: [], text: '' };
            if (!tid) return out;
            const tryUrls = [
                'https://fxtwitter.com/i/status/' + encodeURIComponent(tid) + '.json',
                'https://api.vxtwitter.com/Twitter/status/' + encodeURIComponent(tid),
                'https://vxtwitter.com/i/status/' + encodeURIComponent(tid) + '.json'
            ];
            for (const u of tryUrls) {
                try {
                    const resp = await _edggFetch(u);
                    if (!resp.ok) continue;
                    const j = JSON.parse(resp.body);
                    try {
                        const t = (typeof j.text === 'string' && j.text)
                            || (j.tweet && typeof j.tweet.full_text === 'string' && j.tweet.full_text)
                            || (j.tweet && typeof j.tweet.text === 'string' && j.tweet.text)
                            || '';
                        if (t && !out.text) out.text = String(t);
                    } catch (_) {}
                    const scan = function(obj) {
                        const photos = [], videos = [];
                        const walk = function(v) {
                            if (!v) return;
                            if (typeof v === 'string') {
                                if (/^https?:\/\/pbs\.twimg\.com\/media\//.test(v) && /\.(?:jpg|jpeg|png|gif|webp)(?:$|\?)/i.test(v)) photos.push(v);
                                if (/^https?:\/\/video\.twimg\.com\//.test(v) && /\.mp4(?:$|\?)/i.test(v)) videos.push({ url: v, type: 'video/mp4' });
                                return;
                            }
                            if (Array.isArray(v)) { v.forEach(walk); return; }
                            if (typeof v === 'object') { for (const k in v) walk(v[k]); }
                        };
                        walk(obj);
                        return { photos: photos, videos: videos };
                    };
                    const collected = scan(j);
                    if (collected.photos.length || collected.videos.length) {
                        const seenP = new Set(), seenV = new Set();
                        out.photos.push(...collected.photos.filter(function(p) { return !seenP.has(p) && seenP.add(p); }));
                        out.videos.push(...collected.videos.filter(function(v) {
                            if (seenV.has(v.url)) return false;
                            seenV.add(v.url);
                            return true;
                        }));
                        break;
                    }
                } catch (_) {}
            }
            return out;
        }

        try {
            const first = cdnById || cdnByUrl;
            let r = await _edggFetch(first);
            if (!r.ok && first !== cdnByUrl) r = await _edggFetch(cdnByUrl);

            if (r.ok) {
                const data = JSON.parse(r.body);
                const normalized = {
                    user: data.user || null,
                    author_name: data.author_name || (data.user && (data.user.name || data.user.screen_name)) || '',
                    text: data.text || data.full_text || data.description || '',
                    created_at: data.created_at || data.date || '',
                    entities: data.entities || null,
                    photos: Array.isArray(data.photos)
                        ? data.photos
                        : (data.entities && Array.isArray(data.entities.media) ? data.entities.media : [])
                };
                try {
                    const extra = await enrichFromFxVx(id);
                    if (extra && (extra.photos.length || extra.videos.length)) {
                        const existingUrls = new Set();
                        const photoObjs = Array.isArray(normalized.photos) ? normalized.photos.slice() : [];
                        photoObjs.forEach(function(p) {
                            if (!p) return;
                            const pu = typeof p === 'string' ? p : (p.url || p.media_url_https || p.media_url);
                            if (pu) existingUrls.add(pu);
                        });
                        extra.photos.forEach(function(pu) {
                            if (!existingUrls.has(pu)) photoObjs.push({ url: pu });
                        });
                        normalized.photos = photoObjs;
                        normalized.videos = extra.videos;
                        if ((!normalized.text || !String(normalized.text).trim()) && extra.text) normalized.text = extra.text;
                    }
                } catch (_) {}
                try {
                    if (!normalized.text || !String(normalized.text).trim()) {
                        const oe2 = await _edggFetch(oembedUrl);
                        if (oe2.ok) {
                            const od2 = JSON.parse(oe2.body);
                            if (od2 && od2.html) normalized.oembed_html = od2.html;
                        }
                    }
                } catch (_) {}
                return { ok: true, source: 'cdn', data: Object.assign({}, data, normalized) };
            }

            // CDN failed — try fxvx
            try {
                const extra = await enrichFromFxVx(id);
                if (extra && (extra.photos.length || extra.videos.length)) {
                    return {
                        ok: true, source: 'vx',
                        data: { user: null, author_name: '', text: extra.text || '', created_at: '', entities: null,
                            photos: extra.photos.map(function(u) { return { url: u }; }),
                            videos: extra.videos }
                    };
                }
            } catch (_) {}

            // Final fallback — oembed HTML
            const oe = await _edggFetch(oembedUrl);
            if (!oe.ok) return { ok: false, error: 'Error: HTTP ' + (oe.status || 0) };
            const odata = JSON.parse(oe.body);
            return { ok: true, source: 'oembed', data: { html: odata.html || '', author_name: odata.author_name || '', url: rawUrl } };

        } catch (err) {
            return { ok: false, error: String(err) };
        }
    }

    async function handleEdggMessage(msg) {
        if (!msg || !msg.type) return null;
        switch (msg.type) {
            case 'getSettings':
                return { ok: true, settings: Object.assign({}, EDGG_DEFAULTS, edggGetAllSettings()) };

            case 'setSettings': {
                const s = msg.settings || {};
                const changes = {};
                Object.keys(s).forEach(function(k) {
                    const oldVal = edggGetSetting(k);
                    edggSetSetting(k, s[k]);
                    changes[k] = { oldValue: oldVal, newValue: s[k] };
                });
                edggApplyMediaSizeVars();
                _storageListeners.forEach(function(fn) { try { fn(changes, 'sync'); } catch (_) {} });
                _messageListeners.forEach(function(fn) { try { fn({ type: 'settingsUpdated' }); } catch (_) {} });
                return { ok: true };
            }

            case 'bgFetch':
                try { return await _edggFetch(msg.url); }
                catch (e) { return { ok: false, error: String(e) }; }

            case 'oembed':
                if (msg.provider !== 'youtube') return { ok: false, error: 'invalid_provider' };
                try {
                    const oUrl = 'https://www.youtube.com/oembed?url=' + encodeURIComponent(msg.videoUrl) + '&format=json';
                    const r = await _edggFetch(oUrl);
                    if (!r.ok) return { ok: false, status: r.status };
                    return { ok: true, data: JSON.parse(r.body) };
                } catch (e) { return { ok: false, error: String(e) }; }

            case 'fetchTweet':
                return await handleFetchTweet(String(msg.url || ''));

            default:
                return null;
        }
    }

    // ─── Chrome API polyfill ──────────────────────────────────────────────────
    window.chrome = window.chrome || {};

    window.chrome.runtime = window.chrome.runtime || {};
    window.chrome.runtime.id = 'edgg-desktop-polyfill';
    window.chrome.runtime.lastError = null;

    window.chrome.runtime.sendMessage = function(msg, cb) {
        handleEdggMessage(msg).then(function(result) {
            try { if (cb) cb(result); } catch (_) {}
        }).catch(function() {
            try { if (cb) cb(null); } catch (_) {}
        });
    };

    window.chrome.runtime.onMessage = {
        addListener: function(fn) { _messageListeners.push(fn); }
    };

    // storage.sync
    window.chrome.storage = window.chrome.storage || {};
    window.chrome.storage.sync = {
        get: function(keys, cb) {
            const result = {};
            let allKeys;
            if (keys === null || keys === undefined) {
                allKeys = Object.keys(EDGG_DEFAULTS);
            } else if (Array.isArray(keys)) {
                allKeys = keys;
            } else if (typeof keys === 'string') {
                allKeys = [keys];
            } else {
                allKeys = Object.keys(keys);
            }
            allKeys.forEach(function(k) { result[k] = edggGetSetting(k); });
            try { if (cb) cb(result); } catch (_) {}
        },
        set: function(obj, cb) {
            const changes = {};
            Object.keys(obj).forEach(function(k) {
                const oldVal = edggGetSetting(k);
                edggSetSetting(k, obj[k]);
                changes[k] = { oldValue: oldVal, newValue: obj[k] };
            });
            edggApplyMediaSizeVars();
            _storageListeners.forEach(function(fn) { try { fn(changes, 'sync'); } catch (_) {} });
            try { if (cb) cb(); } catch (_) {}
        }
    };

    window.chrome.storage.onChanged = {
        addListener: function(fn) { _storageListeners.push(fn); }
    };

    // Silence calls to chrome.runtime.lastError inside callbacks
    const _origRuntimeGet = window.chrome.runtime;
    if (!window.chrome.runtime.lastError) {
        Object.defineProperty(window.chrome.runtime, 'lastError', {
            get: function() { return null; },
            configurable: true
        });
    }

})();
