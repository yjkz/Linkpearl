import { beforeEach, describe, expect, it } from 'vitest';
import { act } from '@testing-library/react';
import { useAiStore } from './aiStore';
import type { AiSuggestion } from '../types/ai';

function makeSuggestion(id: string): AiSuggestion {
  return {
    id,
    ruleId: 'rule',
    ruleName: 'Rule',
    agentName: 'Agent',
    mode: 'options',
    channelType: 14,
    channelLabel: 'Party',
    senderName: 'Alice',
    senderWorld: 'Aegis',
    sourceText: 'hello',
    candidates: ['hi'],
    createdAt: Date.now(),
  };
}

describe('aiStore', () => {
  beforeEach(() => {
    act(() => useAiStore.setState({ suggestions: [], activity: [], error: null }));
  });

  it('inserts suggestions newest first and replaces duplicates', () => {
    act(() => useAiStore.getState().upsertSuggestion(makeSuggestion('a')));
    act(() => useAiStore.getState().upsertSuggestion(makeSuggestion('b')));
    expect(useAiStore.getState().suggestions.map((s) => s.id)).toEqual(['b', 'a']);

    act(() => useAiStore.getState().upsertSuggestion(makeSuggestion('a')));
    expect(useAiStore.getState().suggestions.map((s) => s.id)).toEqual(['a', 'b']);
  });

  it('removes suggestions by id', () => {
    act(() => useAiStore.getState().upsertSuggestion(makeSuggestion('a')));
    act(() => useAiStore.getState().removeSuggestion('a'));
    expect(useAiStore.getState().suggestions).toHaveLength(0);
  });

  it('caps the activity log at 50 entries', () => {
    act(() => {
      for (let i = 0; i < 60; i++) {
        useAiStore.getState().addActivity({ timestamp: i, kind: 'info', message: `m${i}` });
      }
    });
    const activity = useAiStore.getState().activity;
    expect(activity).toHaveLength(50);
    expect(activity[0].message).toBe('m59');
  });
});
