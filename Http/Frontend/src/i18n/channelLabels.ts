import { getChannelInfo } from '../lib/channelUtils';
import { PREFIX_TO_CHAT_TYPE } from '../constants/channels';
import { translate, type TranslationKey } from './index';
import { useUiStore, type UiLanguage } from '../store/uiStore';

/**
 * Chat-type → translation key map. Types without an entry fall back to the
 * canonical English label from CHANNEL_MAP.
 */
const CHANNEL_KEYS: Record<number, TranslationKey> = {
  1: 'channel.debug',
  2: 'channel.error',
  10: 'channel.say',
  11: 'channel.shout',
  12: 'channel.tellOut',
  13: 'channel.tellIn',
  14: 'channel.party',
  15: 'channel.alliance',
  16: 'channel.ls1',
  17: 'channel.ls2',
  18: 'channel.ls3',
  19: 'channel.ls4',
  20: 'channel.ls5',
  21: 'channel.ls6',
  22: 'channel.ls7',
  23: 'channel.ls8',
  24: 'channel.freeCompany',
  27: 'channel.noviceNetwork',
  28: 'channel.emote',
  29: 'channel.emoteStandard',
  30: 'channel.yell',
  32: 'channel.crossParty',
  36: 'channel.pvpTeam',
  37: 'channel.cwls1',
  101: 'channel.cwls2',
  102: 'channel.cwls3',
  103: 'channel.cwls4',
  104: 'channel.cwls5',
  105: 'channel.cwls6',
  106: 'channel.cwls7',
  107: 'channel.cwls8',
  41: 'channel.damage',
  42: 'channel.miss',
  43: 'channel.action',
  44: 'channel.item',
  45: 'channel.healing',
  46: 'channel.effect',
  47: 'channel.debuff',
  48: 'channel.buff',
  49: 'channel.debuff',
  55: 'channel.alarm',
  56: 'channel.echo',
  57: 'channel.system',
  58: 'channel.battle',
  59: 'channel.gather',
  60: 'channel.error',
  61: 'channel.system',
  62: 'channel.loot',
  64: 'channel.progress',
  65: 'channel.roll',
  66: 'channel.craft',
  67: 'channel.gather',
  69: 'channel.freeCompanyAnnouncement',
  70: 'channel.freeCompanyLogin',
  71: 'channel.market',
  72: 'channel.recruit',
  73: 'channel.sign',
  74: 'channel.dice',
  75: 'channel.noviceSystem',
  76: 'channel.music',
  77: 'channel.pvpAnnouncement',
  78: 'channel.pvpLogin',
  79: 'channel.book',
};

export function channelLabel(language: UiLanguage, type: number): string {
  const key = CHANNEL_KEYS[type];
  if (key) return translate(language, key);
  return getChannelInfo(type).label;
}

/** Localized label for a send-channel prefix ("/p " → "Party"/"小队"). */
export function channelLabelByPrefix(language: UiLanguage, prefix: string): string {
  const type = PREFIX_TO_CHAT_TYPE[prefix];
  return type !== undefined ? channelLabel(language, type) : prefix.trim();
}

/** Reactive channel-label helper for components. */
export function useChannelLabel() {
  const language = useUiStore((s) => s.language);
  return (type: number) => channelLabel(language, type);
}

/** Reactive channel-label helper keyed by send prefix. */
export function useChannelLabelByPrefix() {
  const language = useUiStore((s) => s.language);
  return (prefix: string) => channelLabelByPrefix(language, prefix);
}
