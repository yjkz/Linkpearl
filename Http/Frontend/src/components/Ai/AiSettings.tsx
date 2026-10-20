import { useEffect, useMemo, useState } from 'react';
import styles from './AiSettings.module.css';
import { useAiStore } from '../../store/aiStore';
import { useAiDataLoader } from '../../hooks/useAi';
import { useTranslation, type TranslationKey } from '../../i18n';
import type {
  AiAgent,
  AiAgentPreset,
  AiRule,
  AiSettings,
  AiSettingsUpdate,
  AiTestResult,
} from '../../types/ai';

const TRIGGER_CHANNEL_GROUPS: { key: TranslationKey; types: number[] }[] = [
  { key: 'channel.say', types: [10] },
  { key: 'channel.party', types: [14, 32] },
  { key: 'channel.alliance', types: [15] },
  { key: 'channel.yell', types: [30] },
  { key: 'channel.shout', types: [11] },
  { key: 'channel.freeCompany', types: [24] },
  { key: 'channel.noviceNetwork', types: [27] },
  { key: 'channel.tell', types: [13] },
  { key: 'channel.ls1', types: [16] },
  { key: 'channel.ls2', types: [17] },
  { key: 'channel.ls3', types: [18] },
  { key: 'channel.ls4', types: [19] },
  { key: 'channel.ls5', types: [20] },
  { key: 'channel.ls6', types: [21] },
  { key: 'channel.ls7', types: [22] },
  { key: 'channel.ls8', types: [23] },
  { key: 'channel.cwls1', types: [37] },
  { key: 'channel.cwls2', types: [101] },
  { key: 'channel.cwls3', types: [102] },
  { key: 'channel.cwls4', types: [103] },
  { key: 'channel.cwls5', types: [104] },
  { key: 'channel.cwls6', types: [105] },
  { key: 'channel.cwls7', types: [106] },
  { key: 'channel.cwls8', types: [107] },
];

interface Draft {
  enabled: boolean;
  apiBaseUrl: string;
  apiKey: string;
  defaultModel: string;
  contextMessageCount: number;
  timeoutSeconds: number;
  agents: AiAgent[];
  rules: AiRule[];
}

function toDraft(settings: AiSettings): Draft {
  return {
    enabled: settings.enabled,
    apiBaseUrl: settings.apiBaseUrl,
    apiKey: '',
    defaultModel: settings.defaultModel,
    contextMessageCount: settings.contextMessageCount,
    timeoutSeconds: settings.timeoutSeconds,
    agents: settings.agents.map((a) => ({ ...a })),
    rules: settings.rules.map((r) => ({ ...r, channelTypes: [...r.channelTypes] })),
  };
}

function newAgent(index: number): AiAgent {
  return {
    id: `agent_${Date.now()}_${index}`,
    name: `Agent ${index + 1}`,
    systemPrompt: '',
    model: '',
    temperature: 0.7,
    maxTokens: 300,
  };
}

function newRule(index: number, agentId: string): AiRule {
  return {
    id: `rule_${Date.now()}_${index}`,
    name: `Rule ${index + 1}`,
    enabled: false,
    agentId,
    mode: 'auto',
    channelTypes: [],
    cooldownSeconds: 10,
    maxPerMinute: 6,
    optionsCount: 3,
  };
}

export function AiSettings() {
  useAiDataLoader(true);
  const { t } = useTranslation();
  const settings = useAiStore((s) => s.settings);
  const error = useAiStore((s) => s.error);
  const saving = useAiStore((s) => s.saving);
  const saveSettings = useAiStore((s) => s.saveSettings);
  const testConnection = useAiStore((s) => s.testConnection);
  const clearError = useAiStore((s) => s.clearError);

  const [draft, setDraft] = useState<Draft | null>(null);
  const [selectedAgent, setSelectedAgent] = useState(0);
  const [selectedRule, setSelectedRule] = useState(0);
  const [saved, setSaved] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<AiTestResult | null>(null);

  useEffect(() => {
    if (settings) setDraft(toDraft(settings));
  }, [settings]);

  const agentOptions = useMemo(
    () => draft?.agents.map((agent) => ({ id: agent.id, name: agent.name })) ?? [],
    [draft],
  );

  if (!draft) {
    return error ? (
      <div className={styles.error}>{error}</div>
    ) : (
      <p className="form-hint">{t('common.loading')}</p>
    );
  }

  const update = (patch: Partial<Draft>) =>
    setDraft((current) => (current ? { ...current, ...patch } : current));

  const updateAgent = (index: number, patch: Partial<AiAgent>) =>
    setDraft((current) =>
      current
        ? {
            ...current,
            agents: current.agents.map((agent, i) => (i === index ? { ...agent, ...patch } : agent)),
          }
        : current,
    );

  const updateRule = (index: number, patch: Partial<AiRule>) =>
    setDraft((current) =>
      current
        ? {
            ...current,
            rules: current.rules.map((rule, i) => (i === index ? { ...rule, ...patch } : rule)),
          }
        : current,
    );

  const buildPayload = (): AiSettingsUpdate => ({
    enabled: draft.enabled,
    apiBaseUrl: draft.apiBaseUrl.trim(),
    defaultModel: draft.defaultModel.trim(),
    contextMessageCount: Math.min(100, Math.max(1, draft.contextMessageCount)),
    timeoutSeconds: Math.min(120, Math.max(5, draft.timeoutSeconds)),
    agents: draft.agents,
    rules: draft.rules,
    apiKey: draft.apiKey.trim() ? draft.apiKey.trim() : undefined,
  });

  const handleSave = async () => {
    clearError();
    const ok = await saveSettings(buildPayload());
    if (ok) {
      setSaved(true);
      window.setTimeout(() => setSaved(false), 2000);
    }
  };

  const handleTest = async () => {
    clearError();
    setTestResult(null);
    const ok = await saveSettings(buildPayload());
    if (!ok) return;
    setTesting(true);
    try {
      setTestResult(await testConnection());
    } finally {
      setTesting(false);
    }
  };

  const handleAddAgent = () => {
    const agent = newAgent(draft.agents.length);
    update({ agents: [...draft.agents, agent] });
    setSelectedAgent(draft.agents.length);
  };

  const handleAddPreset = (preset: AiAgentPreset) => {
    const used = new Set(draft.agents.map((agent) => agent.name.toLowerCase()));
    let name = preset.name;
    let suffix = 2;
    while (used.has(name.toLowerCase())) name = `${preset.name} ${suffix++}`;

    const agent: AiAgent = {
      id: `agent_${Date.now()}_${draft.agents.length}`,
      name,
      systemPrompt: preset.systemPrompt,
      model: '',
      temperature: preset.temperature,
      maxTokens: preset.maxTokens,
    };

    update({ agents: [...draft.agents, agent] });
    setSelectedAgent(draft.agents.length);
  };

  const handleRemoveAgent = () => {
    if (draft.agents.length <= 1) return;
    const remaining = draft.agents.filter((_, i) => i !== selectedAgent);
    const fallback = remaining[0]?.id ?? '';
    update({
      agents: remaining,
      rules: draft.rules.map((rule) =>
        remaining.some((agent) => agent.id === rule.agentId)
          ? rule
          : { ...rule, agentId: fallback },
      ),
    });
    setSelectedAgent(Math.min(selectedAgent, remaining.length - 1));
  };

  const handleAddRule = () => {
    const rule = newRule(draft.rules.length, draft.agents[0]?.id ?? '');
    update({ rules: [...draft.rules, rule] });
    setSelectedRule(draft.rules.length);
  };

  const handleRemoveRule = () => {
    if (draft.rules.length === 0) return;
    const remaining = draft.rules.filter((_, i) => i !== selectedRule);
    update({ rules: remaining });
    setSelectedRule(Math.min(selectedRule, Math.max(0, remaining.length - 1)));
  };

  const presets = settings?.presets ?? [];
  const currentAgent = draft.agents[Math.min(selectedAgent, draft.agents.length - 1)];
  const currentRule = draft.rules[Math.min(selectedRule, draft.rules.length - 1)];

  return (
    <div className={styles.container}>
      {error && <div className={styles.error}>{error}</div>}

      {/* ── Provider ── */}
      <div className="form-section">
        <div className="form-section-title">{t('ai.section.global')}</div>

        <div
          className={styles.toggleRow}
          onClick={() => update({ enabled: !draft.enabled })}
        >
          <span className="form-row-label">{t('ai.enabled')}</span>
          <div className={`toggle-switch${draft.enabled ? ' on' : ''}`}>
            <div className="toggle-knob" />
          </div>
        </div>

        <label className={styles.field}>
          <span className={styles.fieldLabel}>{t('ai.baseUrl')}</span>
          <input
            className="form-input"
            value={draft.apiBaseUrl}
            onChange={(e) => update({ apiBaseUrl: e.target.value })}
            placeholder="https://api.openai.com/v1"
          />
        </label>

        <label className={styles.field}>
          <span className={styles.fieldLabel}>{t('ai.apiKey')}</span>
          <input
            className="form-input"
            type="password"
            value={draft.apiKey}
            onChange={(e) => update({ apiKey: e.target.value })}
            placeholder={settings?.apiKeySet ? t('ai.apiKeyKeep') : ''}
            autoComplete="off"
          />
        </label>

        <label className={styles.field}>
          <span className={styles.fieldLabel}>{t('ai.defaultModel')}</span>
          <input
            className="form-input"
            value={draft.defaultModel}
            onChange={(e) => update({ defaultModel: e.target.value })}
            placeholder="gpt-4o-mini"
          />
        </label>

        <div className={styles.inlineFields}>
          <label className={styles.field}>
            <span className={styles.fieldLabel}>{t('ai.contextCount')}</span>
            <input
              className="form-input"
              type="number"
              min={1}
              max={100}
              value={draft.contextMessageCount}
              onChange={(e) => update({ contextMessageCount: Number(e.target.value) })}
            />
          </label>
          <label className={styles.field}>
            <span className={styles.fieldLabel}>{t('ai.timeout')}</span>
            <input
              className="form-input"
              type="number"
              min={5}
              max={120}
              value={draft.timeoutSeconds}
              onChange={(e) => update({ timeoutSeconds: Number(e.target.value) })}
            />
          </label>
        </div>

        <div className={styles.testRow}>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={testing || saving}
            onClick={handleTest}
          >
            {testing ? t('ai.testing') : t('ai.test')}
          </button>
          {testResult && (
            <span className={testResult.ok ? styles.testOk : styles.testFailed}>
              {testResult.ok
                ? `${t('ai.testOk')} (${testResult.latencyMs ?? 0} ms)`
                : `${t('ai.testFailed')}: ${testResult.error ?? ''}`}
            </span>
          )}
        </div>
        <span className="form-hint">{t('ai.testHint')}</span>
      </div>

      {/* ── Agents ── */}
      <div className="form-section">
        <div className="form-section-title">{t('ai.section.agents')}</div>

        {draft.agents.length === 0 ? (
          <p className="form-hint">{t('ai.noAgents')}</p>
        ) : (
          <>
            <div className={styles.listHeader}>
              <select
                className="form-select"
                value={selectedAgent}
                onChange={(e) => setSelectedAgent(Number(e.target.value))}
              >
                {agentOptions.map((agent, index) => (
                  <option key={agent.id} value={index}>
                    {agent.name}
                  </option>
                ))}
              </select>
              <button type="button" className="btn btn-secondary" onClick={handleAddAgent}>
                {t('ai.addAgent')}
              </button>
              <button
                type="button"
                className="btn btn-secondary"
                disabled={draft.agents.length <= 1}
                onClick={handleRemoveAgent}
              >
                {t('ai.removeAgent')}
              </button>
            </div>

            {presets.length > 0 && (
              <div className={styles.listHeader}>
                <select
                  className="form-select"
                  value=""
                  onChange={(e) => {
                    const preset = presets.find((item) => item.id === e.target.value);
                    if (preset) handleAddPreset(preset);
                  }}
                >
                  <option value="">{t('ai.preset.placeholder')}</option>
                  {presets.map((preset) => (
                    <option key={preset.id} value={preset.id}>
                      {preset.name}
                    </option>
                  ))}
                </select>
                <span className="form-hint">{t('ai.preset.hint')}</span>
              </div>
            )}

            {currentAgent && (
              <div className={styles.editor}>
                <label className={styles.field}>
                  <span className={styles.fieldLabel}>{t('ai.agentName')}</span>
                  <input
                    className="form-input"
                    value={currentAgent.name}
                    onChange={(e) => updateAgent(selectedAgent, { name: e.target.value })}
                  />
                </label>

                <label className={styles.field}>
                  <span className={styles.fieldLabel}>{t('ai.agentModel')}</span>
                  <input
                    className="form-input"
                    value={currentAgent.model}
                    onChange={(e) => updateAgent(selectedAgent, { model: e.target.value })}
                    placeholder={draft.defaultModel || 'gpt-4o-mini'}
                  />
                </label>

                <div className={styles.inlineFields}>
                  <label className={styles.field}>
                    <span className={styles.fieldLabel}>
                      {t('ai.agentTemperature')}: {currentAgent.temperature.toFixed(2)}
                    </span>
                    <input
                      type="range"
                      className={styles.range}
                      min={0}
                      max={2}
                      step={0.05}
                      value={currentAgent.temperature}
                      onChange={(e) =>
                        updateAgent(selectedAgent, { temperature: Number(e.target.value) })
                      }
                    />
                  </label>
                  <label className={styles.field}>
                    <span className={styles.fieldLabel}>{t('ai.agentMaxTokens')}</span>
                    <input
                      className="form-input"
                      type="number"
                      min={1}
                      max={4096}
                      value={currentAgent.maxTokens}
                      onChange={(e) =>
                        updateAgent(selectedAgent, { maxTokens: Number(e.target.value) })
                      }
                    />
                  </label>
                </div>

                <label className={styles.field}>
                  <span className={styles.fieldLabel}>
                    {t('ai.agentPrompt')} · {currentAgent.systemPrompt.length}
                  </span>
                  <textarea
                    className={styles.textarea}
                    rows={5}
                    value={currentAgent.systemPrompt}
                    onChange={(e) =>
                      updateAgent(selectedAgent, { systemPrompt: e.target.value })
                    }
                  />
                </label>
              </div>
            )}
          </>
        )}

        {draft.agents.length === 0 && (
          <button type="button" className="btn btn-secondary" onClick={handleAddAgent}>
            {t('ai.addAgent')}
          </button>
        )}
      </div>

      {/* ── Rules ── */}
      <div className="form-section">
        <div className="form-section-title">{t('ai.section.rules')}</div>

        {draft.rules.length === 0 ? (
          <p className="form-hint">{t('ai.noRules')}</p>
        ) : (
          <>
            <div className={styles.listHeader}>
              <select
                className="form-select"
                value={selectedRule}
                onChange={(e) => setSelectedRule(Number(e.target.value))}
              >
                {draft.rules.map((rule, index) => (
                  <option key={rule.id} value={index}>
                    {rule.name}
                  </option>
                ))}
              </select>
              <button type="button" className="btn btn-secondary" onClick={handleAddRule}>
                {t('ai.addRule')}
              </button>
              <button type="button" className="btn btn-secondary" onClick={handleRemoveRule}>
                {t('ai.removeRule')}
              </button>
            </div>

            {currentRule && (
              <div className={styles.editor}>
                <div
                  className={styles.toggleRow}
                  onClick={() => updateRule(selectedRule, { enabled: !currentRule.enabled })}
                >
                  <span className="form-row-label">{t('ai.ruleEnabled')}</span>
                  <div className={`toggle-switch${currentRule.enabled ? ' on' : ''}`}>
                    <div className="toggle-knob" />
                  </div>
                </div>

                <label className={styles.field}>
                  <span className={styles.fieldLabel}>{t('ai.ruleName')}</span>
                  <input
                    className="form-input"
                    value={currentRule.name}
                    onChange={(e) => updateRule(selectedRule, { name: e.target.value })}
                  />
                </label>

                <div className={styles.inlineFields}>
                  <label className={styles.field}>
                    <span className={styles.fieldLabel}>{t('ai.ruleAgent')}</span>
                    <select
                      className="form-select"
                      value={currentRule.agentId}
                      onChange={(e) => updateRule(selectedRule, { agentId: e.target.value })}
                    >
                      {draft.agents.map((agent) => (
                        <option key={agent.id} value={agent.id}>
                          {agent.name}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label className={styles.field}>
                    <span className={styles.fieldLabel}>{t('ai.ruleMode')}</span>
                    <select
                      className="form-select"
                      value={currentRule.mode}
                      onChange={(e) =>
                        updateRule(selectedRule, {
                          mode: e.target.value as AiRule['mode'],
                        })
                      }
                    >
                      <option value="auto">{t('ai.mode.auto')}</option>
                      <option value="options">{t('ai.mode.options')}</option>
                      <option value="suggestion">{t('ai.mode.suggestion')}</option>
                    </select>
                  </label>
                </div>

                <div className={styles.inlineFields}>
                  {currentRule.mode === 'options' && (
                    <label className={styles.field}>
                      <span className={styles.fieldLabel}>{t('ai.ruleOptionsCount')}</span>
                      <input
                        className="form-input"
                        type="number"
                        min={1}
                        max={10}
                        value={currentRule.optionsCount}
                        onChange={(e) =>
                          updateRule(selectedRule, { optionsCount: Number(e.target.value) })
                        }
                      />
                    </label>
                  )}
                  <label className={styles.field}>
                    <span className={styles.fieldLabel}>{t('ai.ruleCooldown')}</span>
                    <input
                      className="form-input"
                      type="number"
                      min={0}
                      max={3600}
                      value={currentRule.cooldownSeconds}
                      onChange={(e) =>
                        updateRule(selectedRule, { cooldownSeconds: Number(e.target.value) })
                      }
                    />
                  </label>
                  <label className={styles.field}>
                    <span className={styles.fieldLabel}>{t('ai.ruleMaxPerMinute')}</span>
                    <input
                      className="form-input"
                      type="number"
                      min={1}
                      max={120}
                      value={currentRule.maxPerMinute}
                      onChange={(e) =>
                        updateRule(selectedRule, { maxPerMinute: Number(e.target.value) })
                      }
                    />
                  </label>
                </div>

                <div className={styles.field}>
                  <span className={styles.fieldLabel}>{t('ai.ruleChannels')}</span>
                  <div className={styles.channelGrid}>
                    {TRIGGER_CHANNEL_GROUPS.map((group) => {
                      const checked = group.types.every((type) =>
                        currentRule.channelTypes.includes(type),
                      );
                      return (
                        <label key={group.key} className={styles.channelItem}>
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => {
                              const next = checked
                                ? currentRule.channelTypes.filter(
                                    (type) => !group.types.includes(type),
                                  )
                                : Array.from(new Set([...currentRule.channelTypes, ...group.types]));
                              updateRule(selectedRule, { channelTypes: next });
                            }}
                          />
                          <span>{t(group.key)}</span>
                        </label>
                      );
                    })}
                  </div>
                </div>
              </div>
            )}
          </>
        )}

        {draft.rules.length === 0 && (
          <button type="button" className="btn btn-secondary" onClick={handleAddRule}>
            {t('ai.addRule')}
          </button>
        )}
      </div>

      <div className={styles.footer}>
        <button type="button" className="btn btn-primary" disabled={saving} onClick={handleSave}>
          {t('ai.saveSettings')}
        </button>
        {saved && <span className={styles.saved}>{t('ai.settingsSaved')}</span>}
      </div>
    </div>
  );
}
