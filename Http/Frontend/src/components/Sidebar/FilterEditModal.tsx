import { useState } from 'react';
import clsx from 'clsx';
import styles from './FilterEditModal.module.css';
import type { CustomFilter } from '../../types/filter';
import { FILTER_NAME_REGEX } from '../../types/filter';
import { TRACKED_CHANNEL_TYPES, ALL_CHANNELS, CHANNEL_GROUPS } from '../../constants/channels';
import { useTranslation } from '../../i18n';
import { useChannelLabel, useChannelLabelByPrefix } from '../../i18n/channelLabels';

// Types absorbed into groups — rendered as a single grouped checkbox
const GROUPED_TYPE_IDS = new Set(CHANNEL_GROUPS.flatMap((g) => g.types));

type DisplayItem =
  | { kind: 'single'; typeId: number }
  | { kind: 'group'; label: string; types: number[] };

// Build ordered display list: groups replace their member types at the position of the first member
const DISPLAY_ITEMS: DisplayItem[] = (() => {
  const items: DisplayItem[] = [];
  const usedGroupLabels = new Set<string>();
  for (const typeId of TRACKED_CHANNEL_TYPES) {
    if (!GROUPED_TYPE_IDS.has(typeId)) {
      items.push({ kind: 'single', typeId });
    } else {
      const group = CHANNEL_GROUPS.find((g) => g.types.includes(typeId));
      if (group && !usedGroupLabels.has(group.label)) {
        usedGroupLabels.add(group.label);
        items.push({ kind: 'group', label: group.label, types: group.types });
      }
    }
  }
  return items;
})();

interface Props {
  initial: CustomFilter | null; // null = create new
  existingFilterNames: string[]; // existing filter names, excluding this filter
  existingFolderNames: string[]; // folder names used for duplicate check
  onSave: (filter: CustomFilter, oldName: string) => void;
  onClose: () => void;
}

export function FilterEditModal({
  initial,
  existingFilterNames,
  existingFolderNames,
  onSave,
  onClose,
}: Props) {
  const { t } = useTranslation();
  const channelLabel = useChannelLabel();
  const channelLabelByPrefix = useChannelLabelByPrefix();
  const isEdit = initial !== null;

  const [name, setName] = useState(initial?.name ?? '');
  const [notifyUnread, setNotifyUnread] = useState(initial?.notifyUnread ?? false);
  const [defaultSendPrefix, setDefaultSendPrefix] = useState<string>(
    initial?.defaultSendPrefix ?? '',
  );
  const [showChannelTypes, setShowChannelTypes] = useState<number[]>(
    initial?.showChannelTypes ?? [],
  );
  const [nameError, setNameError] = useState('');

  const validateName = (value: string): string => {
    if (!FILTER_NAME_REGEX.test(value)) return t('filter.nameError');
    if (existingFilterNames.includes(value)) return t('filter.nameExists');
    if (existingFolderNames.includes(value)) return t('filter.folderNameExists');
    return '';
  };

  const toggleType = (typeId: number) => {
    setShowChannelTypes((prev) =>
      prev.includes(typeId) ? prev.filter((t) => t !== typeId) : [...prev, typeId],
    );
  };

  const toggleGroup = (types: number[]) => {
    const allSelected = types.every((t) => showChannelTypes.includes(t));
    if (allSelected) {
      setShowChannelTypes((prev) => prev.filter((t) => !types.includes(t)));
    } else {
      setShowChannelTypes((prev) => [...prev, ...types.filter((t) => !prev.includes(t))]);
    }
  };

  const handleSave = () => {
    const err = validateName(name);
    if (err) {
      setNameError(err);
      return;
    }
    onSave(
      {
        name,
        showChannelTypes,
        defaultSendPrefix: defaultSendPrefix === '' ? null : defaultSendPrefix,
        notifyUnread,
      },
      initial?.name ?? name,
    );
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <h2 className={styles.title}>{isEdit ? t('filter.editTitle') : t('filter.newTitle')}</h2>

        {/* ── Filter Name ── */}
        <div className="form-field">
          <label className="form-field-label">{t('filter.name')}</label>
          <input
            className={clsx('form-input', nameError && 'form-input-error')}
            type="text"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              setNameError('');
            }}
            onKeyDown={(e) => {
              if (e.key === 'Escape') onClose();
            }}
            autoFocus
            maxLength={32}
            placeholder={t('filter.namePlaceholder')}
          />
          {nameError && <span className="form-error">{nameError}</span>}
          {!nameError && <span className="form-hint">{t('filter.nameHint')}</span>}
        </div>

        {/* ── Notification ── */}
        <div className={clsx('form-section', styles['modal-section'])}>
          <div className="form-section-title">{t('filter.notification')}</div>
          <div className="form-row">
            <span className="form-row-label">{t('filter.badgeUnread')}</span>
            <button
              className={clsx('toggle-switch', notifyUnread && 'on')}
              role="switch"
              aria-checked={notifyUnread}
              onClick={() => setNotifyUnread((v) => !v)}
            >
              <div className="toggle-knob" />
            </button>
          </div>
        </div>

        {/* ── Default Send Channel ── */}
        <div className={clsx('form-section', styles['modal-section'])}>
          <div className="form-section-title">{t('filter.defaultSendChannel')}</div>
          <select
            className="form-select"
            value={defaultSendPrefix}
            onChange={(e) => setDefaultSendPrefix(e.target.value)}
          >
            <option value="">{t('filter.syncWithGame')}</option>
            {ALL_CHANNELS.map((ch) => (
              <option key={ch.prefix} value={ch.prefix}>
                {channelLabelByPrefix(ch.prefix)}
              </option>
            ))}
          </select>
          <span className="form-hint">{t('filter.syncHint')}</span>
        </div>

        {/* ── Show Channel Types ── */}
        <div className={clsx('form-section', styles['modal-section'])}>
          <div className={styles['section-header']}>
            <div className="form-section-title">{t('filter.showChannelTypes')}</div>
            <div className={styles['select-all-btns']}>
              <button
                className={styles['text-btn']}
                onClick={() => setShowChannelTypes([...TRACKED_CHANNEL_TYPES])}
              >
                {t('filter.selectAll')}
              </button>
              <span className={styles.divider}>|</span>
              <button className={styles['text-btn']} onClick={() => setShowChannelTypes([])}>
                {t('filter.deselectAll')}
              </button>
            </div>
          </div>
          <div className={styles['checkbox-grid']}>
            {DISPLAY_ITEMS.map((item) => {
              if (item.kind === 'single') {
                return (
                  <label key={item.typeId} className={styles['checkbox-item']}>
                    <input
                      type="checkbox"
                      checked={showChannelTypes.includes(item.typeId)}
                      onChange={() => toggleType(item.typeId)}
                    />
                    <span>{channelLabel(item.typeId)}</span>
                  </label>
                );
              }
              const allChecked = item.types.every((t) => showChannelTypes.includes(t));
              const someChecked = item.types.some((t) => showChannelTypes.includes(t));
              return (
                <label key={item.label} className={styles['checkbox-item']}>
                  <input
                    type="checkbox"
                    checked={allChecked}
                    ref={(el) => {
                      if (el) el.indeterminate = someChecked && !allChecked;
                    }}
                    onChange={() => toggleGroup(item.types)}
                  />
                  <span>{item.label === 'Tell' ? t('channel.tell') : channelLabel(item.types[0])}</span>
                </label>
              );
            })}
          </div>
        </div>

        {/* ── Actions ── */}
        <div className="form-actions">
          <button className="btn btn-secondary" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button className="btn btn-primary" onClick={handleSave}>
            {isEdit ? t('common.save') : t('common.create')}
          </button>
        </div>
      </div>
    </div>
  );
}
