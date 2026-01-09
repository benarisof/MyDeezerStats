import { Component, OnInit, HostListener, ElementRef, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule, NavigationEnd } from '@angular/router'; 
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, tap, filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { DashboardService } from '../../services/apiService/deezerApi.service';
import { LoginService } from '../../services/login.service';
import { FormatterService } from '../../services/formatter.service';
import { NavigationService } from '../../services/navigation.service';
import { SearchResult } from '../../models/dashboard.models'; 
import { PERIODS, DEFAULT_PERIOD } from '../../models/period.model';
import { HistoriqueComponent } from '../historique/historique.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss']
})
export class HeaderComponent implements OnInit {
  private destroyRef = inject(DestroyRef); 

  navLinks = [
    { label: 'Dashboard', path: '/dashboard' },
    { label: 'Top Albums', path: '/top/album' },
    { label: 'Top Artistes', path: '/top/artist' },
    { label: 'Top Morceaux', path: '/top/track' },
    { label: 'Dernieres écoutes', path: '/historique'}
  ];

  periods = PERIODS;
  selectedPeriod = DEFAULT_PERIOD;

  searchQuery: string = '';
  filteredResults: SearchResult[] = [];
  showSearchResults: boolean = false;
  isLoadingSearch: boolean = false;
  private searchTerms = new Subject<string>();

  isUploading: boolean = false;

  constructor(
    private router: Router,
    private dashboardService: DashboardService,
    public loginService: LoginService,
    private eRef: ElementRef,
    private formatterService: FormatterService,
    private navigationService: NavigationService
  ) {}

  ngOnInit(): void {
    this.setupSearch();
    this.setupNavigationListener();
  }

  private setupSearch(): void {
    this.searchTerms.pipe(
      filter(term => term.length > 1),
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => {
        this.isLoadingSearch = true;
        this.showSearchResults = true;
      }),
      switchMap(term => this.dashboardService.search(term, ['artist', 'album'])),
      takeUntilDestroyed(this.destroyRef) 
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
  }

  private setupNavigationListener(): void {
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef) 
    ).subscribe(() => {
      this.closeSearch();
    });
  }

  @HostListener('document:click', ['$event'])
  clickout(event: any): void {
    if (!this.eRef.nativeElement.contains(event.target)) {
      this.closeSearch();
    }
  }

  private closeSearch(): void {
    this.showSearchResults = false;
    this.searchQuery = '';
    this.filteredResults = [];
  }

  onPeriodChange(): void {
    this.dashboardService.updatePeriod(this.selectedPeriod);
  }

  onFileSelected(event: Event): void {
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

  private uploadExcelFile(file: File): void {
    this.isUploading = true;
    const formData = new FormData();
    formData.append('file', file, file.name);
    
    this.dashboardService.uploadExcelFile(formData).pipe(
        takeUntilDestroyed(this.destroyRef)
    ).subscribe({
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
    this.navigationService.navigateFromSearchResult(item);
  }

  formatResult(item: SearchResult): string {
    return this.formatterService.formatSearchResult(item);
  }

  logout(): void {
    this.loginService.logout();
    this.navigationService.navigateToLogin();
  }
}