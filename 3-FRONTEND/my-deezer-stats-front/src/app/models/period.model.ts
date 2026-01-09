export const PERIODS = [
  { value: '30', label: '30 derniers jours' },
  { value: '90', label: '90 derniers jours' },
  { value: '180', label: '180 derniers jours' },
  { value: 'thisYear', label: 'Cette année' },
  { value: 'lastYear', label: 'Année dernière' },
  { value: 'allTime', label: 'Depuis le début' }
] as const;

export type PeriodValue = typeof PERIODS[number]['value'];
export type PeriodLabel = typeof PERIODS[number]['label'];

export const DEFAULT_PERIOD: PeriodValue = 'thisYear';

export interface PeriodOption {
  value: PeriodValue;
  label: PeriodLabel;
}

// Fonction utilitaire pour valider une période
export function isValidPeriod(period: string): period is PeriodValue {
  return PERIODS.some(p => p.value === period);
}