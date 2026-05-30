// Tiny loader: agencies drop this into their page to register Odisea web components.
// Usage:
//   <script src="https://odisea.example/components/loader.js"></script>
//   <tp-offer-list collection="summer-greece" api-base="https://odisea.example"></tp-offer-list>
(function () {
  if (window.__odisea_loaded__) return;
  window.__odisea_loaded__ = true;
  var s = document.createElement('script');
  // Resolve relative to this loader's own <script src=...> so a single origin works.
  var current = document.currentScript;
  var base = current ? current.src.replace(/loader\.js.*$/, '') : '/components/';
  s.src = base + 'odisea-components.umd.js';
  s.async = false;
  document.head.appendChild(s);
})();
