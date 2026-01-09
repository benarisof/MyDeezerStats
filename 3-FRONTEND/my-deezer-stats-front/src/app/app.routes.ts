import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { DetailComponent } from './components/detail/detail.component';
import { authGuard } from './guards/auth.guard';
import { TopListComponent } from './components/top-list/top-list.component';
import { HistoriqueComponent } from './components/historique/historique.component';

export const routes: Routes = [
  { 
    path: 'dashboard', 
    component: DashboardComponent,
    canActivate: [authGuard] // Protection par JWT
  },
  { path: 'historique', component: HistoriqueComponent },
  {
    path: 'top/:type',
    component: TopListComponent,
    canActivate: [authGuard]
  },
  { 
    path: 'detail/:type',  // type peut être 'album', 'artist' ou 'track'
    component: DetailComponent,
    canActivate: [authGuard], // Protection par JWT
    data: { 
      requiresAuth: true 
    }
  },
  { 
    path: 'login', 
    component: LoginComponent 
  },
  { 
    path: '', 
    redirectTo: 'login', 
    pathMatch: 'full' 
  },
  { 
    path: '**', 
    redirectTo: 'login' // Redirection vers login pour les routes inconnues
  }
];