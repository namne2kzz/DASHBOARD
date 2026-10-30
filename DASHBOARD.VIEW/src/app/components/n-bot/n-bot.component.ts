import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NBotMood } from '../../models/n-bot.model';

/**
 * N-bot: the product mark given a face, for the places a screen would otherwise be blank.
 *
 * Every mood is the same hexagon with different eyes, so the character stays recognisable
 * while carrying the tone of the moment — asleep on an empty list, cheerful when the work is
 * finished, hard-hatted while NMate is down.
 */
@Component({
  selector: 'app-n-bot',
  templateUrl: './n-bot.component.html',
  styleUrl: './n-bot.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NBotComponent {
  /** Which face to wear. */
  readonly mood = input<NBotMood>('normal');

  /** Rendered width and height in pixels. */
  readonly size = input(88);

  /** Sentence describing the bot to a screen reader, or empty to hide it entirely. */
  readonly label = input('');

  /** Moods that keep the antenna; the hard hat replaces it. */
  readonly hasAntenna = computed(() => this.mood() !== 'maintenance');

  /** The antenna tip reports status on its own: coral normally, grey asleep, red on error. */
  readonly tipColor = computed(() => {
    switch (this.mood()) {
      case 'sleeping': return '#8B949E';
      case 'error':    return '#CF222E';
      default:         return '#F0506E';
    }
  });
}
