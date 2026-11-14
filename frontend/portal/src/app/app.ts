import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DialogHost, ToastHost } from './shared/ui';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastHost, DialogHost],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './app.html',
})
export class App {}
