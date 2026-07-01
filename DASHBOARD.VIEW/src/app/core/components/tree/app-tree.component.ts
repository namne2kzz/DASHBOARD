import { Component, input, output, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { TreeNode } from '../../../models/tree.model';
import { ResourcePipe } from '../../pipes/resource.pipe';

@Component({
  selector: 'app-tree',
  standalone: true,
  imports: [NgClass, ResourcePipe],
  templateUrl: './app-tree.component.html',
  styleUrl: './app-tree.component.scss',
})
export class AppTreeComponent<T = unknown> {
  readonly nodes      = input<TreeNode<T>[]>([]);
  readonly nodeSelect = output<TreeNode<T>>();

  protected readonly selected = signal<string | null>(null);
  protected readonly expanded = signal<Set<string>>(new Set());

  protected toggle(node: TreeNode<T>): void {
    this.expanded.update(set => {
      const next = new Set(set);
      next.has(node.id) ? next.delete(node.id) : next.add(node.id);
      return next;
    });
  }

  protected select(node: TreeNode<T>): void {
    if (node.selectable === false) return;
    this.selected.set(node.id);
    this.nodeSelect.emit(node);
  }

  protected isExpanded(node: TreeNode<T>): boolean {
    return this.expanded().has(node.id);
  }

  protected isSelected(node: TreeNode<T>): boolean {
    return this.selected() === node.id;
  }

  protected hasChildren(node: TreeNode<T>): boolean {
    return (node.children?.length ?? 0) > 0;
  }

  protected trackById(_: number, node: TreeNode<T>): string { return node.id; }
}
