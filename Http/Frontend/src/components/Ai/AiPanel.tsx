import { useState } from 'react';
import clsx from 'clsx';
import { Settings2, Sparkles, X } from 'lucide-react';
import styles from './AiPanel.module.css';
import { AiChat } from './AiChat';
import { useAiStore } from '../../store/aiStore';
import { useAiDataLoader } from '../../hooks/useAi';
import { useTranslation } from '../../i18n';
import { channelLabel } from '../../i18n/channelLabels';
import type { TranslationKey } from '../../i18n';
import type { AiActivity, AiReplyMode, AiSuggestion } from '../../types/ai';

interface Props {
  onClose: () => void;
  onOpenSettings: () => void;
}

function modeKey(mode: AiReplyMode): TranslationKey {
  switch (mode) {
    case 'options':
      return 'ai.mode.options';
    case 'suggestion':
      return 'ai.mode.suggestion';
    default:
      return 'ai.mode.auto';
  }
}

function activityKindKey(kind: AiActivity['kind']): TranslationKey {
  switch (kind) {
    case 'error':
      return 'ai.activity.kind.error';
    case 'auto-reply':
      return 'ai.activity.kind.auto';
    default:
      return 'ai.activity.kind.info';
  }
}

function SuggestionCard({
  suggestion,
  onSend,
  onDismiss,
}: {
  suggestion: AiSuggestion;
  onSend: (id: string, index?: number, text?: string) => void;
  onDismiss: (id: string) => void;
}) {
  const { t, language } = useTranslation();
  const [draft, setDraft] = useState(suggestion.candidates[0] ?? '');
  const channel = channelLabel(language, suggestion.channelType);

  return (
    <article className={styles.card}>
      <header className={styles.cardHeader}>
        <span className={styles.channel}>{channel}</span>
        <span className={styles.sender}>{suggestion.senderName}</span>
        <span className={styles.mode}>{t(modeKey(suggestion.mode))}</span>
      </header>

      <p className={styles.source}>{suggestion.sourceText}</p>

      <div className={styles.meta}>
        {t('ai.ruleLabel')}: {suggestion.ruleName} · {t('ai.agentLabel')}: {suggestion.agentName}
      </div>

      {suggestion.mode === 'options' ? (
        <div className={styles.options}>
          {suggestion.candidates.map((candidate, index) => (
            <div key={`${suggestion.id}-${index}`} className={styles.option}>
              <span className={styles.optionText}>{candidate}</span>
              <button
                type="button"
                className={styles.primary}
                onClick={() => onSend(suggestion.id, index)}
              >
                {t('ai.send')}
              </button>
            </div>
          ))}
        </div>
      ) : (
        <div className={styles.suggestionEditor}>
          <textarea
            className={styles.textarea}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            rows={3}
          />
          <button
            type="button"
            className={styles.primary}
            onClick={() => onSend(suggestion.id, undefined, draft)}
          >
            {t('ai.send')}
          </button>
        </div>
      )}

      <div className={styles.cardFooter}>
        <button type="button" className={styles.secondary} onClick={() => onDismiss(suggestion.id)}>
          {t('ai.dismiss')}
        </button>
      </div>
    </article>
  );
}

export function AiPanel({ onClose, onOpenSettings }: Props) {
  useAiDataLoader(true);

  const { t } = useTranslation();
  const [tab, setTab] = useState<'suggestions' | 'chat'>('suggestions');
  const settings = useAiStore((s) => s.settings);
  const suggestions = useAiStore((s) => s.suggestions);
  const activity = useAiStore((s) => s.activity);
  const error = useAiStore((s) => s.error);
  const sendSuggestion = useAiStore((s) => s.sendSuggestion);
  const dismissSuggestion = useAiStore((s) => s.dismissSuggestion);

  const notConfigured =
    settings != null && (!settings.apiKeySet || !settings.defaultModel.trim());

  return (
    <div className={styles.overlay} onClick={onClose}>
      <aside
        className={styles.panel}
        onClick={(e) => e.stopPropagation()}
        aria-label={t('ai.title')}
      >
        <header className={styles.header}>
          <div className={styles.titleRow}>
            <Sparkles size={16} strokeWidth={1.8} aria-hidden />
            <span className={styles.title}>{t('ai.title')}</span>
            {suggestions.length > 0 && (
              <span className={styles.count}>{t('ai.pending', { count: suggestions.length })}</span>
            )}
          </div>
          <div className={styles.headerActions}>
            <button
              type="button"
              className={styles.iconButton}
              onClick={onOpenSettings}
              aria-label={t('ai.settings')}
              data-tooltip={t('ai.settings')}
            >
              <Settings2 size={16} aria-hidden />
            </button>
            <button
              type="button"
              className={styles.iconButton}
              onClick={onClose}
              aria-label={t('common.close')}
            >
              <X size={16} aria-hidden />
            </button>
          </div>
        </header>

        <div className={styles.tabs}>
          <button
            type="button"
            className={clsx(styles.tab, tab === 'suggestions' && styles.tabActive)}
            onClick={() => setTab('suggestions')}
          >
            {t('ai.tab.suggestions')}
          </button>
          <button
            type="button"
            className={clsx(styles.tab, tab === 'chat' && styles.tabActive)}
            onClick={() => setTab('chat')}
          >
            {t('ai.tab.chat')}
          </button>
        </div>

        <div className={styles.body}>
          <div className={styles.tabPanel} hidden={tab !== 'suggestions'}>
            {error && <div className={styles.error}>{error}</div>}
            {settings && !settings.enabled && (
              <div className={styles.hint}>{t('ai.disabledHint')}</div>
            )}
            {notConfigured && <div className={styles.hint}>{t('ai.notConfiguredHint')}</div>}

            <section className={styles.section}>
              <h3 className={styles.sectionTitle}>{t('ai.tab.suggestions')}</h3>
              {suggestions.length === 0 ? (
                <p className={styles.empty}>{t('ai.noSuggestions')}</p>
              ) : (
                suggestions.map((suggestion) => (
                  <SuggestionCard
                    key={suggestion.id}
                    suggestion={suggestion}
                    onSend={(id, index, text) => {
                      void sendSuggestion(id, index, text);
                    }}
                    onDismiss={(id) => {
                      void dismissSuggestion(id);
                    }}
                  />
                ))
              )}
            </section>

            <section className={styles.section}>
              <h3 className={styles.sectionTitle}>{t('ai.activity')}</h3>
              {activity.length === 0 ? (
                <p className={styles.empty}>{t('ai.noActivity')}</p>
              ) : (
                <ul className={styles.activityList}>
                  {activity.slice(0, 20).map((entry, index) => (
                    <li
                      key={`${entry.timestamp}-${index}`}
                      className={styles.activityItem}
                      data-kind={entry.kind}
                    >
                      <span className={styles.activityKind}>{t(activityKindKey(entry.kind))}</span>
                      <span className={styles.activityMessage}>{entry.message}</span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>

          <div className={styles.tabPanel} hidden={tab !== 'chat'}>
            <AiChat />
          </div>
        </div>
      </aside>
    </div>
  );
}
