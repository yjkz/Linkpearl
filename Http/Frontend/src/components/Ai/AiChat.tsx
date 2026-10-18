import { useEffect, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import styles from './AiChat.module.css';
import { useAiStore } from '../../store/aiStore';
import { useTranslation } from '../../i18n';
import { AI_CONTEXT_CATEGORIES, AI_CONTEXT_CHANNELS } from '../../constants/aiContext';

export function AiChat() {
  const { t } = useTranslation();
  const settings = useAiStore((s) => s.settings);
  const chatTurns = useAiStore((s) => s.chatTurns);
  const chatSending = useAiStore((s) => s.chatSending);
  const chatError = useAiStore((s) => s.chatError);
  const chatContextCount = useAiStore((s) => s.chatContextCount);
  const sendChat = useAiStore((s) => s.sendChat);
  const clearChat = useAiStore((s) => s.clearChat);
  const clearChatContextCount = useAiStore((s) => s.clearChatContextCount);

  const agents = settings?.agents ?? [];
  const [agentId, setAgentId] = useState('');
  const [scopes, setScopes] = useState<Set<string>>(new Set());
  const [draft, setDraft] = useState('');
  const bottomRef = useRef<HTMLDivElement>(null);

  const activeAgentId = agentId || agents[0]?.id || '';
  const noAgents = agents.length === 0;

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [chatTurns.length, chatSending]);

  const toggleScope = (key: string) => {
    clearChatContextCount();
    setScopes((previous) => {
      const next = new Set(previous);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const handleSend = () => {
    const message = draft.trim();
    if (!message || chatSending || !settings?.enabled || noAgents) return;
    setDraft('');
    void sendChat(message, Array.from(scopes), activeAgentId || undefined);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      handleSend();
    }
  };

  const disabled = chatSending || !settings?.enabled || noAgents || !draft.trim();

  return (
    <div className={styles.chat}>
      {settings && !settings.enabled && <p className={styles.hint}>{t('ai.disabledHint')}</p>}
      {noAgents && <p className={styles.hint}>{t('ai.noAgents')}</p>}

      <label className={styles.agentField}>
        <span className={styles.fieldLabel}>{t('ai.chat.agent')}</span>
        <select
          className="form-select"
          value={activeAgentId}
          onChange={(event) => setAgentId(event.target.value)}
          disabled={noAgents}
        >
          {agents.map((agent) => (
            <option key={agent.id} value={agent.id}>
              {agent.name}
            </option>
          ))}
        </select>
      </label>

      <details className={styles.context}>
        <summary className={styles.contextSummary}>{t('ai.chat.contextChannels')}</summary>

        <div className={styles.contextGroup}>
          <span className={styles.contextLabel}>{t('ai.chat.categories')}</span>
          <div className={styles.contextItems}>
            {AI_CONTEXT_CATEGORIES.map((scope) => (
              <label key={scope.key} className={styles.contextItem}>
                <input
                  type="checkbox"
                  checked={scopes.has(scope.key)}
                  onChange={() => toggleScope(scope.key)}
                />
                <span>{t(scope.labelKey)}</span>
              </label>
            ))}
          </div>
        </div>

        <div className={styles.contextGroup}>
          <span className={styles.contextLabel}>{t('ai.chat.channels')}</span>
          <div className={styles.contextItems}>
            {AI_CONTEXT_CHANNELS.map((scope) => (
              <label key={scope.key} className={styles.contextItem}>
                <input
                  type="checkbox"
                  checked={scopes.has(scope.key)}
                  onChange={() => toggleScope(scope.key)}
                />
                <span>{t(scope.labelKey)}</span>
              </label>
            ))}
          </div>
        </div>

        <p className={styles.hint}>{t('ai.chat.contextHint')}</p>
      </details>

      {scopes.size > 0 && chatContextCount !== null && (
        <p className={chatContextCount > 0 ? styles.hint : styles.contextWarning}>
          {chatContextCount > 0
            ? t('ai.chat.contextAttached', { count: chatContextCount })
            : t('ai.chat.noContext')}
        </p>
      )}

      <div className={styles.messages}>
        {chatTurns.length === 0 && !chatSending && (
          <p className={styles.empty}>{t('ai.chat.empty')}</p>
        )}

        {chatTurns.map((turn) => (
          <div
            key={turn.id}
            className={`${styles.bubble} ${turn.role === 'user' ? styles.user : styles.assistant}`}
          >
            <span className={styles.role}>
              {turn.role === 'user' ? t('ai.chat.you') : t('ai.chat.assistant')}
            </span>
            <span className={styles.content}>{turn.content}</span>
          </div>
        ))}

        {chatSending && <p className={styles.thinking}>{t('ai.chat.thinking')}</p>}
        <div ref={bottomRef} />
      </div>

      {chatError && (
        <p className={styles.error}>
          {t('ai.chat.error')}: {chatError}
        </p>
      )}

      <div className={styles.composer}>
        <textarea
          className={styles.input}
          value={draft}
          placeholder={t('ai.chat.placeholder')}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={handleKeyDown}
          rows={2}
          disabled={chatSending || !settings?.enabled}
        />
        <div className={styles.composerActions}>
          <button
            type="button"
            className={styles.clear}
            onClick={clearChat}
            disabled={chatSending || chatTurns.length === 0}
          >
            {t('ai.chat.clear')}
          </button>
          <button type="button" className={styles.send} onClick={handleSend} disabled={disabled}>
            {t('ai.chat.send')}
          </button>
        </div>
      </div>
    </div>
  );
}
