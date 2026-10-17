import clsx from 'clsx';
import { X } from 'lucide-react';
import styles from './SecuritySettings.module.css';
import { BUILT_IN_TRUSTED_DOMAINS } from '../../constants/trustedDomains';
import { useSettingsStore } from '../../store/settingsStore';
import { useTranslation } from '../../i18n';

export function SecuritySettings() {
  const { t } = useTranslation();
  const trustedDomains = useSettingsStore((s) => s.trustedDomains);
  const setTrustedDomains = useSettingsStore((s) => s.setTrustedDomains);
  const handleRemoveDomain = (domain: string) => {
    setTrustedDomains((prev) => {
      const next = new Set(prev);
      next.delete(domain);
      return next;
    });
  };

  return (
    <div className="form-section">
      <div className="form-section-title">{t('security.trustedDomains')}</div>
      <p>{t('security.trustedDescription')}</p>

      <div className={styles['domain-list']}>
        {Array.from(BUILT_IN_TRUSTED_DOMAINS).map((domain) => (
          <div key={domain} className={clsx(styles['domain-item'], styles['domain-item-builtin'])}>
            <span className={styles['domain-name']}>{domain}</span>
            <span className={styles['builtin-badge']}>{t('security.builtIn')}</span>
          </div>
        ))}
        {trustedDomains.size === 0 && (
          <div className={styles['empty-state']}>{t('security.noCustom')}</div>
        )}
        {Array.from(trustedDomains).map((domain) => (
          <div key={domain} className={styles['domain-item']}>
            <span className={styles['domain-name']}>{domain}</span>
            <button
              className={styles['remove-domain-btn']}
              onClick={() => handleRemoveDomain(domain)}
              aria-label={t('common.remove')}
            >
              <X size={14} strokeWidth={2.5} aria-hidden />
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}
