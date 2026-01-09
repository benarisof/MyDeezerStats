import { Injectable } from '@angular/core';
import { HttpRequest, HttpHandler, HttpEvent, HttpInterceptor, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { LoginService } from '../services/login.service';

@Injectable()
export class JwtInterceptor implements HttpInterceptor {
  constructor(private loginService: LoginService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const token = this.loginService.getToken();
    
    // Liste des endpoints qui ne nécessitent pas de token
    const authEndpoints = ['/api/Auth/login', '/api/Auth/signup'];
    const isAuthRequest = authEndpoints.some(url => request.url.toLowerCase().includes(url.toLowerCase()));

    if (token && !isAuthRequest) {
      request = request.clone({
        setHeaders: { Authorization: `Bearer ${token}` }
      });
    }

    return next.handle(request).pipe(
      catchError((error: HttpErrorResponse) => {
        // Si 401 (Unauthorized) en dehors du login, on déconnecte
        if (error.status === 401 && !isAuthRequest) {
          this.loginService.logout();
        }
        return throwError(() => error);
      })
    );
  }
}