import type { ChatMessage, ChannelOption } from './chat';
import type { AiActivity, AiSuggestion } from './ai';

/**
 * Discriminated union of all possible SSE event payloads.
 * System events are identified by the `type` string literal;
 * chat messages fall through to ChatMessage (which has no `type` field).
 */
export type SseEvent =
  | { type: 'connected' }
  | { type: 'ping' }
  | { type: 'reset' }
  | { type: 'ui-language-changed'; language: 'zh' | 'en' }
  | { type: 'player-info'; name: string; world: string }
  | { type: 'channels'; channels: ChannelOption[] }
  | { type: 'active-channel'; prefix: string }
  | { type: 'ai-suggestion'; suggestion: AiSuggestion }
  | { type: 'ai-suggestion-removed'; id: string }
  | { type: 'ai-suggestion-sent'; id: string; text: string }
  | { type: 'ai-activity'; entry: AiActivity }
  | { type: 'ai-settings-changed' }
  | (ChatMessage & { type?: never });
