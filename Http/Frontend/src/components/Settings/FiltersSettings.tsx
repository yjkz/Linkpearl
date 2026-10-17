import { useState } from 'react';
import clsx from 'clsx';
import styles from './FiltersSettings.module.css';
import { ImportFiltersModal } from './ImportFiltersModal';
import { ConfirmDialog } from '../Sidebar/ConfirmDialog';
import type { FilterMode } from '../../hooks/useFilterMode';
import { useSettingsStore } from '../../store/settingsStore';
import { useTranslation } from '../../i18n';

interface Props {
  filterMode: FilterMode | null;
  onDeletePersonalFilters: (key: string) => Promise<void>;
}

export function FiltersSettings({ filterMode, onDeletePersonalFilters }: Props) {
  const { t } = useTranslation();
  const currentFilters = useSettingsStore((s) => s.filters);
  const currentFolders = useSettingsStore((s) => s.folders);
  const setFilters = useSettingsStore((s) => s.setFilters);
  const setFolders = useSettingsStore((s) => s.setFolders);

  const [showImportModal, setShowImportModal] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<string | null>(null);

  const personalFilterCharacters = filterMode?.personalFilterCharacters ?? [];

  return (
    <>
      <div className="form-section">
        <div className="form-section-title">{t('filtersSettings.import')}</div>
        <p className={styles.description}>{t('filtersSettings.importDescription')}</p>
        <button className="btn btn-primary" onClick={() => setShowImportModal(true)}>
          {t('filtersSettings.importButton')}
        </button>
      </div>

      <div className="form-section">
        <div className="form-section-title">{t('filtersSettings.personal')}</div>
        {personalFilterCharacters.length === 0 ? (
          <p className={styles.empty}>{t('filtersSettings.nonePersonal')}</p>
        ) : (
          <>
            <p className={styles.description}>{t('filtersSettings.personalDescription')}</p>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th className={styles.th}>{t('filtersSettings.character')}</th>
                  <th className={styles['th-status']}>{t('filtersSettings.status')}</th>
                  <th className={styles['th-action']} />
                </tr>
              </thead>
              <tbody>
                {personalFilterCharacters.map((key) => {
                  const isCurrent = filterMode?.characterKey === key;
                  return (
                    <tr key={key}>
                      <td className={styles['td-key']}>{key}</td>
                      <td className={styles['td-status']}>
                        {isCurrent ? (
                          <span className={styles['badge-current']}>{t('filtersSettings.current')}</span>
                        ) : (
                          <span className={styles['badge-personal']}>
                            {t('filtersSettings.personalBadge')}
                          </span>
                        )}
                      </td>
                      <td className={styles['td-action']}>
                        <button
                          className={clsx('btn btn-secondary', styles['btn-delete'])}
                          onClick={() => setPendingDelete(key)}
                        >
                          {t('common.delete')}
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </>
        )}
      </div>

      {showImportModal && (
        <ImportFiltersModal
          currentFilters={currentFilters}
          currentFolders={currentFolders}
          onImport={(nf, nfold) => {
            setFilters(nf);
            setFolders(nfold);
          }}
          onClose={() => setShowImportModal(false)}
        />
      )}

      {pendingDelete && (
        <ConfirmDialog
          title={t('filtersSettings.deleteTitle', { name: pendingDelete })}
          body={t('filtersSettings.deleteBody')}
          confirmLabel={t('common.delete')}
          onConfirm={async () => {
            await onDeletePersonalFilters(pendingDelete);
            setPendingDelete(null);
          }}
          onClose={() => setPendingDelete(null)}
        />
      )}
    </>
  );
}
