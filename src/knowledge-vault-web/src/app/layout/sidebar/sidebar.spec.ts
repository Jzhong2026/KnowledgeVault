import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { Sidebar } from './sidebar';

@Component({ template: '' })
class EmptyRouteComponent {}

describe('Sidebar', () => {
  it('groups project and personal document navigation under distinct top-level sections', async () => {
    await TestBed.configureTestingModule({
      imports: [Sidebar],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(Sidebar);
    fixture.detectChanges();

    const sections = Array.from(
      fixture.nativeElement.querySelectorAll('.nav__section'),
    ) as HTMLElement[];

    expect(sections).toHaveLength(2);
    expect(sections[0].querySelector('.nav__parent')?.textContent?.trim()).toBe('Projects');
    expect(
      Array.from(sections[0].querySelectorAll('a')).map((link) => link.textContent?.trim()),
    ).toEqual(['Project Management', 'Documents']);
    expect(sections[1].querySelector('.nav__parent')?.textContent?.trim()).toBe('My Workspace');
    expect(sections[1].querySelector('a')?.textContent?.trim()).toBe('Documents');
  });

  it('keeps the matching document navigation active on detail routes', async () => {
    await TestBed.configureTestingModule({
      imports: [Sidebar, EmptyRouteComponent],
      providers: [
        provideRouter([
          { path: 'project-documents/detail/:id', component: EmptyRouteComponent },
          { path: 'knowledge/detail/:id', component: EmptyRouteComponent },
        ]),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(Sidebar);
    const router = TestBed.inject(Router);
    fixture.detectChanges();

    await router.navigateByUrl('/project-documents/detail/project-document-id');
    await fixture.whenStable();
    fixture.detectChanges();

    const projectDocuments = fixture.nativeElement.querySelector(
      'a[title="Project Documents"]',
    ) as HTMLAnchorElement;
    const myDocuments = fixture.nativeElement.querySelector(
      'a[title="My Documents"]',
    ) as HTMLAnchorElement;

    expect(projectDocuments.classList.contains('is-active')).toBe(true);
    expect(myDocuments.classList.contains('is-active')).toBe(false);

    await router.navigateByUrl('/knowledge/detail/personal-document-id');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(projectDocuments.classList.contains('is-active')).toBe(false);
    expect(myDocuments.classList.contains('is-active')).toBe(true);
  });
});
