import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { LoginService } from '../../services/login.service';
import { Router, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  standalone: true,
  selector: 'app-login',
  templateUrl: './login.component.html',
  imports: [FormsModule, CommonModule],
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  email = '';
  password = '';
  errorMessage = '';
  successMessage = '';
  isLoading = false;
  isSignUp = false;

  // Utilisation de inject() pour une syntaxe plus moderne
  private destroyRef = inject(DestroyRef);

  constructor(
    private loginService: LoginService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    if (this.loginService.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    }
  }

  toggleAuthMode() {
    this.isSignUp = !this.isSignUp;
    this.resetMessages();
  }

  private resetMessages() {
    this.errorMessage = '';
    this.successMessage = '';
  }

  handleAuth(): void {
    if (!this.isValid()) return;

    this.isLoading = true;
    this.resetMessages();

    if (this.isSignUp) {
      this.executeSignUp();
    } else {
      this.executeLogin();
    }
  }

  private executeSignUp(): void {
    this.loginService.signUp(this.email, this.password)
      .pipe(takeUntilDestroyed(this.destroyRef)) // Empêche les fuites de mémoire
      .subscribe({
        next: (response) => {
          this.successMessage = response.message || 'Compte créé avec succès !';
          this.isSignUp = false;
          this.isLoading = false;
          this.password = ''; // Reset password par sécurité
        },
        error: (err) => {
          this.errorMessage = err.message;
          this.isLoading = false;
        }
      });
  }

  private executeLogin(): void {
    this.loginService.login(this.email, this.password)
      .pipe(takeUntilDestroyed(this.destroyRef)) // Nettoyage auto si le composant est détruit
      .subscribe({
        next: () => {
          const returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
          this.router.navigateByUrl(returnUrl);
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err.message;
          this.isLoading = false;
        }
      });
  }

  private isValid(): boolean {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(this.email)) {
      this.errorMessage = 'Veuillez entrer un email valide.';
      return false;
    }
    if (this.password.length < 6) {
      this.errorMessage = 'Le mot de passe doit contenir au moins 6 caractères.';
      return false;
    }
    return true;
  }
}