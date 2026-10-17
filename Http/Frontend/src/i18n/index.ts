import { en, type TranslationKey } from './en';
import { zh } from './zh';
import { useUiStore, type UiLanguage } from '../store/uiStore';

export type TranslationParams = Record<string, string | number>;

const DICTIONARIES: Record<UiLanguage, Record<TranslationKey, string>> = { en, zh };

export function translate(
  language: UiLanguage,
  key: TranslationKey,
  params?: TranslationParams,
): string {
  const dictionary = DICTIONARIES[language] ?? en;
  let value: string = dictionary[key] ?? en[key] ?? key;
  if (params) {
    for (const [name, replacement] of Object.entries(params)) {
      value = value.split(`{${name}}`).join(String(replacement));
    }
  }
  return value;
}

/** Reactive translation hook — re-renders when the shared language changes. */
export function useTranslation() {
  const language = useUiStore((s) => s.language);
  return {
    language,
    t: (key: TranslationKey, params?: TranslationParams) => translate(language, key, params),
  };
}

/** Non-reactive translation for event handlers and utilities. */
export function tStatic(key: TranslationKey, params?: TranslationParams): string {
  return translate(useUiStore.getState().language, key, params);
}

export { en, zh };
export type { TranslationKey };
