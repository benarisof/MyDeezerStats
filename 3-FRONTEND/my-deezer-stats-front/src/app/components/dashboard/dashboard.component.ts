import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { Router } from '@angular/router';

import { DashboardService } from '../../services/dashboard.service';
import { LoginService } from '../../services/login.service';
import { Album, Artist, Track, Recent } from "../../models/dashboard.models";
import { finalize, forkJoin, Subscription } from 'rxjs';
import { switchMap, tap } from 'rxjs/operators';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  imports: [CommonModule] 
})
export class DashboardComponent implements OnInit, OnDestroy {
  topAlbums: Album[] = [];
  topArtists: Artist[] = [];
  topTracks: Track[] = [];
  recentListens: Recent[] = [];
  
  isLoading: boolean = false;
  errorMessage: string = '';
  
  private periodSubscription: Subscription | undefined;

  constructor(
    private loginService: LoginService,
    private router: Router,
    private dashboardService: DashboardService
  ) {}

ngOnInit(): void {
  if (!this.loginService.isAuthenticated()) {
    this.router.navigate(['/login']);
    return;
  }

  this.periodSubscription = this.dashboardService.period$.pipe(
    // 1. On active le chargement dès qu'une nouvelle période arrive
    tap(() => {
        this.isLoading = true;
        this.errorMessage = '';
    }),
    switchMap(period => {
      // 2. On lance les appels
      return forkJoin([
        this.dashboardService.getTopAlbums(period, 7), 
        this.dashboardService.getTopArtists(period, 7),
        this.dashboardService.getTopTracks(period, 7),
        this.dashboardService.getRecentListens(period)
      ]).pipe(
        finalize(() => this.isLoading = false)
      );
    })
  ).subscribe({
    next: ([albums, artists, tracks, recentListens]) => {
      this.topAlbums = albums;
      this.topArtists = artists;
      this.topTracks = tracks;
      this.recentListens = recentListens;
    },
    error: (err) => {
      this.errorMessage = 'Erreur lors du chargement des données';
      console.error(err);
    }
  });
}

  ngOnDestroy(): void {
    if (this.periodSubscription) {
      this.periodSubscription.unsubscribe();
    }
  }

  goToTop(type: 'album' | 'artist' | 'track') {
    this.router.navigate(['/top', type]);
  }

  navigateToDetail(type: 'album' | 'artist', item: any): void {
    let identifier = '';
    switch (type) {
      case 'album':
        const albumTitle = item.title ?? '';
        const albumArtist = item.artist ?? '';
        identifier = albumTitle && albumArtist ? `${albumTitle}|${albumArtist}` : '';
        break;
      case 'artist':
        identifier = item.artist ?? '';
        break;
    }
  
    if (identifier) {
      this.router.navigate(['/detail', type], { 
        queryParams: { identifier } 
      });
    }
  }
}