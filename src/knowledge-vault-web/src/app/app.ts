import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ConfirmDialogHost } from './shared/components/confirm-dialog/confirm-dialog-host';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ConfirmDialogHost],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
