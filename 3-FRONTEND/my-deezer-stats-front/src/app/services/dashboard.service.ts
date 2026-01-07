import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { BehaviorSubject, catchError, map, Observable, throwError } from 'rxjs';
import { Album, Artist, Track, Recent, SearchResult } from '../models/dashboard.models';


export const DEFAULT_PERIOD = 'thisYear'; 

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  
  private readonly apiUrl = 'http://localhost:5000/api';
  public last4Weeks: Date = new Date();

  constructor(private http: HttpClient) {}

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  uploadExcelFile(formData: FormData): Observable<any> {
    return this.http.post(`${this.apiUrl}/upload/import-excel`, formData, {
      headers: new HttpHeaders({
        'Accept': 'application/json'
      }),
      reportProgress: true,
      observe: 'response'
    }).pipe(
      catchError(error => {
        return throwError(() => ({
          status: error.status,
          error: error.error,
          message: error.error?.title || error.message
        }));
      })
    );
  }

  getTopAlbums(period: string, nb: number): Observable<Album[]> {
    const { from, to } = this.getDateRange(period);
    const params = new HttpParams()
      .set('from', from.toISOString())  
      .set('to', to.toISOString())
      .set('nb', nb.toString());
  
    return this.http.get<Album[]>(`${this.apiUrl}/listening/top-albums`, {
      headers: this.getAuthHeaders(),
      params: params
    });
  }

  getTopArtists(period: string, nb: number): Observable<Artist[]> {
  const { from, to } = this.getDateRange(period);
  const params = new HttpParams()
    .set('from', from.toISOString())
    .set('to', to.toISOString())
    .set('nb', nb.toString());

  return this.http.get<Artist[]>(`${this.apiUrl}/listening/top-artists`, {
    headers: this.getAuthHeaders(),
    params: params
  });
}

  getTopTracks(period : string, nb: number): Observable<Track[]> {
    const { from, to } = this.getDateRange(period);
    const params = new HttpParams()
      .set('from', from.toISOString()) 
      .set('to', to.toISOString())
      .set('nb', nb.toString());
    return this.http.get<Track[]>(`${this.apiUrl}/listening/top-tracks`, {
      headers: this.getAuthHeaders(),
      params: params
    });
  }

  getRecentListens(period: string): Observable<Recent[]> {
    const { from, to } = this.getDateRange(period);
    
    const params = new HttpParams()
      .set('from', from.toISOString())  
      .set('to', to.toISOString());
    return this.http.get<Recent[]>(`${this.apiUrl}/listening/recent`, {
      headers: this.getAuthHeaders(),
      params: params
    });
  }

private getDateRange(period: string): { from: Date, to: Date } {
  const to = new Date(); // Maintenant
  let from = new Date();
  
  // Normalisation pour inclure toute la journée d'aujourd'hui
  to.setHours(23, 59, 59, 999);

  const year = new Date().getFullYear();

  switch (period) {
    case '30':
    case '90':
    case '180':
      const days = parseInt(period);
      from.setDate(to.getDate() - days);
      from.setHours(0, 0, 0, 0); // Début de journée
      break;

    case "thisYear":
      from = new Date(year, 0, 1, 0, 0, 0);
      // 'to' reste aujourd'hui, c'est plus logique pour un dashboard
      break;

    case "lastYear":
      from = new Date(year - 1, 0, 1, 0, 0, 0);
      const lastYearTo = new Date(year - 1, 11, 31, 23, 59, 59);
      return { from, to: lastYearTo };

    case "allTime":
    default:
      from = new Date(2000, 0, 1, 0, 0, 0);
      break;
  }

  return { from, to };
}

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
      map((results: SearchResult[]) =>
        results.map(result => ({
          ...result,
          type: result.type.toLowerCase() as 'album' | 'artist'
        }))
      ),
      catchError(error => {
        console.error('Erreur lors de la recherche:', error);
        return throwError(() => error);
      })
    );
  }

    // 1. On crée un BehaviorSubject pour stocker l'état de la période
  private periodSubject = new BehaviorSubject<string>(DEFAULT_PERIOD);
  
  // 2. On expose l'observable pour que le Dashboard puisse s'y abonner
  period$ = this.periodSubject.asObservable();

  // 3. Méthode pour changer la période (appelée par le Header)
  updatePeriod(period: string) {
    this.periodSubject.next(period);
  }
}
