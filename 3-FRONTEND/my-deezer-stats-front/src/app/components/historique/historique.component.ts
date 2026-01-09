import { Component, inject, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { DashboardService } from '../../services/apiService/deezerApi.service';
import { Recent } from '../../models/dashboard.models';

@Component({
  selector: 'app-historique',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './historique.component.html',
  styleUrls: ['./historique.component.scss']
})
export class HistoriqueComponent implements OnInit {
  private destroyRef = inject(DestroyRef);

  items: Recent[] = [];

  isLoading = false;
  hoveredIndex: number = -1;

  constructor(
    private dashboardService: DashboardService,
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading = true;

    const observable$: Observable<Recent[]> = this.dashboardService.getRecentListens();
    
    observable$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data: Recent[]) => {
        this.items = data;
        this.isLoading = false;
      },
      error: (err: any) => {
        console.error(err);
        this.isLoading = false;
      }
    });
  }

  getTitle(): string {
    return 'Historique des écoutes';
  }

  getImage(item: Recent): string {
    return item.trackUrl || 'assets/default-cover.jpg';
  }
}