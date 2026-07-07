import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { signal, WritableSignal } from '@angular/core';
import { GitRepositoryService } from './git-repository.service';
import { RepositoryContextService } from './repository-context.service';
import { environment } from '../../environments/environment';
import { GitRepositoryConnectionSummary, GitRepositoryOverview } from '../models/git-repository.model';

describe('GitRepositoryService', () => {
  let service: GitRepositoryService;
  let httpMock: HttpTestingController;
  let selectedRepoId: WritableSignal<string | null>;

  const connections: GitRepositoryConnectionSummary[] = [
    { repoUrl: 'https://github.com/acme/dash.git', ownerLogin: 'acme', repoName: 'dash', isPrimary: true },
    { repoUrl: 'https://github.com/acme/dash-view.git', ownerLogin: 'acme', repoName: 'dash-view', isPrimary: false },
  ];

  const overview: GitRepositoryOverview = {
    hasConnection: true,
    repoUrl: 'https://github.com/acme/dash.git',
    fullName: 'acme/dash',
    defaultBranch: 'main',
    branches: [],
    commits: [],
    pullRequests: [],
    rateLimit: { limit: 5000, remaining: 4990, resetAt: new Date().toISOString() },
    status: 'Ok',
    lastSyncError: null,
  };

  function connectionsUrl(repoId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/git-repositories`;
  }

  function overviewUrl(repoId: string): string {
    return `${connectionsUrl(repoId)}/overview`;
  }

  beforeEach(() => {
    // selectedRepoId starts as null so the constructor effect's initial run is a no-op (reset only);
    // tests drive loading through the service's public methods directly instead of signal timing.
    selectedRepoId = signal<string | null>(null);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        GitRepositoryService,
        { provide: RepositoryContextService, useValue: { selectedRepoId } },
      ],
    });

    service = TestBed.inject(GitRepositoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should create', () => {
    expect(service).toBeTruthy();
  });

  it('loadForRepository — fetches connections and overview, updates signals', () => {
    service.loadForRepository('repo-1');

    const connReq = httpMock.expectOne(connectionsUrl('repo-1'));
    expect(connReq.request.method).toBe('GET');
    connReq.flush(connections);

    const overviewReq = httpMock.expectOne(req => req.url === overviewUrl('repo-1'));
    expect(overviewReq.request.method).toBe('GET');
    expect(overviewReq.request.params.has('repoUrl')).toBeFalse();
    overviewReq.flush(overview);

    expect(service.connections()).toEqual(connections);
    expect(service.overview()).toEqual(overview);
    expect(service.hasConnection()).toBeTrue();
    expect(service.loading()).toBeFalse();
    expect(service.error()).toBeNull();
  });

  it('loadForRepository — passes repoUrl as a query param when given', () => {
    service.loadForRepository('repo-1', connections[1].repoUrl);

    httpMock.expectOne(connectionsUrl('repo-1')).flush(connections);

    const overviewReq = httpMock.expectOne(req => req.url === overviewUrl('repo-1'));
    expect(overviewReq.request.params.get('repoUrl')).toBe(connections[1].repoUrl);
    overviewReq.flush({ ...overview, repoUrl: connections[1].repoUrl });
  });

  it('selectConnection — reloads only the overview for the chosen connection', () => {
    service.loadForRepository('repo-1');
    httpMock.expectOne(connectionsUrl('repo-1')).flush(connections);
    httpMock.expectOne(req => req.url === overviewUrl('repo-1')).flush(overview);

    service.selectConnection('repo-1', connections[1].repoUrl);

    // No second connections list fetch — only the overview reloads.
    httpMock.expectNone(connectionsUrl('repo-1'));
    const req = httpMock.expectOne(req => req.url === overviewUrl('repo-1'));
    expect(req.request.params.get('repoUrl')).toBe(connections[1].repoUrl);
    req.flush({ ...overview, repoUrl: connections[1].repoUrl });

    expect(service.selectedRepoUrl()).toBe(connections[1].repoUrl);
  });

  it('overview HTTP error — sets error signal and clears overview', () => {
    service.loadForRepository('repo-1');
    httpMock.expectOne(connectionsUrl('repo-1')).flush(connections);

    httpMock
      .expectOne(req => req.url === overviewUrl('repo-1'))
      .flush('Server error', { status: 500, statusText: 'Internal Server Error' });

    expect(service.error()).toBe('Failed to load repository overview');
    expect(service.overview()).toBeNull();
    expect(service.loading()).toBeFalse();
  });

  it('reset — clears connections, overview, and selection', () => {
    service.loadForRepository('repo-1');
    httpMock.expectOne(connectionsUrl('repo-1')).flush(connections);
    httpMock.expectOne(req => req.url === overviewUrl('repo-1')).flush(overview);

    service.reset();

    expect(service.connections()).toEqual([]);
    expect(service.overview()).toBeNull();
    expect(service.selectedRepoUrl()).toBeNull();
    expect(service.hasConnection()).toBeFalse();
  });
});
