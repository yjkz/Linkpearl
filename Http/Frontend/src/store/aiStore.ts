import { create } from 'zustand';
import { RELAY_ADDR } from '../constants/config';
import type {
  AiActivity,
  AiChatEntry,
  AiChatTurn,
  AiSettings,
  AiSettingsUpdate,
  AiSuggestion,
  AiTestResult,
} from '../types/ai';

async function readError(res: Response): Promise<string> {
  try {
    const text = await res.text();
    if (!text) return `HTTP ${res.status}`;
    try {
      const data = JSON.parse(text) as { error?: string };
      return data.error ?? text;
    } catch {
      return text;
    }
  } catch {
    return `HTTP ${res.status}`;
  }
}

interface AiState {
  settings: AiSettings | null;
  settingsLoaded: boolean;
  suggestions: AiSuggestion[];
  activity: AiActivity[];
  chatTurns: AiChatEntry[];
  chatSending: boolean;
  chatError: string | null;
  loading: boolean;
  saving: boolean;
  error: string | null;

  loadSettings: () => Promise<void>;
  saveSettings: (update: AiSettingsUpdate) => Promise<boolean>;
  loadSuggestions: () => Promise<void>;
  loadActivity: () => Promise<void>;
  sendSuggestion: (id: string, index?: number, text?: string) => Promise<boolean>;
  dismissSuggestion: (id: string) => Promise<void>;
  sendChat: (message: string, contextScopes: string[], agentId?: string) => Promise<void>;
  clearChat: () => void;
  testConnection: () => Promise<AiTestResult>;
  upsertSuggestion: (suggestion: AiSuggestion) => void;
  removeSuggestion: (id: string) => void;
  addActivity: (entry: AiActivity) => void;
  clearError: () => void;
}

const ACTIVITY_LIMIT = 50;

export const useAiStore = create<AiState>((set, get) => ({
  settings: null,
  settingsLoaded: false,
  suggestions: [],
  activity: [],
  chatTurns: [],
  chatSending: false,
  chatError: null,
  loading: false,
  saving: false,
  error: null,

  loadSettings: async () => {
    set({ loading: true, error: null });
    try {
      const res = await fetch(`${RELAY_ADDR}/ai/settings`, { credentials: 'include' });
      if (!res.ok) throw new Error(await readError(res));
      const settings = (await res.json()) as AiSettings;
      set({ settings, settingsLoaded: true });
    } catch (e) {
      set({ error: e instanceof Error ? e.message : String(e) });
    } finally {
      set({ loading: false });
    }
  },

  saveSettings: async (update) => {
    set({ saving: true, error: null });
    try {
      const res = await fetch(`${RELAY_ADDR}/ai/settings`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(update),
      });
      if (!res.ok) throw new Error(await readError(res));
      await get().loadSettings();
      return true;
    } catch (e) {
      set({ error: e instanceof Error ? e.message : String(e) });
      return false;
    } finally {
      set({ saving: false });
    }
  },

  loadSuggestions: async () => {
    try {
      const res = await fetch(`${RELAY_ADDR}/ai/suggestions`, { credentials: 'include' });
      if (!res.ok) return;
      const data = (await res.json()) as { suggestions?: AiSuggestion[] };
      set({ suggestions: data.suggestions ?? [] });
    } catch {
      /* ignore transient failures */
    }
  },

  loadActivity: async () => {
    try {
      const res = await fetch(`${RELAY_ADDR}/ai/activity`, { credentials: 'include' });
      if (!res.ok) return;
      const data = (await res.json()) as { activity?: AiActivity[] };
      set({ activity: data.activity ?? [] });
    } catch {
      /* ignore transient failures */
    }
  },

  sendSuggestion: async (id, index, text) => {
    set({ error: null });
    try {
      const res = await fetch(`${RELAY_ADDR}/ai/suggestions/send`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id, index, text }),
      });
      if (!res.ok) throw new Error(await readError(res));
      get().removeSuggestion(id);
      return true;
    } catch (e) {
      set({ error: e instanceof Error ? e.message : String(e) });
      return false;
    }
  },

  dismissSuggestion: async (id) => {
    try {
      const res = await fetch(`${RELAY_ADDR}/ai/suggestions/dismiss`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id }),
      });
      if (res.ok) get().removeSuggestion(id);
    } catch {
      /* ignore */
    }
  },

  sendChat: async (message, contextScopes, agentId) => {
    const trimmed = message.trim();
    if (!trimmed) return;

    const conversation: AiChatTurn[] = get().chatTurns.map(({ role, content }) => ({
      role,
      content,
    }));
    const userEntry: AiChatEntry = {
      id: `${Date.now()}-user`,
      role: 'user',
      content: trimmed,
    };

    set((state) => ({
      chatTurns: [...state.chatTurns, userEntry],
      chatSending: true,
      chatError: null,
    }));

    try {
      const res = await fetch(`${RELAY_ADDR}/ai/chat`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          agentId,
          message: trimmed,
          contextScopes,
          conversation,
        }),
      });
      if (!res.ok) throw new Error(await readError(res));
      const data = (await res.json()) as { reply?: string };
      const reply = data.reply?.trim();
      if (!reply) throw new Error('The model returned an empty reply.');

      set((state) => ({
        chatTurns: [
          ...state.chatTurns,
          { id: `${Date.now()}-assistant`, role: 'assistant', content: reply },
        ],
      }));
    } catch (e) {
      set({ chatError: e instanceof Error ? e.message : String(e) });
    } finally {
      set({ chatSending: false });
    }
  },

  clearChat: () => set({ chatTurns: [], chatError: null }),

  testConnection: async () => {
    try {
      const res = await fetch(`${RELAY_ADDR}/ai/test`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: '{}',
      });
      const data = (await res.json()) as AiTestResult;
      if (!res.ok) return { ok: false, error: data.error ?? `HTTP ${res.status}` };
      return data;
    } catch (e) {
      return { ok: false, error: e instanceof Error ? e.message : String(e) };
    }
  },

  upsertSuggestion: (suggestion) =>
    set((state) => ({
      suggestions: [suggestion, ...state.suggestions.filter((s) => s.id !== suggestion.id)],
    })),

  removeSuggestion: (id) =>
    set((state) => ({ suggestions: state.suggestions.filter((s) => s.id !== id) })),

  addActivity: (entry) =>
    set((state) => ({ activity: [entry, ...state.activity].slice(0, ACTIVITY_LIMIT) })),

  clearError: () => set({ error: null }),
}));
