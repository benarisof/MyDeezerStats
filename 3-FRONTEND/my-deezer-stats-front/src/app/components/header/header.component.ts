import { Component, OnInit, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule, NavigationEnd } from '@angular/router'; 
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, tap, filter, finalize } from 'rxjs/operators';
import { DashboardService, DEFAULT_PERIOD } from '../../services/dashboard.service';
import { LoginService } from '../../services/login.service';
import { SearchResult } from '../../models/dashboard.models'; 

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss']
})
export class HeaderComponent implements OnInit {
  // Navigation Tabs
  navLinks = [
    { label: 'Dashboard', path: '/dashboard' },
    { label: 'Top Albums', path: '/top/album' },
    { label: 'Top Artists', path: '/top/artist' },
    { label: 'Top Tracks', path: '/top/track' }
  ];

  // Périodes
  periods = [
    { value: '4weeks', label: '4 dernières semaines' },
    { value: 'thisYear', label: 'Cette année' },
    { value: 'lastYear', label: 'Année dernière' },
    { value: 'allTime', label: 'Depuis le début' }
  ];
  selectedPeriod = DEFAULT_PERIOD;

  // Recherche
  searchQuery: string = '';
  filteredResults: SearchResult[] = [];
  showSearchResults: boolean = false;
  isLoadingSearch: boolean = false;
  private searchTerms = new Subject<string>();

  // Upload
  isUploading: boolean = false;

  constructor(
    private router: Router,
    private dashboardService: DashboardService,
    public loginService: LoginService,
    private eRef: ElementRef 
  ) {}

  ngOnInit(): void {
    // 1. Logique de recherche existante
    this.searchTerms.pipe(
      filter(term => term.length > 1),
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => {
        this.isLoadingSearch = true;
        this.showSearchResults = true;
      }),
      switchMap(term => this.dashboardService.search(term, ['artist', 'album'])),
    ).subscribe({
      next: (results: SearchResult[]) => {
        this.filteredResults = results;
        this.isLoadingSearch = false;
      },
      error: () => {
        this.filteredResults = [];
        this.isLoadingSearch = false;
      }
    });

    // 2. Fermer la recherche lors du changement de composant (navigation)
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.closeSearch();
    });
  }

  // Detecter le clic à l'extérieur du composant
  @HostListener('document:click', ['$event'])
  clickout(event: any) {
    if (!this.eRef.nativeElement.contains(event.target)) {
      this.closeSearch();
    }
  }

  // Méthode utilitaire pour réinitialiser la recherche
  private closeSearch(): void {
    this.showSearchResults = false;
    this.searchQuery = '';
    this.filteredResults = [];
  }

  // --- Gestion de la Période ---
  onPeriodChange(): void {
    this.dashboardService.updatePeriod(this.selectedPeriod);
  }

  // --- Gestion de l'Upload ---
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
    this.isUploading = true;
    const formData = new FormData();
    formData.append('file', file, file.name);
    
    this.dashboardService.uploadExcelFile(formData).subscribe({
      next: () => {
        alert('Fichier importé avec succès !');
        this.isUploading = false;
        this.dashboardService.updatePeriod(this.selectedPeriod); 
      },
      error: (error) => {
        alert(`Erreur : ${error.error?.title || "Échec de l'import"}`);
        this.isUploading = false;
      }
    });
  }

  // --- Gestion de la Recherche ---
    onSearchInput(): void {
    if (this.searchQuery.length > 1) {
      this.showSearchResults = true; 
      this.searchTerms.next(this.searchQuery);
    } else {
      this.closeSearch();
    }
  }

  onSelectResult(item: SearchResult): void {
    this.showSearchResults = false;
    this.searchQuery = '';
    // Logique de navigation vers le détail 
    let identifier = '';
    if (item.type === 'album') identifier = `${item.title}|${item.artist}`;
    else if (item.type === 'artist') identifier = item.artist || '';

    if (identifier) {
      this.router.navigate(['/detail', item.type], { queryParams: { identifier } });
    }
  }

  formatResult(item: SearchResult): string {
    return item.type === 'album' ? `${item.title} - ${item.artist}` : item.artist || '';
  }

  logout(): void {
    this.loginService.logout();
    this.router.navigate(['/login']);
  }
}