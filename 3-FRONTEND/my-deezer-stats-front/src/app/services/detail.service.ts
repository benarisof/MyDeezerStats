// src/app/services/detail.service.ts
import { HttpClient, HttpHeaders, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core'; // Ajouté inject si besoin
import { catchError, throwError, Observable } from 'rxjs';
import { AlbumItem, ArtistItem, DetailItem } from '../models/detail.models';
import { LoginService } from './login.service'; // Ajouté : Injection de LoginService
import { PeriodService } from './period.service'; // Ajouté : Import de PeriodService

@Injectable({
  providedIn: 'root'
})
export class DetailService {
  private readonly apiUrl = 'http://localhost:5000/api';
  private loginService = inject(LoginService); // Ajouté : Utilise inject pour standalone

  constructor(
    private http: HttpClient,
    private periodService: PeriodService // Ajouté : Injection de PeriodService
  ) { }

  private getAuthHeaders(): HttpHeaders {
    const token = this.loginService.getToken(); // Modifié : Utilise service au lieu de localStorage
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    });
  }

  private handleError(error: HttpErrorResponse) {
    console.error('API Error:', error);
    return throwError(() => new Error(
      error.error?.message || 'Une erreur est survenue'
    ));
  }

  getDetails(type: 'album' | 'artist', identifier: string, period: string): Observable<DetailItem> {
    const dateRange = this.periodService.getDateRange(period);
    let endpoint = '';
    let params = new HttpParams()
      .set('identifier', identifier)
      .set('from', dateRange.from.toISOString())
      .set('to', dateRange.to.toISOString());

    switch (type) {
      case 'album':
        endpoint = `/listening/album`;
        return this.http.get<AlbumItem>(`${this.apiUrl}${endpoint}`, {
          params,
          headers: this.getAuthHeaders()
        }).pipe(
          catchError(this.handleError)
        );
      case 'artist':
        endpoint = `/listening/artist`;
        return this.http.get<ArtistItem>(`${this.apiUrl}${endpoint}`, {
          params,
          headers: this.getAuthHeaders()
        }).pipe(
          catchError(this.handleError)
        );
      default:
        throw new Error('Type non supporté : ' + type);
    }
  }

  // ============ PROPRIÉTÉS PUBLIQUES DÉLÉGUÉES ============
  
  // Délégation des méthodes de période au PeriodService (inspiré du DashboardService)
  get period$() {
    return this.periodService.period$;
  }

  updatePeriod(period: string) {
    this.periodService.updatePeriod(period);
  }
}