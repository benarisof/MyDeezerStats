import { Component, OnInit } from '@angular/core';
import { LoginService } from '../../services/login.service';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DashboardService } from '../../services/dashboard.service';
import { Album, Artist, Track, Recent } from "../../models/dashboard.models";
import { finalize, forkJoin, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, tap, filter } from 'rxjs/operators';

interface SearchResult {
  type: 'artist' | 'album' ;
  title?: string;
  artist?: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  imports: [CommonModule, FormsModule]
})
export class DashboardComponent implements OnInit {
  searchQuery: string = '';
  filteredResults: SearchResult[] = [];
  showSearchResults: boolean = false;
  topAlbums: Album[] = [];
  topArtists: Artist[] = [];
  topTracks: Track[] = [];
  recentListens: Recent[] = [];
  isLoading: boolean = false;
  errorMessage: string = '';

  periods = [
    { value: '4weeks', label: '4 dernières semaines' },
    { value: 'thisYear', label: 'Cette année' },
    { value: 'lastYear', label: 'Année dernière' },
    { value: 'allTime', label: 'Depuis le début' }
  ];

  selectedPeriod = 'lastYear';

  private searchTerms = new Subject<string>();

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
    this.loadDashboardData();

    this.searchTerms.pipe(
      filter(term => term.length > 1),
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => {
        this.isLoading = true;
        this.showSearchResults = true;
      }),
      switchMap(term => this.dashboardService.search(term, ['artist', 'album'])),
      finalize(() => this.isLoading = false)
    ).subscribe({
      next: (results: SearchResult[]) => {
        this.filteredResults = results;  // déjà au bon format (type + champs utiles)
        this.isLoading = false;
      },
      error: () => {
        this.filteredResults = [];
        this.isLoading = false;
      }
    });
    
  }

  onPeriodChange(): void {
    this.isLoading = true;
    this.dashboardService.last4Weeks = new Date(this.recentListens[0].date);
    this.loadDashboardData();
    this.isLoading = false;
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;

    if (input.files && input.files.length > 0) {
      const file = input.files[0];

      if (!this.isExcelFile(file)) {
        alert('Veuillez sélectionner un fichier Excel (.xlsx, .xls)');
        return;
      }
      this.uploadExcelFile(file);
    }
  }

  private isExcelFile(file: File): boolean {
    const allowedExtensions = ['.xlsx', '.xls'];
    const fileName = file.name.toLowerCase();
    return allowedExtensions.some(ext => fileName.endsWith(ext));
  }

  private uploadExcelFile(file: File) {
    this.isLoading = true;
    const formData = new FormData();
    formData.append('file', file, file.name);
    this.dashboardService.uploadExcelFile(formData).subscribe({
      next: (response) => {
        alert('Fichier importé avec succès !');
        this.loadDashboardData();
      },
      error: (error) => {
        alert(`Erreur ${error.status}: ${error.error?.title || 'Échec de l\'import'}`);
        this.isLoading = false;
      }
    });
  }

  private loadDashboardData(): void {
    this.isLoading = true;
    this.errorMessage = '';

    forkJoin([
      this.dashboardService.getTopAlbums(this.selectedPeriod,5),
      this.dashboardService.getTopArtists(this.selectedPeriod,5),
      this.dashboardService.getTopTracks(this.selectedPeriod,5),
      this.dashboardService.getRecentListens(this.selectedPeriod)
    ]).pipe(
      finalize(() => this.isLoading = false)
    ).subscribe({
      next: ([albums, artists, tracks, recentListens]) => {
        this.topAlbums = albums;
        this.topArtists = artists;
        this.topTracks = tracks;
        this.recentListens = recentListens;
      },
      error: (err) => {
        this.errorMessage = 'Erreur lors du chargement';
      }
    });
  }

  logout(): void {
    this.loginService.logout();
    this.router.navigate(['/login']);
  }

  onSearchInput(): void {
    if (this.searchQuery.length > 1) {
      this.searchTerms.next(this.searchQuery);
      this.showSearchResults = true;
    } else {
      this.filteredResults = [];
      this.showSearchResults = false;
    }
  }
  
  onSelectResult(item: SearchResult): void {
    this.showSearchResults = false;
    this.searchQuery = '';
    this.navigateToDetail(item.type, item);
  }
  
  hideSearchResults(): void {
    this.showSearchResults = false;
  }
  
  formatResult(item: SearchResult): string {
    if (item.type === 'album') {
      return `${item.title || ''} - ${item.artist || ''}`;
    } else if (item.type === 'artist') {
      return item.artist || '';
    }
    return '';
  }
  
  performSearch(): void {
    if (this.searchQuery.length > 1) {
      this.searchTerms.next(this.searchQuery);
      this.showSearchResults = true;
    } else {
      this.filteredResults = [];
      this.showSearchResults = false;
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
    } else {
      console.warn(`Incomplete data for type: ${type}`);
    }
  }
}
