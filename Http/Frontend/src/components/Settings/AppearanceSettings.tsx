import clsx from 'clsx';
import type { ChatMessage } from '../../types/chat';
import styles from './AppearanceSettings.module.css';
import { FONTS } from '../../constants/config';
import { MessageItem } from '../ChatArea/MessageItem';
import { useSettingsStore } from '../../store/settingsStore';
import { useUiStore } from '../../store/uiStore';
import { useTranslation } from '../../i18n';

// Static preview messages — defined at module level to avoid purity lint warnings
const PREVIEW_BASE_TIME = Date.now();

const PREVIEW_SHOUT: ChatMessage = {
  Type: 11,
  SenderName: "Yoshi'p Sampo",
  SenderWorld: 'Aegis',
  MessagePayloads: [{ Type: 'text', Text: 'Hear... Feel... Think...' }],
  Timestamp: PREVIEW_BASE_TIME,
};

const PREVIEW_EMOTE: ChatMessage = {
  Type: 28,
  SenderName: "Yoshi'p Sampo",
  SenderWorld: 'Aegis',
  MessagePayloads: [
    {
      Type: 'text',
      Text: "Yoshi'p Sampo swipes at their tomestone and checks their messages.",
    },
  ],
  Timestamp: PREVIEW_BASE_TIME + 30000,
};

const NOOP = () => {};

export function AppearanceSettings() {
  const { t } = useTranslation();
  const language = useUiStore((s) => s.language);
  const setLanguage = useUiStore((s) => s.setLanguage);
  const theme = useSettingsStore((s) => s.theme);
  const setTheme = useSettingsStore((s) => s.setTheme);
  const fontFamily = useSettingsStore((s) => s.fontFamily);
  const fontSize = useSettingsStore((s) => s.fontSize);
  const italicizeSystem = useSettingsStore((s) => s.italicizeSystem);
  const useColoredBackground = useSettingsStore((s) => s.useColoredBackground);
  const largeLinkPreviews = useSettingsStore((s) => s.largeLinkPreviews);
  const setFontFamily = useSettingsStore((s) => s.setFontFamily);
  const setFontSize = useSettingsStore((s) => s.setFontSize);
  const setItalicizeSystem = useSettingsStore((s) => s.setItalicizeSystem);
  const setUseColoredBackground = useSettingsStore((s) => s.setUseColoredBackground);
  const setLargeLinkPreviews = useSettingsStore((s) => s.setLargeLinkPreviews);
  return (
    <>
      <div className="form-section">
        <div className="form-section-title">{t('appearance.language')}</div>
        <p>{t('appearance.languageDescription')}</p>
        <select
          className="form-select"
          value={language}
          onChange={(e) => setLanguage(e.target.value as 'zh' | 'en')}
        >
          <option value="zh">中文</option>
          <option value="en">English</option>
        </select>
      </div>

      <div className="form-section">
        <MessageItem
          msg={PREVIEW_SHOUT}
          prevMsg={null}
          nextMsg={PREVIEW_EMOTE}
          tellRef={null}
          onLinkClick={NOOP}
          tellModeAll={false}
          onReply={NOOP}
        />
        <MessageItem
          msg={PREVIEW_EMOTE}
          prevMsg={PREVIEW_SHOUT}
          nextMsg={null}
          tellRef={null}
          onLinkClick={NOOP}
          tellModeAll={false}
          onReply={NOOP}
        />
      </div>

      <div className="form-section">
        <div className="form-section-title">{t('appearance.theme')}</div>
        <div className={styles['theme-radio-group']} role="radiogroup" aria-label={t('appearance.theme')}>
          {(['light', 'dark', 'system'] as const).map((mode) => (
            <button
              key={mode}
              type="button"
              role="radio"
              aria-checked={theme === mode}
              className={clsx(styles['theme-radio'], theme === mode && styles.active)}
              onClick={() => setTheme(mode)}
            >
              {mode === 'light'
                ? t('appearance.light')
                : mode === 'dark'
                  ? t('appearance.dark')
                  : t('appearance.system')}
            </button>
          ))}
        </div>
      </div>

      <div className="form-section">
        <div className="form-section-title">{t('appearance.font')}</div>
        <p>{t('appearance.fontDescription')}</p>
        <div className={styles['font-input-container']}>
          <input
            list="font-list"
            className="form-input"
            value={fontFamily}
            onChange={(e) => setFontFamily(e.target.value)}
            placeholder={t('appearance.fontPlaceholder')}
          />
          <datalist id="font-list">
            {FONTS.map((f) => (
              <option key={f} value={f} />
            ))}
          </datalist>
        </div>

        <div className="form-row">
          <span className="form-row-label">{t('appearance.size')}</span>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '12px',
              flex: 1,
              justifyContent: 'flex-end',
            }}
          >
            <span className="form-row-label" style={{ opacity: 0.7 }}>
              {fontSize}px
            </span>
            <input
              type="range"
              className={styles['settings-range']}
              min="11"
              max="24"
              step="1"
              value={fontSize}
              onChange={(e) => setFontSize(Number(e.target.value))}
            />
          </div>
        </div>

        <div
          className="form-row"
          style={{ cursor: 'pointer' }}
          onClick={() => setItalicizeSystem(!italicizeSystem)}
        >
          <span className="form-row-label">{t('appearance.italicize')}</span>
          <div className={clsx('toggle-switch', italicizeSystem && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
      </div>

      <div className="form-section">
        <div className="form-section-title">{t('appearance.display')}</div>
        <div
          className="form-row"
          style={{ cursor: 'pointer' }}
          onClick={() => setUseColoredBackground(!useColoredBackground)}
        >
          <span className="form-row-label">{t('appearance.colorizeBackgrounds')}</span>
          <div className={clsx('toggle-switch', useColoredBackground && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>

        <div
          className="form-row"
          style={{ cursor: 'pointer' }}
          onClick={() => setLargeLinkPreviews(!largeLinkPreviews)}
        >
          <span className="form-row-label">{t('appearance.largeLinkPreviews')}</span>
          <div className={clsx('toggle-switch', largeLinkPreviews && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
      </div>
    </>
  );
}
