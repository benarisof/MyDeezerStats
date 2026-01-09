import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { DEFAULT_PERIOD } from '../models/period.model';

@Injectable({
  providedIn: 'root'
})
export class PeriodService {
  private periodSubject = new BehaviorSubject<string>(DEFAULT_PERIOD);
  public period$ = this.periodSubject.asObservable();

  updatePeriod(period: string): void {
    this.periodSubject.next(period);
  }

  getDateRange(period: string): { from: Date; to: Date } {
    const to = new Date();
    let from = new Date();
    to.setHours(23, 59, 59, 999);
    
    const year = new Date().getFullYear();

    switch (period) {
      case '30':
      case '90':
      case '180': {
        const days = parseInt(period, 10);
        from.setDate(to.getDate() - days);
        from.setHours(0, 0, 0, 0);
        break;
      }

      case 'thisYear': {
        from = new Date(year, 0, 1, 0, 0, 0);
        break;
      }

      case 'lastYear': {
        from = new Date(year - 1, 0, 1, 0, 0, 0);
        const lastYearTo = new Date(year - 1, 11, 31, 23, 59, 59);
        return { from, to: lastYearTo };
      }

      case 'allTime':
      default: {
        from = new Date(2000, 0, 1, 0, 0, 0);
        break;
      }
    }

    return { from, to };
  }

  getAvailablePeriods() {
    return [
      { value: '30', label: '30 derniers jours' },
      { value: '90', label: '90 derniers jours' },
      { value: '180', label: '180 derniers jours' },
      { value: 'thisYear', label: 'Cette année' },
      { value: 'lastYear', label: 'Année dernière' },
      { value: 'allTime', label: 'Toute la période' }
    ];
  }

  getCurrentPeriod(): string {
    return this.periodSubject.getValue();
  }

  resetToDefault(): void {
    this.periodSubject.next(DEFAULT_PERIOD);
  }
}