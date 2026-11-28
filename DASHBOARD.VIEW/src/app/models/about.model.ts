/** Status of one external service, as reported by GET /system/about. */
export type ServiceStatusKind = 'healthy' | 'unreachable' | 'configured' | 'not-configured';

/** One external dependency the API talks to. */
export interface ServiceStatusDto {
  /** Display name, e.g. "SQL Server". */
  name: string;
  /** Stable machine key the UI picks its icon from, e.g. "database". */
  kind: string;
  /** Whether the service answered, failed, or was not probed at all. */
  status: ServiceStatusKind;
  /** Target host, or the reason a probe failed. */
  detail: string | null;
  /** Probe round-trip in milliseconds, or null when no probe ran. */
  latencyMs: number | null;
}

/** Everything the About dialog shows: what this build is, and what it is connected to. */
export interface AboutDto {
  /** Product name. */
  application: string;
  /** Product version, e.g. "1.0.0". */
  version: string;
  /** CI build number, or 0 for a local developer build. */
  buildNumber: number;
  /** Version and build number together, e.g. "1.0.0+build.482". */
  fullVersion: string;
  /** Source commit of the build, or null when CI did not supply one. */
  commitHash: string | null;
  /** ISO timestamp the API assembly was written. */
  buildDate: string | null;
  /** Hosting environment, e.g. "Development". */
  environment: string;
  /** Server clock at the moment of the request. */
  serverTimeUtc: string;
  /** .NET runtime version serving the request. */
  runtime: string;
  /** One entry per external dependency. */
  services: ServiceStatusDto[];
}
