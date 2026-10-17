import clsx from 'clsx';
import { useSettingsStore } from '../../store/settingsStore';
import { useTranslation } from '../../i18n';
import styles from './OthersSettings.module.css';

export function OthersSettings() {
  const { t } = useTranslation();
  const tellModeAll = useSettingsStore((s) => s.tellModeAll);
  const ctrlEnterToSend = useSettingsStore((s) => s.ctrlEnterToSend);
  const emoteConfirm = useSettingsStore((s) => s.emoteConfirm);
  const emoteSortByName = useSettingsStore((s) => s.emoteSortByName);
  const retainSyncSendPrefix = useSettingsStore((s) => s.retainSyncSendPrefix);
  const sendDelayEnabled = useSettingsStore((s) => s.sendDelayEnabled);
  const sendDelaySeconds = useSettingsStore((s) => s.sendDelaySeconds);
  const sendDelayAlwaysQueue = useSettingsStore((s) => s.sendDelayAlwaysQueue);
  const setTellModeAll = useSettingsStore((s) => s.setTellModeAll);
  const setCtrlEnterToSend = useSettingsStore((s) => s.setCtrlEnterToSend);
  const setEmoteConfirm = useSettingsStore((s) => s.setEmoteConfirm);
  const setEmoteSortByName = useSettingsStore((s) => s.setEmoteSortByName);
  const setRetainSyncSendPrefix = useSettingsStore((s) => s.setRetainSyncSendPrefix);
  const setSendDelayEnabled = useSettingsStore((s) => s.setSendDelayEnabled);
  const setSendDelaySeconds = useSettingsStore((s) => s.setSendDelaySeconds);
  const setSendDelayAlwaysQueue = useSettingsStore((s) => s.setSendDelayAlwaysQueue);
  return (
    <>
      <div className="form-section">
        <div className="form-section-title">{t('others.message')}</div>
        <div
          className={clsx('form-row', styles['clickable-row'])}
          onClick={() => setTellModeAll(!tellModeAll)}
        >
          <span className="form-row-label">{t('others.tellModeAll')}</span>
          <div className={clsx('toggle-switch', tellModeAll && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
        <div
          className={clsx('form-row', styles['clickable-row'])}
          onClick={() => setCtrlEnterToSend(!ctrlEnterToSend)}
        >
          <span className="form-row-label">{t('others.ctrlEnter')}</span>
          <div className={clsx('toggle-switch', ctrlEnterToSend && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
        <div
          className={clsx('form-row', styles['clickable-row'])}
          onClick={() => setSendDelayEnabled(!sendDelayEnabled)}
        >
          <span className="form-row-label">{t('others.queue')}</span>
          <div className={clsx('toggle-switch', sendDelayEnabled && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
        {!sendDelayAlwaysQueue && (
          <div className={clsx(styles['sub-note'], !sendDelayEnabled && styles.dimmed)}>
            {t('others.queueHint')}
          </div>
        )}
        <div className={clsx('form-row', styles['indented-row'])}>
          <span className={clsx('form-row-label', !sendDelayEnabled && styles.dimmed)}>
            {t('others.queueDuration')}
          </span>
          <div className={styles['slider-container']}>
            <span
              className={clsx(
                'form-row-label',
                styles['muted-value'],
                !sendDelayEnabled && styles.dimmed,
              )}
            >
              {sendDelaySeconds}s
            </span>
            <input
              type="range"
              className={styles['settings-range']}
              min="1"
              max="10"
              step="1"
              value={sendDelaySeconds}
              onChange={(e) => setSendDelaySeconds(Number(e.target.value))}
              disabled={!sendDelayEnabled}
            />
          </div>
        </div>
        <div
          className={clsx(
            'form-row',
            styles['indented-row'],
            sendDelayEnabled && styles['clickable-row'],
          )}
          onClick={() => sendDelayEnabled && setSendDelayAlwaysQueue(!sendDelayAlwaysQueue)}
        >
          <span className={clsx('form-row-label', !sendDelayEnabled && styles.dimmed)}>
            {t('others.alwaysQueue')}
          </span>
          <div
            className={clsx(
              'toggle-switch',
              sendDelayAlwaysQueue && 'on',
              !sendDelayEnabled && styles.dimmed,
            )}
          >
            <div className="toggle-knob" />
          </div>
        </div>
      </div>

      <div className="form-section">
        <div className="form-section-title">{t('others.emote')}</div>
        <div
          className={clsx('form-row', styles['clickable-row'])}
          onClick={() => setEmoteConfirm(!emoteConfirm)}
        >
          <span className="form-row-label">{t('others.confirmEmote')}</span>
          <div className={clsx('toggle-switch', emoteConfirm && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
        <div
          className={clsx('form-row', styles['clickable-row'])}
          onClick={() => setEmoteSortByName(!emoteSortByName)}
        >
          <span className="form-row-label">{t('others.sortEmotes')}</span>
          <div className={clsx('toggle-switch', emoteSortByName && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
      </div>

      <div className="form-section">
        <div className="form-section-title">{t('others.sendDestination')}</div>
        <div
          className={clsx('form-row', styles['clickable-row'])}
          onClick={() => setRetainSyncSendPrefix(!retainSyncSendPrefix)}
        >
          <span className="form-row-label">{t('others.retainSync')}</span>
          <div className={clsx('toggle-switch', retainSyncSendPrefix && 'on')}>
            <div className="toggle-knob" />
          </div>
        </div>
      </div>
    </>
  );
}
