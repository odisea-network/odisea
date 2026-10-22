import { HttpClient } from '@angular/common/http';
import { Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface ProgramListItem {
  id: string;
  name: string;
  season: string | null;
  description: string | null;
  status: string;
}

@Component({
  selector: 'app-agency-programs',
  template: `
    <h2>Published programs</h2>
    @if (programs.isLoading()) {
      <p>Loading…</p>
    } @else if (programs.error()) {
      <p class="error">Could not load programs.</p>
    } @else {
      <div class="cards">
        @for (program of programs.value(); track program.id) {
          <article class="card">
            <h3>{{ program.name }}</h3>
            @if (program.season) {
              <p class="muted">{{ program.season }}</p>
            }
            @if (program.description) {
              <p>{{ program.description }}</p>
            }
            <p class="muted">Search &amp; booking UI arrives in the next slice.</p>
          </article>
        } @empty {
          <p>No published programs yet.</p>
        }
      </div>
    }
  `,
  styles: `
    .cards { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr)); }
    .card { background: var(--surface-1); border: 1px solid var(--border); border-radius: .75rem; padding: 1.25rem; }
    .card h3 { margin: 0 0 .25rem; }
    .muted { color: var(--text-muted); font-size: .85rem; }
    .error { color: var(--danger); }
  `,
})
export class AgencyPrograms {
  private readonly http = inject(HttpClient);

  readonly programs = resource({
    loader: () => firstValueFrom(this.http.get<ProgramListItem[]>('/api/v1/catalog/programs')),
  });
}
