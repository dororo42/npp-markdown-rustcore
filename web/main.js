// rustrender — main-thread message handling for the WebView2 preview shell.
//
// Contract with the C# host (route C experiment):
//   window.chrome.webview.postMessage('render;REQID;json')  → host pushes work
//   this worker posts results back; main thread notifies host via
//   window.chrome.webview.postMessage('renderDone;REQID;json')
//
// Markdown features rendered client-side on top of the core HTML:
//   - ```mermaid blocks  → mermaid.js diagrams (flag: enable_mermaid)
//   - $$…$$ / $…$        → KaTeX (flag: enable_katex)
//   - code blocks        → highlight.js when the core had no syntect

/* global Worker, mermaid, renderMathInElement, hljs */

const worker = new Worker('worker.js', { type: 'module' });
const pending = new Map();
let seq = 0;

function postToHost(msg) {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
        window.chrome.webview.postMessage(msg);
    } else {
        console.log('[rustrender]', msg);
    }
}

export function render(markdown, options) {
    const id = ++seq;
    return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        worker.postMessage({ id, markdown, options });
    });
}

worker.onmessage = (ev) => {
    const { id, ok, payload, error } = ev.data || {};
    const job = pending.get(id);
    if (!job) return;
    pending.delete(id);
    if (ok) {
        applyPayload(payload);
        job.resolve(payload);
        postToHost('renderDone;' + id + ';' + JSON.stringify(payload));
    } else {
        job.reject(new Error(error || 'render failed'));
    }
};

// ---------------------------------------------------------------- enhance

export function applyPayload(payload) {
    const root = document.getElementById('content');
    if (!root) return;
    root.innerHTML = payload.html_body;

    // Mermaid: turn language-mermaid code blocks into diagrams.
    if (window.mermaid && payload.html_body.includes('language-mermaid')) {
        document.querySelectorAll('pre > code.language-mermaid').forEach((el) => {
            const holder = document.createElement('div');
            holder.className = 'mermaid';
            holder.textContent = el.textContent;
            el.closest('pre').replaceWith(holder);
        });
        window.mermaid.run({ nodes: root.querySelectorAll('.mermaid') }).catch(() => {});
    }

    // KaTeX: math in $…$ / $$…$$ (comrak leaves them as plain text).
    if (window.renderMathInElement) {
        window.renderMathInElement(root, {
            delimiters: [
                { left: '$$', right: '$$', display: true },
                { left: '$', right: '$', display: false },
            ],
            throwOnError: false,
        });
    }

    // Code highlighting when the core had no syntect (WASM route).
    if (window.hljs) {
        root.querySelectorAll('pre > code[class^="language-"]').forEach((el) => {
            try { window.hljs.highlightElement(el); } catch (e) { /* noop */ }
        });
    }
}

// Host-initiated work loop (poll-free: host pushes via postMessage).
window.addEventListener('DOMContentLoaded', () => {
    postToHost('ready');
});

// Expose for the WebView2 host script.
window.rustrender = { render };
