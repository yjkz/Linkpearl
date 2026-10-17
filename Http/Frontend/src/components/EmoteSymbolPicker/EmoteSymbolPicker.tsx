import { useState } from 'react';
import clsx from 'clsx';
import styles from './EmoteSymbolPicker.module.css';
import { EmoteTab } from './EmoteTab';
import { SymbolsTab } from './SymbolsTab';
import { useEmoteList } from '../../hooks/useEmoteList';
import { useTranslation } from '../../i18n';

type ActiveTab = 'emotes' | 'symbols';

const TABS: { id: ActiveTab; labelKey: 'emote.emotes' | 'emote.symbols' }[] = [
  { id: 'emotes', labelKey: 'emote.emotes' },
  { id: 'symbols', labelKey: 'emote.symbols' },
];

interface Props {
  /** Inserts text into the chat input (used by Symbols tab). */
  onInsert: (text: string) => void;
  /** Directly executes the emote command (used by Emotes tab). */
  onExecute: (command: string) => void;
}

export function EmoteSymbolPicker({ onInsert, onExecute }: Props) {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<ActiveTab>('emotes');
  const { emotes, loading, error } = useEmoteList();

  return (
    <div className={styles.picker} data-emote-symbol-picker>
      <div className={styles['tab-bar']}>
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={clsx(styles['tab-btn'], activeTab === tab.id && styles.active)}
            onClick={() => setActiveTab(tab.id)}
          >
            {t(tab.labelKey)}
          </button>
        ))}
      </div>

      {activeTab === 'emotes' ? (
        <EmoteTab emotes={emotes} loading={loading} error={error} onExecute={onExecute} />
      ) : (
        <SymbolsTab onSelect={onInsert} />
      )}
    </div>
  );
}
