/**
 * The faces N-bot can wear.
 *
 * - `normal`      the resting mark, eyes open
 * - `happy`       nothing left to do — curved eyes and a smile
 * - `sleeping`    an empty list nobody needs to act on — closed eyes, drifting z's
 * - `maintenance` a service is down — hard hat instead of the antenna
 * - `error`       something failed — crossed eyes
 */
export type NBotMood = 'normal' | 'happy' | 'sleeping' | 'maintenance' | 'error';
