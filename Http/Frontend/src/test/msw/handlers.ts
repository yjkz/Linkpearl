import { http, HttpResponse, passthrough } from 'msw';
import {
  MOCK_PLAYER,
  MOCK_CHANNELS,
  MOCK_SETTINGS,
  MOCK_FILTER_MODE,
  MOCK_MESSAGES,
  MOCK_CHARACTERS,
  MOCK_PLAYER_AVATAR_URL,
  MOCK_AI_SETTINGS,
  MOCK_AI_SUGGESTIONS,
  MOCK_AI_ACTIVITY,
  getAvatarForName,
} from '../../mock/data';

const MOCK_CHARACTER_NAMES = new Set([
  MOCK_PLAYER.name,
  'Baldur Grimstone',
  'Finn Ashwood',
  'Caelum Arke',
  'Thalise Maron',
  'Nael Orenthal',
]);

// Settings live in-process so PUT updates round-trip back through GET.
let mockSettings: Record<string, unknown> = { ...MOCK_SETTINGS };
let mockLanguage: 'en' | 'zh' = 'en';
let mockAiSettings = { ...MOCK_AI_SETTINGS };

export const handlers = [
  // ── UI language ──────────────────────────────────────────────────
  http.get('/language', () => HttpResponse.json({ language: mockLanguage })),
  http.put('/language', async ({ request }) => {
    try {
      const body = (await request.json()) as { language?: string };
      if (body.language === 'zh' || body.language === 'en') mockLanguage = body.language;
    } catch {
      /* ignore malformed bodies */
    }
    return HttpResponse.json({ ok: true, language: mockLanguage });
  }),

  // ── AI assistant ─────────────────────────────────────────────────
  http.get('/ai/settings', () => HttpResponse.json(mockAiSettings)),
  http.put('/ai/settings', async ({ request }) => {
    try {
      mockAiSettings = (await request.json()) as typeof mockAiSettings;
    } catch {
      /* ignore malformed bodies */
    }
    return HttpResponse.json({ ok: true });
  }),
  http.get('/ai/suggestions', () => HttpResponse.json({ suggestions: MOCK_AI_SUGGESTIONS })),
  http.get('/ai/activity', () => HttpResponse.json({ activity: MOCK_AI_ACTIVITY })),
  http.post('/ai/suggestions/send', () => HttpResponse.json({ ok: true })),
  http.post('/ai/suggestions/dismiss', () => HttpResponse.json({ ok: true })),
  http.post('/ai/chat', async ({ request }) => {
    const body = (await request.json()) as { message?: string; contextScopes?: string[] };
    const context = body.contextScopes?.length
      ? ` with context from ${body.contextScopes.join(', ')}`
      : '';
    return HttpResponse.json({
      reply: `Demo assistant: I received "${body.message ?? ''}"${context}. Ask me to summarize the selected chat, draft a reply, or explain a game mechanic.`,
    });
  }),
  http.post('/ai/test', () =>
    HttpResponse.json({ ok: true, latencyMs: 87, model: 'gpt-4o-mini' }),
  ),

  // ── Settings ────────────────────────────────────────────────────
  http.get('/settings', () => HttpResponse.json(mockSettings)),
  http.put('/settings', async ({ request }) => {
    try {
      mockSettings = (await request.json()) as Record<string, unknown>;
    } catch {
      /* ignore malformed bodies */
    }
    return HttpResponse.json({ ok: true });
  }),

  // ── Filter mode ─────────────────────────────────────────────────
  http.get('/settings/filter-mode', () => HttpResponse.json(MOCK_FILTER_MODE)),
  http.post('/settings/filter-mode', () => HttpResponse.json({ ok: true })),
  http.delete('/settings/filter-mode', () => HttpResponse.json({ ok: true })),

  // ── Characters ──────────────────────────────────────────────────
  http.get('/settings/characters', () => HttpResponse.json(MOCK_CHARACTERS)),
  http.delete('/settings/characters', () => HttpResponse.json({ ok: true })),

  // ── History ─────────────────────────────────────────────────────
  http.get('/history', ({ request }) => {
    const url = new URL(request.url);
    const beforeParam = url.searchParams.get('before');
    const before = beforeParam !== null ? Number(beforeParam) : null;
    const msgs =
      before !== null ? MOCK_MESSAGES.filter((m) => m.Timestamp < before) : MOCK_MESSAGES;
    return HttpResponse.json(msgs);
  }),

  // ── Channels ────────────────────────────────────────────────────
  http.get('/channels', () => HttpResponse.json(MOCK_CHANNELS)),

  // ── Avatar (whitelist only — unknown names pass through to the real server) ──
  http.get('/avatar', ({ request }) => {
    const url = new URL(request.url);
    const name = url.searchParams.get('name') ?? '';
    if (MOCK_CHARACTER_NAMES.has(name)) {
      const avatarUrl = name === MOCK_PLAYER.name ? MOCK_PLAYER_AVATAR_URL : getAvatarForName(name);
      return HttpResponse.json({ avatarUrl });
    }
    return passthrough();
  }),

  // ── Send (no-op in mock) ────────────────────────────────────────
  http.post('/send', () => HttpResponse.json('OK')),

  // ── SSE (kept open forever so the client stays in "connecting" without errors) ──
  http.get('/sse', () => new HttpResponse(null, { status: 200 })),
];
