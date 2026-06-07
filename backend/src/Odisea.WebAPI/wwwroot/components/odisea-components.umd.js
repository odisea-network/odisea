// Legacy path — kept for embeds that pre-date runtime versioning (issue #28).
// Agencies that hard-coded /components/odisea-components.umd.js get v1 loaded for them.
// Suppress this warning by switching to: <script src="…/loader.js" data-version="1">
(function () {
  console.warn(
    '[Odisea] loading components from the unversioned path is deprecated. ' +
    'switch to <script src="…/loader.js" data-version="1"> to pin your embed to v1.',
  );
  var base = '';
  if (document.currentScript && document.currentScript.src) {
    base = document.currentScript.src.replace(/\/components\/.*$/, '');
  }
  var s = document.createElement('script');
  s.src = base + '/components/v1/odisea-components.umd.js';
  s.async = false;
  document.head.appendChild(s);
})();
