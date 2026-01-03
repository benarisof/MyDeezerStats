import { Component } from '@angular/core';
import { LoginService } from '../../services/login.service';
import { Router, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { SignUpResponse } from '../../models/login.model';

@Component({
  standalone: true,
  selector: 'app-login',
  templateUrl: './login.component.html',
  imports: [FormsModule, CommonModule],
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  email = '';
  password = '';
  errorMessage = '';
  successMessage = '';
  isLoading = false;
  isSignUp: boolean = false;

  constructor(
    private loginService: LoginService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    // Vérification si l'utilisateur est déjà connecté
    if (this.loginService.isAuthenticated()) {
      this.router.navigate(['/dashboard']); // Redirection vers /dashboard si déjà connecté
    }
  }

    // Bascule entre le mode login et le mode sign-up
  toggleAuthMode() {
      this.isSignUp = !this.isSignUp;
  }

  login(): void {
  if (!this.isValidForm()) return;

  this.isLoading = true;
  this.errorMessage = '';

  this.loginService.login(this.email, this.password).subscribe({
    next: (response) => {
      // CORRECTION : Accès au token imbriqué
      const apiResponse = response.token;  // D'abord récupérer l'objet token
      
      if (!apiResponse?.token) {
        throw new Error('Réponse invalide de l\'API : token manquant');
      }
      
      if (!apiResponse.success) {
        throw new Error(apiResponse.message || 'Authentification échouée');
      }
      
      // 1. Stockage du token JWT (pas de l'objet complet)
      localStorage.setItem('auth_token', apiResponse.token);  // ← apiResponse.token, pas response.token
      
      // 2. Stocker aussi l'userId pour usage futur
      if (apiResponse.userId) {
        localStorage.setItem('user_id', apiResponse.userId);
      }
      
      // 3. Récupération de l'URL de redirection
      const returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
      
      // 4. Navigation
      this.router.navigateByUrl(returnUrl)
        .then(navSuccess => {
          if (!navSuccess) {
            console.error('Échec de la navigation vers', returnUrl);
            this.router.navigate(['/dashboard']); // Fallback
          }
        })
        .catch(err => {
          console.error('Erreur de navigation:', err);
          // Fallback ultime
          setTimeout(() => {
            window.location.href = '/dashboard';
          }, 100);
        });
    },
    error: (err) => {
      this.errorMessage = this.getErrorMessage(err);
      this.isLoading = false;
    },
    complete: () => {
      this.isLoading = false;
    }
  });
}
  signUp() {
    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';
  
    this.loginService.signUp(this.email, this.password).subscribe({
      next: (response: SignUpResponse) => {
        this.isLoading = false;
        
        if (response.success) {
          this.successMessage = response.message || 'Votre compte a été créé avec succès ! Vous pouvez maintenant vous connecter.';
          // this.router.navigate(['/login']);
        } else {
          this.errorMessage = response.message || 'Erreur inattendue lors de la création du compte.';
        }
      },
      error: (error: Error) => {
        this.isLoading = false;
        this.errorMessage = error.message;
      }
    });
  }
  
  private isValidForm(): boolean {
    return this.email.includes('@') && this.password.length >= 6;
  }

  private getErrorMessage(err: any): string {
    switch (err.status) {
      case 0: return 'Serveur indisponible';
      case 401: return 'Identifiants invalides';
      case 429: return 'Trop de tentatives';
      default: return 'Erreur technique';
    }
  }
}