export type AiReplyMode = 'auto' | 'options' | 'suggestion';

export interface AiAgent {
  id: string;
  name: string;
  systemPrompt: string;
  model: string;
  temperature: number;
  maxTokens: number;
}

export interface AiRule {
  id: string;
  name: string;
  enabled: boolean;
  agentId: string;
  mode: AiReplyMode;
  channelTypes: number[];
  cooldownSeconds: number;
  maxPerMinute: number;
  optionsCount: number;
}

export interface AiSuggestion {
  id: string;
  ruleId: string;
  ruleName: string;
  agentName: string;
  mode: AiReplyMode;
  channelType: number;
  channelLabel: string;
  senderName: string;
  senderWorld: string;
  sourceText: string;
  candidates: string[];
  createdAt: number;
}

export interface AiActivity {
  timestamp: number;
  kind: 'info' | 'auto-reply' | 'error' | string;
  message: string;
}

export interface AiSettings {
  enabled: boolean;
  apiKeySet: boolean;
  apiBaseUrl: string;
  defaultModel: string;
  contextMessageCount: number;
  timeoutSeconds: number;
  agents: AiAgent[];
  rules: AiRule[];
}

export interface AiSettingsUpdate {
  enabled: boolean;
  apiBaseUrl: string;
  defaultModel: string;
  contextMessageCount: number;
  timeoutSeconds: number;
  agents: AiAgent[];
  rules: AiRule[];
  /** Omit or leave empty to keep the stored key. */
  apiKey?: string;
  clearApiKey?: boolean;
}

export interface AiTestResult {
  ok: boolean;
  latencyMs?: number;
  model?: string;
  error?: string;
}

export interface AiChatTurn {
  role: 'user' | 'assistant';
  content: string;
}

export interface AiChatEntry extends AiChatTurn {
  id: string;
}

export interface AiChatRequest {
  agentId?: string;
  message: string;
  contextScopes: string[];
  conversation: AiChatTurn[];
}

export interface AiChatResponse {
  reply?: string;
  /** Number of game chat messages that were attached to the request. */
  contextCount?: number;
}
