import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, BehaviorSubject } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { AuthResponse, SignUpResponse } from '../models/login.model';
import { Router } from '@angular/router';
import { jwtDecode } from 'jwt-decode';

@Injectable({ providedIn: 'root' })
export class LoginService {
  private readonly apiUrl = 'http://localhost:5000/api/auth';
  
  // État réactif initialisé avec une vérification réelle
  private authStatus = new BehaviorSubject<boolean>(this.checkTokenValidity());
  isLoggedIn$ = this.authStatus.asObservable();

  constructor(private http: HttpClient, private router: Router) {}

  private checkTokenValidity(): boolean {
    const token = this.getToken();
    return !!token && !this.isTokenExpired(token);
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, { email, password })
      .pipe(
        tap(response => {
          // Selon ton API : response.token contient l'objet TokenResponse
          const data = response.token;
          if (data && data.success && data.token) {
            this.saveSession(data);
            this.authStatus.next(true);
          }
        }),
        catchError(this.handleError)
      );
  }

  signUp(email: string, password: string): Observable<SignUpResponse> {
    return this.http.post<SignUpResponse>(`${this.apiUrl}/signup`, { email, password })
      .pipe(catchError(this.handleError));
  }

  private saveSession(data: any): void {
    localStorage.setItem('access_token', data.token);
    localStorage.setItem('user_id', data.userId);
    if (data.expiresAt) {
      localStorage.setItem('token_expires_at', data.expiresAt);
    }
  }

  logout(): void {
    localStorage.clear();
    this.authStatus.next(false);
    this.router.navigate(['/login']);
  }

  /**
   * Vérifie si l'utilisateur est authentifié.
   * On vérifie l'état en mémoire ET la validité réelle du token.
   */
  isAuthenticated(): boolean {
    const isValid = this.checkTokenValidity();
    if (!isValid && this.authStatus.value) {
      this.authStatus.next(false); 
    }
    return isValid;
  }

  getToken(): string | null {
    return localStorage.getItem('access_token');
  }

  private isTokenExpired(token: string): boolean {
    if (!token) return true;
    try {
      const decoded: any = jwtDecode(token);
      if (!decoded.exp) return false;

      const now = Math.floor(Date.now() / 1000);
      return decoded.exp < now;
    } catch (e) {
      console.error("Erreur lors du décodage du token", e);
      return true;
    }
  }

  private handleError(error: HttpErrorResponse) {
    let errorMessage = 'Une erreur est survenue';
    if (error.status === 401) errorMessage = 'Identifiants incorrects';
    if (error.status === 409) errorMessage = 'Cet utilisateur existe déjà';
    
    const apiMessage = error.error?.message || error.error;
    return throwError(() => new Error(typeof apiMessage === 'string' ? apiMessage : errorMessage));
  }
}