import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { LoginService } from '../services/login.service';  // Import du service

export const authGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const loginService = inject(LoginService);  // Injection du service
  
  // Utilisez la méthode du service au lieu de vérifier directement le localStorage
  if (loginService.isAuthenticated()) {
    return true;
  }
  
  // Redirection avec préservation de l'URL
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url }
  });
};