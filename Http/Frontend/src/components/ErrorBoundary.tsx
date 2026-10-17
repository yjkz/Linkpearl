import { ErrorBoundary as ReactErrorBoundary } from 'react-error-boundary';
import type { ReactNode } from 'react';
import { useTranslation } from '../i18n';

function ErrorFallback() {
  const { t } = useTranslation();
  return (
    <div className="messages-empty">
      <div className="messages-empty-icon">⚠️</div>
      <div>{t('error.rendering')}</div>
      <button
        className="btn btn-primary"
        style={{ marginTop: '12px' }}
        onClick={() => window.location.reload()}
      >
        {t('common.reload')}
      </button>
    </div>
  );
}

export function ErrorBoundary({ children }: { children: ReactNode }) {
  return (
    <ReactErrorBoundary
      FallbackComponent={ErrorFallback}
      onError={(error, info) => console.error(error, info)}
    >
      {children}
    </ReactErrorBoundary>
  );
}
