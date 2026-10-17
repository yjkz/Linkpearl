import { useEffect } from 'react';
import { useAiStore } from '../store/aiStore';

/**
 * Loads the AI state when a panel/settings view is opened.
 * Live updates continue to arrive through SSE while the panel is mounted anyway.
 */
export function useAiDataLoader(enabled = true): void {
  const loadSettings = useAiStore((s) => s.loadSettings);
  const loadSuggestions = useAiStore((s) => s.loadSuggestions);
  const loadActivity = useAiStore((s) => s.loadActivity);

  useEffect(() => {
    if (!enabled) return;
    void loadSettings();
    void loadSuggestions();
    void loadActivity();
  }, [enabled, loadSettings, loadSuggestions, loadActivity]);
}
