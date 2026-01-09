import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { LoginService } from '../../services/login.service';
import { Router, ActivatedRoute } from '@angular/router';
import { DetailService } from '../../services/detail.service';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { AlbumItem, ArtistItem, DetailItem } from '../../models/detail.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

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
    private detailService: DetailService
  ) {}

  ngOnInit(): void {
    if (!this.loginService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }
  
    this.route.params.pipe(
        takeUntilDestroyed(this.destroyRef)
    ).subscribe(params => {
      const type = params['type'] as 'album' | 'artist' ;
      const identifier = this.route.snapshot.queryParams['identifier'];
      if (identifier) {
        this.loadDetailData(type, identifier);
      } else {
        this.router.navigate(['/dashboard']);
      }
    });
  }

  loadDetailData(type: 'album' | 'artist', identifier: string): void {
    this.detailService.getDetails(type, identifier).pipe(
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