import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { DocumentActivityDay } from '../../../core/models/knowledge.models';
import { ProjectSummary } from '../../../core/models/projects.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

type DashboardTab = 'statistics' | 'hub';
type ActivityGranularity = 'day' | 'week';
type ActivityRange = '7d' | '1m' | '6m';

interface ChartPoint {
  key: string;
  label: string;
  axisLabel: string;
  count: number;
  height: number;
}

const RANGE_DAYS: Record<ActivityRange, number> = {
  '7d': 7,
  '1m': 30,
  '6m': 183,
};

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, LoadingIndicator],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
})
export class DashboardPage {
  private readonly api = inject(ApiClient);

  readonly activeTab = signal<DashboardTab>('statistics');
  readonly granularity = signal<ActivityGranularity>('day');
  readonly activityRange = signal<ActivityRange>('7d');
  readonly selectedProjectId = signal('');
  readonly loading = signal(true);
  readonly activityLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly activityError = signal<string | null>(null);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly activity = signal<DocumentActivityDay[]>([]);
  readonly totalItems = signal(0);
  readonly categoryCount = signal(0);
  readonly tagCount = signal(0);

  readonly totalChanges = computed(() =>
    this.activity().reduce((total, item) => total + item.changeCount, 0),
  );

  readonly selectedProjectName = computed(() => {
    const selectedId = this.selectedProjectId();
    return selectedId
      ? this.projects().find((project) => project.id === selectedId)?.name ?? 'Selected project'
      : 'All followed projects';
  });

  readonly chartPoints = computed<ChartPoint[]>(() => {
    const dailyPoints = this.buildDailyPoints();
    const points =
      this.granularity() === 'day' ? dailyPoints : this.aggregateWeeks(dailyPoints);
    const maximum = Math.max(...points.map((point) => point.count), 1);
    const labelStep = points.length <= 14 ? 1 : points.length <= 35 ? 3 : 14;

    return points.map((point, index) => ({
      ...point,
      axisLabel:
        index === 0 || index === points.length - 1 || index % labelStep === 0
          ? point.axisLabel
          : '',
      height: (point.count / maximum) * 100,
    }));
  });

  readonly chartMinimumWidth = computed(() => {
    const pointWidth = this.granularity() === 'day' ? 36 : 64;
    return Math.max(this.chartPoints().length * pointWidth, 680);
  });

  constructor() {
    this.load();
  }

  setTab(tab: DashboardTab): void {
    this.activeTab.set(tab);
  }

  setGranularity(granularity: ActivityGranularity): void {
    this.granularity.set(granularity);
  }

  setRange(range: ActivityRange): void {
    if (range === this.activityRange()) {
      return;
    }

    this.activityRange.set(range);
    this.loadActivity();
  }

  selectProject(event: Event): void {
    this.selectedProjectId.set((event.target as HTMLSelectElement).value);
    this.loadActivity();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      stats: this.api.getProjectDocumentStats(),
      projects: this.api.listProjects({ followingOnly: true, page: 1, pageSize: 100 }),
    }).subscribe({
      next: ({ stats, projects }) => {
        this.totalItems.set(stats.documentCount);
        this.categoryCount.set(stats.categoryCount);
        this.tagCount.set(stats.tagCount);
        this.projects.set(projects.items);
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
      complete: () => this.loading.set(false),
    });

    this.loadActivity();
  }

  private loadActivity(): void {
    this.activityLoading.set(true);
    this.activityError.set(null);
    this.activity.set([]);
    this.api.listProjectDocumentActivity(this.createActivityQuery()).subscribe({
      next: (activity) => this.activity.set(activity),
      error: (error) => {
        this.activityError.set(getErrorMessage(error));
        this.activityLoading.set(false);
      },
      complete: () => this.activityLoading.set(false),
    });
  }

  private createActivityQuery(): {
    from: string;
    to: string;
    utcOffsetMinutes: number;
    projectId?: string;
  } {
    const now = new Date();
    const from = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    from.setDate(from.getDate() - (RANGE_DAYS[this.activityRange()] - 1));

    return {
      from: from.toISOString(),
      to: now.toISOString(),
      utcOffsetMinutes: -now.getTimezoneOffset(),
      projectId: this.selectedProjectId() || undefined,
    };
  }

  private buildDailyPoints(): ChartPoint[] {
    const activityByDate = new Map(
      this.activity().map((item) => [item.date, item.changeCount]),
    );
    const today = new Date();
    const firstDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    firstDate.setDate(firstDate.getDate() - (RANGE_DAYS[this.activityRange()] - 1));

    return Array.from({ length: RANGE_DAYS[this.activityRange()] }, (_, index) => {
      const date = new Date(firstDate);
      date.setDate(firstDate.getDate() + index);
      const key = this.dateKey(date);
      return {
        key,
        label: date.toLocaleDateString('en-US', {
          weekday: 'short',
          month: 'short',
          day: 'numeric',
          year: 'numeric',
        }),
        axisLabel: date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
        count: activityByDate.get(key) ?? 0,
        height: 0,
      };
    });
  }

  private aggregateWeeks(days: ChartPoint[]): ChartPoint[] {
    const weeks = new Map<string, { start: Date; end: Date; count: number }>();
    for (const day of days) {
      const date = this.parseDateKey(day.key);
      const monday = new Date(date);
      const dayOfWeek = monday.getDay();
      monday.setDate(monday.getDate() - (dayOfWeek === 0 ? 6 : dayOfWeek - 1));
      const key = this.dateKey(monday);
      const current = weeks.get(key);
      if (current) {
        current.end = date;
        current.count += day.count;
      } else {
        weeks.set(key, { start: date, end: date, count: day.count });
      }
    }

    return Array.from(weeks.entries()).map(([key, week]) => {
      const start = week.start.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
      const end = week.end.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
      return {
        key,
        label: `${start} – ${end}`,
        axisLabel: start,
        count: week.count,
        height: 0,
      };
    });
  }

  private dateKey(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private parseDateKey(key: string): Date {
    const [year, month, day] = key.split('-').map(Number);
    return new Date(year, month - 1, day);
  }
}
