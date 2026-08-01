import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { EditorComponent } from './editor/editor.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'app', component: EditorComponent }
];
