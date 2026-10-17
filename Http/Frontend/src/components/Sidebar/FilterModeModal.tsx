import { useState } from 'react';
import styles from './FilterModeModal.module.css';
import type { FilterMode } from '../../hooks/useFilterMode';
import { useTranslation } from '../../i18n';

interface Props {
  filterMode: FilterMode;
  onEnable: (copyFromGlobal: boolean) => Promise<void>;
  onDisable: () => Promise<void>;
  onClose: () => void;
}

export function FilterModeModal({ filterMode, onEnable, onDisable, onClose }: Props) {
  const { t } = useTranslation();
  const [overwrite, setOverwrite] = useState(false);
  const [busy, setBusy] = useState(false);

  const handleEnable = async () => {
    setBusy(true);
    try {
      const copyFromGlobal = !filterMode.hasExistingFile || overwrite;
      await onEnable(copyFromGlobal);
      onClose();
    } finally {
      setBusy(false);
    }
  };

  const handleDisable = async () => {
    setBusy(true);
    try {
      await onDisable();
      onClose();
    } finally {
      setBusy(false);
    }
  };

  if (filterMode.isPersonal) {
    return (
      <div className="modal-overlay" onClick={onClose}>
        <div className={styles.dialog} onClick={(e) => e.stopPropagation()}>
          <p className={styles.title}>{t('filterMode.sharedTitle')}</p>
          <p className={styles.body}>{t('filterMode.sharedBody')}</p>
          <div className={styles.actions}>
            <button className="btn btn-secondary" onClick={onClose} disabled={busy}>
              {t('common.cancel')}
            </button>
            <button className="btn btn-primary" onClick={handleDisable} disabled={busy}>
              {busy ? t('common.switching') : t('filterMode.useShared')}
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className={styles.dialog} onClick={(e) => e.stopPropagation()}>
        <p className={styles.title}>{t('filterMode.personalTitle')}</p>
        <p className={styles.body}>
          {filterMode.hasExistingFile
            ? t('filterMode.personalBodyExisting')
            : t('filterMode.personalBodyNew')}
        </p>

        {filterMode.hasExistingFile && (
          <label className={styles.toggle}>
            <input
              type="checkbox"
              checked={overwrite}
              onChange={(e) => setOverwrite(e.target.checked)}
            />
            <span className={styles['toggle-label']}>{t('filterMode.overwrite')}</span>
          </label>
        )}

        <div className={styles.actions}>
          <button className="btn btn-secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button className="btn btn-primary" onClick={handleEnable} disabled={busy}>
            {busy ? t('common.switching') : t('filterMode.usePersonal')}
          </button>
        </div>
      </div>
    </div>
  );
}
