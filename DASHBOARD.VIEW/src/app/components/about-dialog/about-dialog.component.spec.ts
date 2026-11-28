import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import type { AboutDto } from '../../models/about.model';
import { AboutDialogComponent } from './about-dialog.component';

const ABOUT_URL = `${environment.apiBaseUrl}/system/about`;

const RESPONSE: AboutDto = {
  application:   'NFlow Dashboard',
  version:       '1.0.0',
  buildNumber:   482,
  fullVersion:   '1.0.0+build.482',
  commitHash:    'abcdef1234567890',
  buildDate:     '2026-11-28T08:00:00Z',
  environment:   'Development',
  serverTimeUtc: '2026-11-28T09:00:00Z',
  runtime:       '10.0.0',
  services: [
    { name: 'SQL Server', kind: 'database',  status: 'healthy',     detail: 'Application database', latencyMs: 12 },
    { name: 'RabbitMQ',   kind: 'messaging', status: 'unreachable', detail: 'No response within 3s', latencyMs: 3000 },
    { name: 'SMTP',       kind: 'email',     status: 'configured',  detail: 'mail.local:587',       latencyMs: null },
    { name: 'HUB Chat',   kind: 'chat',      status: 'healthy',     detail: 'http://chat:8080',     latencyMs: 25 },
    { name: 'NMate AI',   kind: 'ai',        status: 'not-configured', detail: 'Not configured in this deployment', latencyMs: null },
    { name: 'GitHub',     kind: 'git',       status: 'healthy',     detail: 'api.github.com · 2 connections', latencyMs: 180 },
  ],
};

describe('AboutDialogComponent', () => {
  let fixture: ComponentFixture<AboutDialogComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AboutDialogComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(AboutDialogComponent);
    http    = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Opens the dialog and answers the probe request with the given payload. */
  function open(body: AboutDto = RESPONSE): void {
    fixture.detectChanges();           // triggers ngOnInit → load()
    http.expectOne(ABOUT_URL).flush(body);
    fixture.detectChanges();
  }

  it('probes the services only once the dialog is opened', () => {
    // Nothing before the first change detection: the endpoint hits real services,
    // so it must not run just because the component was constructed.
    expect(http.match(ABOUT_URL).length).toBe(0);

    fixture.detectChanges();
    const opened = http.match(ABOUT_URL);
    expect(opened.length).toBe(1);
    opened[0].flush(RESPONSE);
  });

  it('shows the full version and build number', () => {
    open();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('1.0.0+build.482');
    expect(text).toContain('482');
  });

  it('calls a local build out instead of printing build number 0', () => {
    open({ ...RESPONSE, buildNumber: 0, fullVersion: '1.0.0' });
    expect(fixture.nativeElement.textContent).toContain('local build');
  });

  it('lists every service with its status label', () => {
    open();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('SQL Server');
    expect(text).toContain('Connected');
    expect(text).toContain('RabbitMQ');
    expect(text).toContain('Unreachable');
    expect(text).toContain('SMTP');
    expect(text).toContain('Configured');
  });

  it('distinguishes a switched-off integration from a broken one', () => {
    open();
    const text: string = fixture.nativeElement.textContent;
    // NMate has no base URL here: that is a deployment choice, not a fault, so it must not
    // read as "Unreachable" the way a dead RabbitMQ does.
    expect(text).toContain('HUB Chat');
    expect(text).toContain('NMate AI');
    expect(text).toContain('Not configured');
  });

  it('shows how many Git connections are configured', () => {
    open();
    expect(fixture.nativeElement.textContent).toContain('api.github.com · 2 connections');
  });

  it('shows latency only for services that were actually probed', () => {
    open();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('12ms');
    // SMTP reports null latency — nothing was measured, so nothing is shown.
    expect(text).not.toContain('nullms');
  });

  it('surfaces a load failure instead of an empty dialog', () => {
    fixture.detectChanges();
    http.expectOne(ABOUT_URL).flush('nope', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('system information');
  });

  it('emits closed when the Close button is clicked', () => {
    open();
    let closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);

    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('button'));
    buttons.find(b => b.textContent?.trim() === 'Close')!.click();

    expect(closed).toBe(1);
  });
});
