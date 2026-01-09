import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { SearchResult } from '../models/dashboard.models';
import { FormatterService } from './formatter.service';

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  
  constructor(
    private router: Router,
    private formatterService: FormatterService
  ) {}

  // Navigation vers les pages de top
  navigateToTop(type: 'album' | 'artist' | 'track'): void {
    this.router.navigate(['/top', type]);
  }

  // Navigation vers les détails
  navigateToDetail(type: 'album' | 'artist', identifier: string): void {
    if (identifier) {
      this.router.navigate(['/detail', type], { 
        queryParams: { identifier } 
      });
    }
  }

  // Navigation depuis un résultat de recherche
  navigateFromSearchResult(item: SearchResult): void {
    let identifier = '';
    
    if (item.type === 'album') {
      identifier = `${item.title}|${item.artist}`;
    } else if (item.type === 'artist') {
      identifier = item.artist || '';
    }

    if (identifier) {
      this.navigateToDetail(item.type, identifier);
    }
  }

  // Navigation depuis un item de dashboard ou top-list
  navigateFromDashboardItem(item: any, type: 'album' | 'artist'): void {
    const identifier = this.formatterService.getNavigationIdentifier(item, type);
    this.navigateToDetail(type, identifier);
  }

  // NOUVELLE MÉTHODE: Navigation depuis une track
  navigateFromTrack(track: any): void {
    // Navigue vers l'album si disponible, sinon vers l'artiste
    if (track.album && track.artist) {
      const albumItem = { title: track.album, artist: track.artist };
      this.navigateFromDashboardItem(albumItem, 'album');
    } else if (track.artist) {
      const artistItem = { artist: track.artist };
      this.navigateFromDashboardItem(artistItem, 'artist');
    }
  }

  // Navigation de base
  navigateToLogin(): void {
    this.router.navigate(['/login']);
  }

  navigateToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }
}