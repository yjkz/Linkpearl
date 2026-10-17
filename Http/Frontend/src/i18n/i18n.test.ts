import { describe, expect, it } from 'vitest';
import { en, translate, zh } from './index';

describe('i18n', () => {
  it('provides every English key in Chinese', () => {
    expect(Object.keys(zh).sort()).toEqual(Object.keys(en).sort());
  });

  it('interpolates named parameters', () => {
    expect(translate('en', 'messages.newMany', { count: 3 })).toBe('3 new messages');
    expect(translate('zh', 'messages.newMany', { count: 3 })).toBe('3 条新消息');
    expect(translate('en', 'sidebar.addFilterTo', { name: 'Raid' })).toBe('Add filter to Raid');
  });

  it('falls back to the English dictionary for unknown keys', () => {
    // @ts-expect-error deliberately invalid key
    expect(translate('zh', 'does.not.exist')).toBe('does.not.exist');
  });
});
