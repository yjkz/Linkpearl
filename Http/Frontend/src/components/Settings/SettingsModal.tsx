import { useState } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import clsx from 'clsx';
import { Palette, MessageSquare, AlignLeft, Filter as FilterIcon, Shield, Sparkles } from 'lucide-react';
import styles from './SettingsModal.module.css';
import { AppearanceSettings } from './AppearanceSettings';
import { ChannelSettings } from './ChannelSettings';
import { FiltersSettings } from './FiltersSettings';
import { OthersSettings } from './OthersSettings';
import { SecuritySettings } from './SecuritySettings';
import { AiSettings } from '../Ai/AiSettings';
import type { ChannelOption } from '../../types/chat';
import type { FilterMode } from '../../hooks/useFilterMode';
import { useTranslation } from '../../i18n';

export type SettingsCategory =
  | 'appearance'
  | 'send-channels'
  | 'filters'
  | 'chat-input'
  | 'security'
  | 'ai';

interface Props {
  // Send Channels (selectedSendPrefix is transient UI state owned by App)
  serverChannels: ChannelOption[];
  selectedSendPrefix: string;
  setSelectedSendPrefix: Dispatch<SetStateAction<string>>;
  // Filters
  filterMode: FilterMode | null;
  onDeletePersonalFilters: (key: string) => Promise<void>;
  // Control
  initialCategory?: SettingsCategory;
  onClose: () => void;
}

export function SettingsModal({
  serverChannels,
  selectedSendPrefix,
  setSelectedSendPrefix,
  filterMode,
  onDeletePersonalFilters,
  initialCategory = 'appearance',
  onClose,
}: Props) {
  const { t } = useTranslation();
  const [category, setCategory] = useState<SettingsCategory>(initialCategory);
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  const handleClose = () => {
    setCategory('appearance');
    setIsSidebarOpen(false);
    onClose();
  };

  const selectCategory = (cat: SettingsCategory) => {
    setCategory(cat);
    setIsSidebarOpen(false);
  };

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className={styles['settings-modal']} onClick={(e) => e.stopPropagation()}>
        {isSidebarOpen && (
          <div
            className={styles['settings-sidebar-overlay']}
            onClick={() => setIsSidebarOpen(false)}
          />
        )}

        <div className={clsx(styles['settings-sidebar'], isSidebarOpen && styles.open)}>
          <div className={styles['settings-sidebar-title']}>
            {t('settings.title')}
            <span className={styles['version-label']}>v{__APP_VERSION__}</span>
          </div>
          <nav className={styles['settings-nav']}>
            <button
              className={clsx(
                styles['settings-nav-item'],
                category === 'appearance' && styles.active,
              )}
              onClick={() => selectCategory('appearance')}
            >
              <Palette size={16} strokeWidth={1.5} aria-hidden />
              {t('settings.appearance')}
            </button>
            <button
              className={clsx(
                styles['settings-nav-item'],
                category === 'chat-input' && styles.active,
              )}
              onClick={() => selectCategory('chat-input')}
            >
              <MessageSquare size={16} strokeWidth={1.5} aria-hidden />
              {t('settings.chatInput')}
            </button>
            <button
              className={clsx(
                styles['settings-nav-item'],
                category === 'send-channels' && styles.active,
              )}
              onClick={() => selectCategory('send-channels')}
            >
              <AlignLeft size={16} strokeWidth={1.5} aria-hidden />
              {t('settings.sendChannels')}
            </button>
            <button
              className={clsx(styles['settings-nav-item'], category === 'filters' && styles.active)}
              onClick={() => selectCategory('filters')}
            >
              <FilterIcon size={16} strokeWidth={1.5} aria-hidden />
              {t('settings.filters')}
            </button>
            <button
              className={clsx(
                styles['settings-nav-item'],
                category === 'security' && styles.active,
              )}
              onClick={() => selectCategory('security')}
            >
              <Shield size={16} strokeWidth={1.5} aria-hidden />
              {t('settings.security')}
            </button>
            <button
              className={clsx(styles['settings-nav-item'], category === 'ai' && styles.active)}
              onClick={() => selectCategory('ai')}
            >
              <Sparkles size={16} strokeWidth={1.5} aria-hidden />
              {t('settings.ai')}
            </button>
          </nav>
        </div>

        <div className={styles['settings-content']}>
          <div className={styles['settings-content-header']}>
            <button
              className={styles['settings-hamburger-btn']}
              onClick={() => setIsSidebarOpen((prev) => !prev)}
              aria-label={t('settings.toggleCategories')}
            >
              <span className={clsx('hamburger-icon', isSidebarOpen && 'open')}>
                <span />
                <span />
                <span />
              </span>
            </button>
            <h2>
              {category === 'appearance' && t('settings.appearance')}
              {category === 'send-channels' && t('settings.sendChannels')}
              {category === 'filters' && t('settings.filters')}
              {category === 'chat-input' && t('settings.chatInput')}
              {category === 'security' && t('settings.security')}
              {category === 'ai' && t('settings.ai')}
            </h2>
            <button className={styles['modal-close']} onClick={handleClose}>
              ×
            </button>
          </div>

          <div className={styles['settings-content-body']} key={category}>
            {category === 'appearance' && <AppearanceSettings />}
            {category === 'send-channels' && (
              <ChannelSettings
                serverChannels={serverChannels}
                selectedSendPrefix={selectedSendPrefix}
                setSelectedSendPrefix={setSelectedSendPrefix}
              />
            )}
            {category === 'filters' && (
              <FiltersSettings
                filterMode={filterMode}
                onDeletePersonalFilters={onDeletePersonalFilters}
              />
            )}
            {category === 'chat-input' && <OthersSettings />}
            {category === 'security' && <SecuritySettings />}
            {category === 'ai' && <AiSettings />}
          </div>
        </div>
      </div>
    </div>
  );
}
