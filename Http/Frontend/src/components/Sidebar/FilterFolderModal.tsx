import { useState } from 'react';
import clsx from 'clsx';
import styles from './FilterFolderModal.module.css';
import { FILTER_NAME_REGEX } from '../../types/filter';
import { useTranslation } from '../../i18n';

interface Props {
  initialName?: string;
  existingNames: string[]; // existing folder names, excluding this folder
  onSave: (name: string) => void;
  onClose: () => void;
}

export function FilterFolderModal({ initialName = '', existingNames, onSave, onClose }: Props) {
  const { t } = useTranslation();
  const [name, setName] = useState(initialName);
  const [error, setError] = useState('');

  const validate = (value: string): string => {
    if (!FILTER_NAME_REGEX.test(value)) return t('filter.nameError');
    if (existingNames.includes(value)) return t('folder.nameExists');
    return '';
  };

  const handleSave = () => {
    const err = validate(name);
    if (err) {
      setError(err);
      return;
    }
    onSave(name);
  };

  const isEdit = initialName !== '';

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <h2 className={styles.title}>{isEdit ? t('folder.renameTitle') : t('folder.newTitle')}</h2>

        <label className={styles.label}>
          {t('folder.name')}
          <input
            className={clsx(styles.input, error && styles['input-error'])}
            type="text"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              setError('');
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter') handleSave();
              if (e.key === 'Escape') onClose();
            }}
            autoFocus
            maxLength={32}
            placeholder={t('folder.namePlaceholder')}
          />
          {error && <span className={styles.error}>{error}</span>}
          {!error && <span className={styles.hint}>{t('folder.nameHint')}</span>}
        </label>

        <div className={styles.actions}>
          <button className="btn btn-secondary" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button className="btn btn-primary" onClick={handleSave}>
            {isEdit ? t('common.rename') : t('common.create')}
          </button>
        </div>
      </div>
    </div>
  );
}
