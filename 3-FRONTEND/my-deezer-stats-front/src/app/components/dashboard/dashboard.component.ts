import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardService } from '../../services/apiService/deezerApi.service';
import { LoginService } from '../../services/login.service';
import { NavigationService } from '../../services/navigation.service';
import { Album, Artist, Track, Recent } from "../../models/dashboard.models";
import { finalize, forkJoin } from 'rxjs';
import { switchMap, tap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  imports: [CommonModule]
})
export class DashboardComponent implements OnInit {
  private destroyRef = inject(DestroyRef);

  topAlbums: Album[] = [];
  topArtists: Artist[] = [];
  topTracks: Track[] = [];
  recentListens: Recent[] = [];
  
  isLoading: boolean = false;
  errorMessage: string = '';

  constructor(
    private loginService: LoginService,
    private dashboardService: DashboardService,
    private navigationService: NavigationService
  ) {}

  ngOnInit(): void {
    if (!this.loginService.isAuthenticated()) {
      this.navigationService.navigateToLogin();
      return;
    }
    this.loadDashboardData();
  }

  private loadDashboardData(): void {
    this.dashboardService.period$.pipe(
      tap(() => {
        this.isLoading = true;
        this.errorMessage = '';
      }),
      switchMap(period => {
        const data = this.dashboardService.getDashboardData(period);
        return forkJoin(data).pipe(
          finalize(() => this.isLoading = false)
        );
      }),
      takeUntilDestroyed(this.destroyRef) 
    ).subscribe({
      next: ({ topAlbums, topArtists, topTracks }) => {
        this.topAlbums = topAlbums;
        this.topArtists = topArtists;
        this.topTracks = topTracks;
      },
      error: (err) => {
        this.errorMessage = err.message || 'Erreur lors du chargement des données';
        console.error('Dashboard error:', err);
      }
    });
  }

  goToTop(type: 'album' | 'artist' | 'track'): void {
    this.navigationService.navigateToTop(type);
  }

  navigateToDetail(type: 'album' | 'artist', item: any): void {
    this.navigationService.navigateFromDashboardItem(item, type);
  }

  scroll(elementId: string, direction: number): void {
    const container = document.getElementById(elementId);
    if (container) {
      const scrollAmount = container.clientWidth * 0.8;
      container.scrollBy({
        left: direction * scrollAmount,
        behavior: 'smooth'
      });
    }
  }
}