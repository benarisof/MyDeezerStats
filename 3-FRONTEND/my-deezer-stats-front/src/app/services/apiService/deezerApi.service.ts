import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { Album, Artist, Track, Recent, SearchResult } from '../../models/dashboard.models';
import { PeriodService } from '../period.service';
import { FormatterService } from '../formatter.service';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly apiUrl = 'http://localhost:5000/api';

  constructor(
    private http: HttpClient,
    private periodService: PeriodService,
    private formatterService: FormatterService
  ) {}

  // ============ MÉTHODES D'UPLOAD ============
  
  uploadExcelFile(formData: FormData): Observable<any> {
    return this.http.post(`${this.apiUrl}/upload/import-excel`, formData, {
      headers: new HttpHeaders({
        'Accept': 'application/json'
      }),
      reportProgress: true,
      observe: 'response'
    }).pipe(
      catchError(this.handleError)
    );
  }

  // ============ MÉTHODES DE RÉCUPÉRATION DES DONNÉES ============

  getTopAlbums(period: string, nb: number): Observable<Album[]> {
    const dateRange = this.periodService.getDateRange(period);
    const params = this.buildQueryParams(dateRange, nb);

    return this.http.get<Album[]>(`${this.apiUrl}/listening/top-albums`, {
      headers: this.getAuthHeaders(),
      params
    }).pipe(
      map(albums => this.enrichAlbums(albums)),
      catchError(this.handleError)
    );
  }

  getTopArtists(period: string, nb: number): Observable<Artist[]> {
    const dateRange = this.periodService.getDateRange(period);
    const params = this.buildQueryParams(dateRange, nb);

    return this.http.get<Artist[]>(`${this.apiUrl}/listening/top-artists`, {
      headers: this.getAuthHeaders(),
      params
    }).pipe(
      map(artists => this.enrichArtists(artists)),
      catchError(this.handleError)
    );
  }

  getTopTracks(period: string, nb: number): Observable<Track[]> {
    const dateRange = this.periodService.getDateRange(period);
    const params = this.buildQueryParams(dateRange, nb);

    return this.http.get<Track[]>(`${this.apiUrl}/listening/top-tracks`, {
      headers: this.getAuthHeaders(),
      params
    }).pipe(
      map(tracks => this.enrichTracks(tracks)),
      catchError(this.handleError)
    );
  }

  getRecentListens(period: string): Observable<Recent[]> {
    const dateRange = this.periodService.getDateRange(period);
    const params = this.buildQueryParams(dateRange);

    return this.http.get<Recent[]>(`${this.apiUrl}/listening/recent`, {
      headers: this.getAuthHeaders(),
      params
    }).pipe(
      map(recents => this.enrichRecents(recents)),
      catchError(this.handleError)
    );
  }

  // ============ MÉTHODES DE RECHERCHE ============

  search(query: string, types: ('album' | 'artist')[]): Observable<SearchResult[]> {
    if (!query || query.trim() === '') {
      return new Observable(observer => {
        observer.next([]);
        observer.complete();
      });
    }

    let params = new HttpParams().set('query', query.trim());

    if (types.length > 0) {
      params = params.set('types', types.join(','));
    }

    return this.http.get<SearchResult[]>(`${this.apiUrl}/search/suggest`, {
      headers: this.getAuthHeaders(),
      params
    }).pipe(
      map(results => this.formatSearchResults(results)),
      catchError(this.handleError)
    );
  }

  // ============ MÉTHODES D'ENRICHISSEMENT DES DONNÉES ============

  private enrichAlbums(albums: Album[]): Album[] {
    return albums.map(album => ({
      ...album,
      displayTitle: this.formatterService.formatFullTitle(album, 'album'),
      formattedItem: this.formatterService.formatForList(album, 'album')
    }));
  }

  private enrichArtists(artists: Artist[]): Artist[] {
    return artists.map(artist => ({
      ...artist,
      displayTitle: this.formatterService.formatFullTitle(artist, 'artist'),
      formattedItem: this.formatterService.formatForList(artist, 'artist')
    }));
  }

  private enrichTracks(tracks: Track[]): Track[] {
    return tracks.map(track => ({
      ...track,
      displayTitle: this.formatterService.formatFullTitle(track, 'track'),
      formattedItem: this.formatterService.formatForList(track, 'track')
    }));
  }

  private enrichRecents(recents: Recent[]): Recent[] {
    return recents.map(recent => ({
      ...recent,
      displayTitle: `${recent.artist} - ${recent.track}`,
      formattedTime: this.formatTime(recent.date)
    }));
  }

  private formatSearchResults(results: SearchResult[]): SearchResult[] {
    return results.map(result => ({
      ...result,
      type: result.type.toLowerCase() as 'album' | 'artist',
      displayText: this.formatterService.formatSearchResult(result),
      formattedItem: result.type === 'album' 
        ? this.formatterService.formatForList(result as unknown as Album, 'album')
        : this.formatterService.formatForList(result as unknown as Artist, 'artist')
    }));
  }

  // ============ MÉTHODES UTILITAIRES ============

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  private buildQueryParams(dateRange: { from: Date; to: Date }, nb?: number): HttpParams {
    let params = new HttpParams()
      .set('from', dateRange.from.toISOString())
      .set('to', dateRange.to.toISOString());

    if (nb !== undefined) {
      params = params.set('nb', nb.toString());
    }

    return params;
  }

  private formatTime(dateString: string): string {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));

    if (diffHours < 1) {
      const diffMinutes = Math.floor(diffMs / (1000 * 60));
      return `Il y a ${diffMinutes} minute${diffMinutes > 1 ? 's' : ''}`;
    } else if (diffHours < 24) {
      return `Il y a ${diffHours} heure${diffHours > 1 ? 's' : ''}`;
    } else {
      return date.toLocaleDateString('fr-FR', {
        day: 'numeric',
        month: 'short',
        hour: '2-digit',
        minute: '2-digit'
      });
    }
  }

  private handleError(error: any): Observable<never> {
    console.error('API Error:', error);
    
    const errorMessage = error.error?.title || 
                        error.error?.message || 
                        error.message || 
                        'Une erreur est survenue';
    
    return throwError(() => ({
      status: error.status,
      error: error.error,
      message: errorMessage,
      timestamp: new Date().toISOString()
    }));
  }

  // ============ PROPRIÉTÉS PUBLIQUES DÉLÉGUÉES ============
  
  // Délégation des méthodes de période au PeriodService
  get period$() {
    return this.periodService.period$;
  }

  updatePeriod(period: string) {
    this.periodService.updatePeriod(period);
  }

  // Méthode utilitaire pour les cas d'usage courants
  getDashboardData(period: string) {
    return {
      topAlbums: this.getTopAlbums(period, 10),
      topArtists: this.getTopArtists(period, 10),
      topTracks: this.getTopTracks(period, 10),
      recentListens: this.getRecentListens(period)
    };
  }
}