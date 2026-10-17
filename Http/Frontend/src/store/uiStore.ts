import { create } from 'zustand';
import { RELAY_ADDR } from '../constants/config';

export type UiLanguage = 'zh' | 'en';

const STORAGE_KEY = 'linkpearl-ui-language';

function readCachedLanguage(): UiLanguage {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    if (value === 'zh' || value === 'en') return value;
  } catch {
    /* localStorage unavailable */
  }
  return 'zh';
}

function applyDocumentLang(language: UiLanguage): void {
  try {
    document.documentElement.lang = language === 'zh' ? 'zh-CN' : 'en';
  } catch {
    /* non-browser environment */
  }
}

function cacheLanguage(language: UiLanguage): void {
  try {
    localStorage.setItem(STORAGE_KEY, language);
  } catch {
    /* localStorage unavailable */
  }
}

interface UiState {
  language: UiLanguage;
  /** User-initiated change: update locally, cache, and persist to the plugin config. */
  setLanguage: (language: UiLanguage) => void;
  /** Server/SSE-initiated change: update locally and cache without writing back. */
  applyServerLanguage: (language: UiLanguage) => void;
  /** Pull the shared language from the plugin after authentication. */
  syncFromServer: () => Promise<void>;
}

export const useUiStore = create<UiState>((set) => ({
  language: readCachedLanguage(),

  setLanguage: (language) => {
    set({ language });
    applyDocumentLang(language);
    cacheLanguage(language);
    void fetch(`${RELAY_ADDR}/language`, {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ language }),
    }).catch(() => {
      /* plugin unreachable — keep the local choice */
    });
  },

  applyServerLanguage: (language) => {
    set({ language });
    applyDocumentLang(language);
    cacheLanguage(language);
  },

  syncFromServer: async () => {
    try {
      const res = await fetch(`${RELAY_ADDR}/language`, { credentials: 'include' });
      if (!res.ok) return;
      const data = (await res.json()) as { language?: string };
      if (data.language === 'zh' || data.language === 'en') {
        useUiStore.getState().applyServerLanguage(data.language);
      }
    } catch {
      /* plugin not reachable yet — keep the cached/default language */
    }
  },
}));

applyDocumentLang(useUiStore.getState().language);
