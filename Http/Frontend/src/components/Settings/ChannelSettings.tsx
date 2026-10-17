import type { Dispatch, SetStateAction } from 'react';
import clsx from 'clsx';
import styles from './ChannelSettings.module.css';
import { ALL_CHANNELS } from '../../constants/channels';
import { getBadgeInfoByPrefix } from '../../lib/channelUtils';
import type { ChannelOption } from '../../types/chat';
import { useSettingsStore } from '../../store/settingsStore';
import { useTheme } from '../../hooks/useTheme';
import { useTranslation } from '../../i18n';
import { useChannelLabelByPrefix } from '../../i18n/channelLabels';

interface Props {
  serverChannels: ChannelOption[];
  selectedSendPrefix: string;
  setSelectedSendPrefix: Dispatch<SetStateAction<string>>;
}

export function ChannelSettings({
  serverChannels,
  selectedSendPrefix,
  setSelectedSendPrefix,
}: Props) {
  const { t } = useTranslation();
  const channelLabelByPrefix = useChannelLabelByPrefix();
  const disabledChannels = useSettingsStore((s) => s.disabledChannels);
  const setDisabledChannels = useSettingsStore((s) => s.setDisabledChannels);
  const theme = useTheme();

  const mergedChannels: ChannelOption[] = [
    ...ALL_CHANNELS,
    ...serverChannels.filter((sc) => !ALL_CHANNELS.some((ac) => ac.prefix === sc.prefix)),
  ];

  const toggleChannel = (prefix: string) => {
    const next = new Set(disabledChannels);
    if (next.has(prefix)) {
      next.delete(prefix);
    } else {
      next.add(prefix);
    }
    if (next.has(selectedSendPrefix)) {
      const first = mergedChannels.find((c) => !next.has(c.prefix));
      if (first) setSelectedSendPrefix(first.prefix);
    }
    setDisabledChannels(next);
  };

  return (
    <>
      <div className="form-section">
        <div className="form-section-title">{t('channels.title')}</div>
        <p>{t('channels.description')}</p>
      </div>
      <div className={styles['ch-toggle-list']}>
        {mergedChannels.map((ch) => {
          const badge = getBadgeInfoByPrefix(ch.prefix);
          const enabled = !disabledChannels.has(ch.prefix);
          const label = channelLabelByPrefix(ch.prefix);
          return (
            <div
              key={ch.prefix}
              className={styles['ch-toggle-row']}
              onClick={() => toggleChannel(ch.prefix)}
            >
              <span className="channel-badge" style={theme.badgeStyle(badge)}>
                {label}
              </span>
              <span className={styles['ch-toggle-label']}>{label}</span>
              <div className={clsx('toggle-switch', enabled && 'on')}>
                <div className="toggle-knob" />
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}
