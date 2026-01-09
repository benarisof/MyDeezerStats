import { Component, inject, Input, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Observable, combineLatest } from 'rxjs';
import { filter, debounceTime } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { DashboardService } from '../../services/apiService/deezerApi.service';
import { NavigationService } from '../../services/navigation.service'; 
import { Album, Artist, Track } from '../../models/dashboard.models';
import { PERIODS, DEFAULT_PERIOD, isValidPeriod, PeriodValue } from '../../models/period.model';

@Component({
  selector: 'app-top-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './top-list.component.html',
  styleUrls: ['./top-list.component.scss']
})
export class TopListComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private route = inject(ActivatedRoute);

  @Input() type: 'album' | 'artist' | 'track' = 'album';
  items: any[] = [];
  periods = PERIODS;
  selectedPeriod = DEFAULT_PERIOD;

  isLoading = false;
  hoveredIndex: number = -1;

  constructor(
    private dashboardService: DashboardService,
    private navigationService: NavigationService
  ) {}

  ngOnInit(): void {
    combineLatest([
      this.route.params,
      this.dashboardService.period$
    ]).pipe(
      filter(([params, period]) => !!period && isValidPeriod(period)),
      debounceTime(100),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(([params, period]) => {
      const receivedType = params['type'];
      if (receivedType === 'album' || receivedType === 'artist' || receivedType === 'track') {
        this.type = receivedType;
      }
      this.selectedPeriod = period as PeriodValue; 
      this.loadData();
    });
  }

  onPeriodChange(): void {
    this.dashboardService.updatePeriod(this.selectedPeriod);
  }

  loadData(): void {
    if (!this.selectedPeriod) return; 

    this.isLoading = true;
    const nb = 50;

    let observable$: Observable<Album[] | Artist[] | Track[]>;

    switch (this.type) {
      case 'album': 
        observable$ = this.dashboardService.getTopAlbums(this.selectedPeriod, nb); 
        break;
      case 'artist': 
        observable$ = this.dashboardService.getTopArtists(this.selectedPeriod, nb); 
        break;
      case 'track': 
        observable$ = this.dashboardService.getTopTracks(this.selectedPeriod, nb); 
        break;
      default: 
        this.isLoading = false;
        return;
    }
    
    observable$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data: any[]) => {
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
    switch (this.type) {
      case 'album': return 'Albums';
      case 'artist': return 'Artistes';
      case 'track': return 'Morceaux';
      default: return 'Top';
    }
  }

  formatTitle(item: any): string {
    if (this.type === 'track') {
      return `${item.track} - ${item.artist}${item.album ? ' (' + item.album + ')' : ''}`;
    } else if (this.type === 'album') {
      return `${item.title}${item.artist ? ' - ' + item.artist : ''}`;
    } else {
      return item.artist;
    }
  }

  getImage(item: any): string {
    if (this.type === 'track') return item.trackUrl || 'assets/default-cover.jpg';
    if (this.type === 'album') return item.coverUrl || 'assets/default-cover.jpg';
    return item.coverUrl || 'assets/default-cover.jpg';
  }

  getMainText(item: any): string {
    return this.type === 'track' ? item.track : this.type === 'album' ? item.title : item.artist;
  }

  getSubText(item: any): string {
    if (this.type === 'track') return `${item.artist} - ${item.album}`;
    if (this.type === 'album') return item.artist;
    return '';
  }

  // Navigation vers le détail - Utilisation du NavigationService
  navigateToDetail(item: any): void {
    if (this.type === 'album' || this.type === 'artist') {
      // Utilise la méthode existante du NavigationService
      this.navigationService.navigateFromDashboardItem(item, this.type);
    } 
    // Pour les tracks, création d'une méthode spécifique
    else if (this.type === 'track') {
      this.navigateFromTrack(item);
    }
  }

  // Nouvelle méthode pour la navigation depuis une track
  private navigateFromTrack(track: any): void {
    // Option 1: Naviguer vers l'album si disponible
    if (track.album && track.artist) {
      const albumItem = { title: track.album, artist: track.artist };
      this.navigationService.navigateFromDashboardItem(albumItem, 'album');
    }
    // Option 2: Naviguer vers l'artiste
    else if (track.artist) {
      const artistItem = { artist: track.artist };
      this.navigationService.navigateFromDashboardItem(artistItem, 'artist');
    }
  }

  // Méthode pour le clic sur la carte entière
  onCardClick(item: any): void {
    this.navigateToDetail(item);
  }
}