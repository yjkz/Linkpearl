import styles from './SymbolsTab.module.css';
import { FFXIV_SPECIAL_CHARS, FFXIV_CHAR_CATEGORIES } from '../../constants/ffxivSpecialChars';
import { useTranslation, type TranslationKey } from '../../i18n';

const CATEGORY_KEYS: Record<string, TranslationKey> = {
  IME: 'symbols.category.ime',
  Time: 'symbols.category.time',
  UI: 'symbols.category.ui',
  Sign: 'symbols.category.sign',
  Mouse: 'symbols.category.mouse',
  Battle: 'symbols.category.battle',
  Enemy: 'symbols.category.enemy',
  Instance: 'symbols.category.instance',
  Party: 'symbols.category.party',
};

// Pre-computed once at module load — derived from constants that never change
const SYMBOL_GROUPS = FFXIV_CHAR_CATEGORIES.slice(1)
  .map((cat) => ({
    category: cat,
    chars: FFXIV_SPECIAL_CHARS.filter((c) => c.category === cat),
  }))
  .filter((g) => g.chars.length > 0);

interface Props {
  onSelect: (char: string) => void;
}

export function SymbolsTab({ onSelect }: Props) {
  const { t } = useTranslation();
  return (
    <div className={styles.scroll}>
      {SYMBOL_GROUPS.map((g) => (
        <div key={g.category} className={styles.section}>
          <div className={styles['section-label']}>
            {CATEGORY_KEYS[g.category] ? t(CATEGORY_KEYS[g.category]) : g.category}
          </div>
          <div className={styles.grid}>
            {g.chars.map((item, i) => (
              <button
                key={i}
                type="button"
                className={styles['char-btn']}
                title={item.label}
                onClick={() => onSelect(item.char)}
              >
                {item.char}
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
