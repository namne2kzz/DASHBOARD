/** Numeric date ordering the app uses when formatting dates. */
export type DateFormat = 'dmy' | 'mdy' | 'ymd';

/**
 * A selectable display timezone, as an IANA zone id (e.g. `"Asia/Ho_Chi_Minh"`).
 * Empty string means "follow the browser's own zone".
 *
 * @remarks
 * IANA ids rather than fixed offsets because an offset cannot express daylight saving: Berlin is
 * UTC+1 in winter and UTC+2 in summer, so a stored `"+0100"` is wrong for half the year. The id names
 * the *zone*; the offset is derived per instant. The same ids are what `Intl` and .NET
 * `TimeZoneInfo.FindSystemTimeZoneById` accept, so one value works on both ends.
 */
export type TimezoneId = string;

/** Interface display language. */
export type AppLanguage = 'en' | 'vi' | 'ja';
