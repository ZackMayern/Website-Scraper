import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, signal } from '@angular/core';
import { AgGridAngular } from 'ag-grid-angular';
import {
  ClientSideRowModelModule,
  ColDef,
  DateFilterModule,
  ModuleRegistry,
  NumberFilterModule,
  PaginationModule,
  TextFilterModule,
  TooltipModule,
  themeQuartz
} from 'ag-grid-community';
import { finalize } from 'rxjs';

ModuleRegistry.registerModules([
  ClientSideRowModelModule,
  DateFilterModule,
  NumberFilterModule,
  PaginationModule,
  TextFilterModule,
  TooltipModule
]);

interface GameListing {
  sourceId: number;
  title: string;
  genres: string[];
  description: string;
  price: number;
  sourceUrl: string;
  scrapedAt: string;
}

interface ListingsResponse {
  total: number;
  items: GameListing[];
}

@Component({
  selector: 'app-root',
  imports: [AgGridAngular],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  protected readonly listings = signal<GameListing[]>([]);
  protected readonly totalListings = signal(0);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly genreCount = computed(() =>
    new Set(this.listings().flatMap((listing) => listing.genres)).size
  );
  protected readonly gridTheme = themeQuartz.withParams({
    accentColor: '#28735b',
    backgroundColor: '#fffefa',
    borderColor: '#e3e6dc',
    browserColorScheme: 'light',
    foregroundColor: '#28352e',
    headerBackgroundColor: '#f2f4ed',
    headerFontWeight: 600,
    rowHoverColor: '#f5f8f1',
    spacing: 7
  });
  protected readonly columnDefs: ColDef<GameListing>[] = [
    { field: 'sourceId', headerName: 'ID', width: 95, sort: 'asc' },
    { field: 'title', headerName: 'Title', minWidth: 210, flex: 1.2 },
    {
      field: 'genres',
      minWidth: 175,
      valueFormatter: (params) => params.value?.join(', ') ?? 'Uncategorized'
    },
    {
      field: 'price',
      width: 120,
      valueFormatter: (params) => `$${Number(params.value).toFixed(2)}`
    },
    {
      field: 'description',
      minWidth: 260,
      flex: 2,
      wrapText: true,
      cellClass: 'description-cell',
      tooltipField: 'description'
    },
    {
      field: 'scrapedAt',
      headerName: 'Last scraped',
      minWidth: 180,
      valueFormatter: (params) => params.value ? new Date(params.value).toLocaleString() : ''
    }
  ];
  protected readonly defaultColDef: ColDef<GameListing> = {
    sortable: true,
    filter: true,
    resizable: true
  };

  constructor(private readonly http: HttpClient) {}

  ngOnInit(): void {
    this.loadListings();
  }

  protected loadListings(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.http.get<ListingsResponse>('/api/games?page=1&pageSize=100')
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (response) => {
          this.listings.set(response.items ?? []);
          this.totalListings.set(response.total ?? response.items?.length ?? 0);
        },
        error: () => this.errorMessage.set('Could not reach ScraperApi. Check that the API is running and try again.')
      });
  }
}
