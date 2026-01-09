import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { LoginService } from '../../services/login.service';
import { Router, ActivatedRoute } from '@angular/router';
import { DetailService } from '../../services/detail.service';
import { PeriodService } from '../../services/period.service'; // Ajouté : Import de PeriodService
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { AlbumItem, ArtistItem, DetailItem } from '../../models/detail.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { combineLatest } from 'rxjs'; // Ajouté : Pour combiner observables

@Component({
  selector: 'app-detail',
  standalone: true,
  imports: [DurationPipe, CommonModule],
  templateUrl: './detail.component.html',
  styleUrls: ['./detail.component.scss']
})
export class DetailComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  item?: DetailItem;
  loading = true;
  error: string | null = null;

  constructor(
    private location: Location,
    private loginService: LoginService,
    private router: Router,
    private route: ActivatedRoute,
    private detailService: DetailService,
    private periodService: PeriodService // Ajouté : Injection de PeriodService
  ) {}

  ngOnInit(): void {
    if (!this.loginService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }
  
    combineLatest([
      this.route.params,
      this.periodService.period$
    ]).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(([params, period]) => {
      const type = params['type'] as 'album' | 'artist';
      const identifier = this.route.snapshot.queryParams['identifier'];
      if (identifier && period) {
        this.loadDetailData(type, identifier, period);
      } else {
        this.router.navigate(['/dashboard']);
      }
    });
  }

  loadDetailData(type: 'album' | 'artist', identifier: string, period: string): void {
    this.loading = true; // Réinitialiser le loading à chaque changement
    this.detailService.getDetails(type, identifier, period).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (data) => {
        this.item = this.mapDataToItem(type, data);
        this.loading = false;
      },
      error: (err) => {
        this.handleError(err);
      }
    });
  }

  private mapDataToItem(type: 'album' | 'artist' , data: any): DetailItem {
    switch (type) {
      case 'album':
        return { ...data, type } as AlbumItem;
      case 'artist':
        return { ...data, type } as ArtistItem;
      default:
        throw new Error('Type non supporté : ' + type);
    }
  }

  private handleError(error: any): void {
    console.error('Erreur:', error);
    this.error = error.message || 'Échec du chargement des données';
    this.loading = false;
    setTimeout(() => this.router.navigate(['/dashboard']), 3000);
  }

  goToDashboard() {
    this.router.navigate(['/dashboard']);
  }

  goBack(): void {
    this.location.back();
  } 

}