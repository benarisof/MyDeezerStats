import { Component, inject, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DashboardService } from '../../services/dashboard.service';
import { Subscription, Observable } from 'rxjs';

@Component({
  selector: 'app-top-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './top-list.component.html',
  styleUrl: './top-list.component.scss'
})
export class TopListComponent implements OnInit {

  @Input() type: 'album' | 'artist' | 'track' ='album';
  @Input() items: any[] = [];
  private route = inject(ActivatedRoute);
  private periodSubscription: Subscription | undefined;
  periods = [
    { value: '4weeks', label: '4 dernières semaines' },
    { value: 'thisYear', label: 'Cette année' },
    { value: 'lastYear', label: 'Année dernière' },
    { value: 'allTime', label: 'Depuis le début' }
  ];

  isLoading = false;
  hoveredIndex: number = -1;
  selectedPeriod = 'thisYear';

  constructor(
      private dashboardService: DashboardService
    ) {}

  ngOnInit(): void {
    // Gestion du TYPE (via l'URL)
    this.route.params.subscribe(params => {
      const receivedType = params['type'];
      if (receivedType === 'album' || receivedType === 'artist' || receivedType === 'track') {
        this.type = receivedType;
        this.loadData();
      }
    });

    // 3. Gestion de la PÉRIODE (via le Service)
    this.periodSubscription = this.dashboardService.period$.subscribe(period => {
      this.selectedPeriod = period; // On met à jour la variable locale
      this.loadData(); 
    });
  }

  // 4. Nettoyage pour éviter les fuites de mémoire
  ngOnDestroy(): void {
    if (this.periodSubscription) {
      this.periodSubscription.unsubscribe();
    }
  }

  onPeriodChange(): void {
    this.dashboardService.updatePeriod(this.selectedPeriod);
  }

loadData(): void {
  if (!this.selectedPeriod) return; 

  this.isLoading = true;
  const nb = 50;

  // 2. On déclare explicitement que c'est un Observable qui retourne un tableau
  let observable$: Observable<any[]>; 

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

  // 3. On type les arguments du subscribe pour corriger les erreurs TS7006
  observable$.subscribe({
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
    console.log(this.type);
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

  navigateToDetail(item: any): void {
    // Navigation optionnelle si tu veux faire un zoom sur l'élément
    // this.router.navigate(['/detail', this.type, item.id]);
  }

}
