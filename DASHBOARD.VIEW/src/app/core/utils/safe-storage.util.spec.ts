import { readStorage, removeStorage, writeStorage } from './safe-storage.util';

/**
 * The whole point of this util is the throwing case: a browser with site data blocked raises
 * on every localStorage call, and these run on the startup path.
 */
describe('safe-storage.util', () => {
  afterEach(() => localStorage.clear());

  describe('when storage works', () => {
    it('round-trips a value', () => {
      writeStorage('k', 'v');
      expect(readStorage('k')).toBe('v');
    });

    it('returns null for a missing key', () => {
      expect(readStorage('absent')).toBeNull();
    });

    it('removes a value', () => {
      writeStorage('k', 'v');
      removeStorage('k');
      expect(readStorage('k')).toBeNull();
    });
  });

  describe('when storage throws', () => {
    beforeEach(() => {
      spyOn(localStorage, 'getItem').and.throwError('SecurityError');
      spyOn(localStorage, 'setItem').and.throwError('QuotaExceededError');
      spyOn(localStorage, 'removeItem').and.throwError('SecurityError');
    });

    it('reads as null instead of throwing', () => {
      expect(() => readStorage('k')).not.toThrow();
      expect(readStorage('k')).toBeNull();
    });

    it('swallows a failed write', () => {
      expect(() => writeStorage('k', 'v')).not.toThrow();
    });

    it('swallows a failed remove', () => {
      expect(() => removeStorage('k')).not.toThrow();
    });
  });
});
