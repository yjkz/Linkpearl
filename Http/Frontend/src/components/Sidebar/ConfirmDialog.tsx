import clsx from 'clsx';
import styles from './ConfirmDialog.module.css';
import { useTranslation } from '../../i18n';

interface Props {
  title: string;
  body: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onClose: () => void;
}

export function ConfirmDialog({ title, body, confirmLabel, onConfirm, onClose }: Props) {
  const { t } = useTranslation();
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className={styles.dialog} onClick={(e) => e.stopPropagation()}>
        <p className={styles.title}>{title}</p>
        <p className={styles.body}>{body}</p>
        <div className={styles.actions}>
          <button className={clsx('btn btn-secondary', styles['btn-cancel'])} onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button className={clsx('btn', styles['btn-danger'])} onClick={onConfirm}>
            {confirmLabel ?? t('common.delete')}
          </button>
        </div>
      </div>
    </div>
  );
}
