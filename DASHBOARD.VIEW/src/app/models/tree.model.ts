export interface TreeNode<T = unknown> {
  id: string;
  label: string;
  data?: T;
  children?: TreeNode<T>[];
  icon?: string;
  expanded?: boolean;
  selectable?: boolean;
}
