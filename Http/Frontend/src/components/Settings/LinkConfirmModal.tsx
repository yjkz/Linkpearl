import type { Dispatch, SetStateAction } from 'react';
import { useTranslation } from '../../i18n';
import styles from './LinkConfirmModal.module.css';

interface Props {
  confirmLink: string;
  trustDomain: boolean;
  setTrustDomain: Dispatch<SetStateAction<boolean>>;
  setTrustedDomains: Dispatch<SetStateAction<Set<string>>>;
  onClose: () => void;
}

export function LinkConfirmModal({
  confirmLink,
  trustDomain,
  setTrustDomain,
  setTrustedDomains,
  onClose,
}: Props) {
  const { t } = useTranslation();
  const handleConfirm = () => {
    if (trustDomain) {
      try {
        const { hostname } = new URL(confirmLink);
        setTrustedDomains((prev) => new Set(prev).add(hostname));
      } catch {
        /* invalid URL — skip saving domain */
      }
    }
    window.open(confirmLink, '_blank', 'noopener,noreferrer');
    onClose();
  };

  let hostname = '';
  try {
    hostname = new URL(confirmLink).hostname;
  } catch {
    /* invalid URL — hostname stays empty */
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className={styles['modal-content']} onClick={(e) => e.stopPropagation()}>
        <div className={styles['modal-header']}>
          <h3>{t('link.title')}</h3>
          <button className={styles['close-btn']} onClick={onClose}>
            &times;
          </button>
        </div>
        <div className={styles['modal-body']}>
          <p>{t('link.description')}</p>
          <div className={styles['link-preview-box']}>{confirmLink}</div>
          <p className={styles['modal-warning']}>{t('link.warning')}</p>
          <label className={styles['trust-domain-checkbox']}>
            <input
              type="checkbox"
              checked={trustDomain}
              onChange={(e) => setTrustDomain(e.target.checked)}
            />
            <span>{t('link.dontAsk', { host: hostname })}</span>
          </label>
        </div>
        <div className={styles['modal-footer']}>
          <button className="btn btn-secondary" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button className="btn btn-primary" onClick={handleConfirm}>
            {t('link.open')}
          </button>
        </div>
      </div>
    </div>
  );
}
