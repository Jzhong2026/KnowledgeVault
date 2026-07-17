import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar {
  readonly collapsed = input(false);
  readonly collapsedChange = output<boolean>();

  toggle(): void {
    this.collapsedChange.emit(!this.collapsed());
  }
}
