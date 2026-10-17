import type { TranslationKey } from '../i18n';

export interface AiContextScope {
  key: string;
  labelKey: TranslationKey;
}

/** Message categories for the manual AI conversation context. */
export const AI_CONTEXT_CATEGORIES: AiContextScope[] = [
  { key: 'all', labelKey: 'ai.chat.scope.all' },
  { key: 'battle', labelKey: 'ai.chat.scope.battle' },
  { key: 'system', labelKey: 'ai.chat.scope.system' },
  { key: 'loot', labelKey: 'ai.chat.scope.loot' },
  { key: 'npc', labelKey: 'ai.chat.scope.npc' },
  { key: 'craft', labelKey: 'ai.chat.scope.craft' },
];

/** Specific chat channels for the manual AI conversation context. */
export const AI_CONTEXT_CHANNELS: AiContextScope[] = [
  { key: 'say', labelKey: 'channel.say' },
  { key: 'party', labelKey: 'channel.party' },
  { key: 'alliance', labelKey: 'channel.alliance' },
  { key: 'yell', labelKey: 'channel.yell' },
  { key: 'shout', labelKey: 'channel.shout' },
  { key: 'freeCompany', labelKey: 'channel.freeCompany' },
  { key: 'noviceNetwork', labelKey: 'channel.noviceNetwork' },
  { key: 'tell', labelKey: 'channel.tell' },
  { key: 'ls1', labelKey: 'channel.ls1' },
  { key: 'ls2', labelKey: 'channel.ls2' },
  { key: 'ls3', labelKey: 'channel.ls3' },
  { key: 'ls4', labelKey: 'channel.ls4' },
  { key: 'ls5', labelKey: 'channel.ls5' },
  { key: 'ls6', labelKey: 'channel.ls6' },
  { key: 'ls7', labelKey: 'channel.ls7' },
  { key: 'ls8', labelKey: 'channel.ls8' },
  { key: 'cwls1', labelKey: 'channel.cwls1' },
  { key: 'cwls2', labelKey: 'channel.cwls2' },
  { key: 'cwls3', labelKey: 'channel.cwls3' },
  { key: 'cwls4', labelKey: 'channel.cwls4' },
  { key: 'cwls5', labelKey: 'channel.cwls5' },
  { key: 'cwls6', labelKey: 'channel.cwls6' },
  { key: 'cwls7', labelKey: 'channel.cwls7' },
  { key: 'cwls8', labelKey: 'channel.cwls8' },
];
