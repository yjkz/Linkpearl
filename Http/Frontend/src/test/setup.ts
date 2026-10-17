import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './msw/server';
import { useUiStore } from '../store/uiStore';

// jsdom lacks APIs rich-textarea relies on.
if (!('ResizeObserver' in globalThis)) {
  (globalThis as { ResizeObserver: unknown }).ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}
if (typeof document !== 'undefined' && !document.elementFromPoint) {
  document.elementFromPoint = () => null;
}

// Lifecycle: start MSW for all tests, reset handlers between tests, close after.
// Tests assert against the English dictionary, so pin the UI language to English.
beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
  useUiStore.getState().applyServerLanguage('en');
});
afterEach(() => {
  server.resetHandlers();
  useUiStore.getState().applyServerLanguage('en');
});
afterAll(() => server.close());
