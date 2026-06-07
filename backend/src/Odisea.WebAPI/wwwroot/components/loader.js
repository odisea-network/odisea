// Odisea loader v2 — resolves the versioned component bundle.
// Usage:
//   <script src="https://…/components/loader.js" data-version="1"></script>
(function () {
  if (window.__odisea_loaded__) return;
  window.__odisea_loaded__ = true;

  var current = document.currentScript;
  var ver = current ? current.getAttribute('data-version') : null;

  if (!ver) {
    console.warn(
      '[Odisea] loader: data-version attribute missing — defaulting to v1. ' +
      'pin your embed: <script src="…/loader.js" data-version="1"><\/script>',
    );
    ver = '1';
  }

  // Resolve relative to loader's own URL so any origin works.
  var base = current
    ? current.src.replace(/\/[^/]+$/, '/')
    : '/components/';

  var s = document.createElement('script');
  s.src = base + 'v' + ver + '/odisea-components.umd.js';
  s.async = false;
  document.head.appendChild(s);
})();
