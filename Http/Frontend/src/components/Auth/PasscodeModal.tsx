import { useState, useRef, useEffect } from 'react';
import type { KeyboardEvent } from 'react';
import clsx from 'clsx';
import { Lock } from 'lucide-react';
import type { AuthResult, AuthStatus } from '../../hooks/useAuth';
import { useTranslation } from '../../i18n';
import styles from './PasscodeModal.module.css';

interface Props {
  status: AuthStatus;
  onAuthenticate: (passcode: string) => Promise<AuthResult>;
}

export function PasscodeModal({ status, onAuthenticate }: Props) {
  const { t } = useTranslation();
  const [passcode, setPasscode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [formRevealed, setFormRevealed] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Reveal the form with a CSS transition once a response is received
  useEffect(() => {
    if (status === 'unauthenticated' || status === 'not-configured') {
      const raf = requestAnimationFrame(() => setFormRevealed(true));
      return () => {
        cancelAnimationFrame(raf);
        setFormRevealed(false);
      };
    }
  }, [status]);

  // Focus input after the reveal transition settles
  useEffect(() => {
    if (!formRevealed || status !== 'unauthenticated') return;
    const t = setTimeout(() => inputRef.current?.focus(), 360);
    return () => clearTimeout(t);
  }, [formRevealed, status]);

  const handleSubmit = async () => {
    if (passcode.length < 4 || isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    const result = await onAuthenticate(passcode);
    setIsSubmitting(false);
    if (result === 'wrong') {
      setError(t('auth.incorrect'));
      setPasscode('');
      setTimeout(() => inputRef.current?.focus(), 0);
    } else if (result === 'not-configured') {
      setError(t('auth.noPasscode'));
    } else if (result === 'error') {
      setError(t('auth.connectionFailed'));
    }
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') handleSubmit();
  };

  const handleChange = (value: string) => {
    const digits = value.replace(/\D/g, '').slice(0, 8);
    setPasscode(digits);
    if (error) setError(null);
  };

  const isConnecting = status === 'loading' || status === 'unreachable';
  const isNotConfigured = status === 'not-configured';

  return (
    <div className="modal-overlay">
      <div className={styles.card}>
        <div className={styles.lockIcon}>
          <Lock size={36} strokeWidth={1.8} aria-hidden />
        </div>

        <h1 className={styles.title}>Linkpearl</h1>

        {isConnecting && (
          <div className={styles.connecting}>
            <p className={styles.connectingText}>
              {status === 'unreachable' ? t('auth.couldNotReach') : t('auth.connecting')}
            </p>
            <div className={styles.connectingDots}>
              <span />
              <span />
              <span />
            </div>
          </div>
        )}

        <div className={clsx(styles.formBody, formRevealed && styles.formRevealed)}>
          <div className={styles.formBodyInner}>
            <p className={styles.subtitle}>
              {isNotConfigured ? t('auth.pluginConfigRequired') : t('auth.enterPasscode')}
            </p>

            {!isNotConfigured && (
              <input
                ref={inputRef}
                type="password"
                inputMode="numeric"
                pattern="[0-9]*"
                maxLength={8}
                className={styles.input}
                value={passcode}
                onChange={(e) => handleChange(e.target.value)}
                onKeyDown={handleKeyDown}
                onBlur={() => {
                  if (formRevealed) inputRef.current?.focus();
                }}
                placeholder="••••"
                disabled={isSubmitting}
                autoComplete="off"
              />
            )}

            {error && <p className={styles.error}>{error}</p>}

            {!isNotConfigured && (
              <button
                className={styles.button}
                onClick={handleSubmit}
                disabled={passcode.length < 4 || isSubmitting}
              >
                {isSubmitting ? t('auth.verifying') : t('auth.unlock')}
              </button>
            )}

            {isNotConfigured && (
              <p className={styles.hint}>{t('auth.hint')}</p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
