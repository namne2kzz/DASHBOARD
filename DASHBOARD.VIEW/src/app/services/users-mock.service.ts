import { computed, Injectable, signal } from '@angular/core';

interface MockUser {
  id: string;
  name: string;
  email: string;
  avatarClass: string;
}

const MOCK_USERS: MockUser[] = [
  {
    id: 'u1',
    name: 'Alex Nguyen',
    email: 'alex.nguyen@contoso.com',
    avatarClass: 'bg-sky-600',
  },
  {
    id: 'u2',
    name: 'Mai Tran',
    email: 'mai.tran@contoso.com',
    avatarClass: 'bg-violet-600',
  },
  {
    id: 'u3',
    name: 'Dev Bot',
    email: 'bot@contoso.com',
    avatarClass: 'bg-emerald-600',
  },
];

@Injectable({ providedIn: 'root' })
export class UsersMockService {
  readonly currentUserId = signal<string>(MOCK_USERS[0].id);

  readonly allUsers = signal<MockUser[]>(MOCK_USERS);

  readonly currentUser = computed(
    () => this.allUsers().find((u) => u.id === this.currentUserId()) ?? MOCK_USERS[0],
  );

  setCurrentUser(id: string): void {
    if (this.allUsers().some((u) => u.id === id)) {
      this.currentUserId.set(id);
    }
  }

  getUser(id: string | null | undefined): MockUser | null {
    if (!id) {
      return null;
    }
    return this.allUsers().find((u) => u.id === id) ?? null;
  }

  displayName(id: string | null | undefined): string {
    return this.getUser(id)?.name ?? 'Unassigned';
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return '?';
    }
    if (parts.length === 1) {
      return parts[0].slice(0, 2).toUpperCase();
    }
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
}
