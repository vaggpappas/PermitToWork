/**
 * Global setup, run before every spec file.
 *
 * jsdom implements most of the DOM but not `window.matchMedia`, and SettingsService calls it
 * while working out whether the operating system prefers a light or dark theme. Because that
 * call happens in a field initialiser, *any* component that injects SettingsService — even
 * indirectly, through the shell — dies on construction with "matchMedia is not a function".
 *
 * The stub reports no match, so the theme resolves to the dark default. Deterministic on
 * purpose: a test that renders differently depending on the machine's colour scheme is worse
 * than no test at all.
 */
if (typeof window.matchMedia !== 'function') {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: (query: string): MediaQueryList =>
      ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        dispatchEvent: () => false,
      }) as MediaQueryList,
  });
}
