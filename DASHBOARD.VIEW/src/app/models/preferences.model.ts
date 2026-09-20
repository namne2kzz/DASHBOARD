/** Numeric date ordering the app uses when formatting dates. */
export type DateFormat = 'dmy' | 'mdy' | 'ymd';

/** A selectable display timezone. Empty string means the browser's local zone. */
export type TimezoneId = '' | 'utc7' | 'utc0' | 'utc-5' | 'utc9';

/** Interface display language. */
export type AppLanguage = 'en' | 'vi' | 'ja';
