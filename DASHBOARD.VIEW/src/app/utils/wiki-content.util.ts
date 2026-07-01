import { renderMarkdown } from './markdown.util';

/** Normalize legacy Markdown pages to HTML for the rich editor. */
export function ensureWikiHtml(content: string): string {
  const trimmed = content.trim();
  if (!trimmed) {
    return '<p></p>';
  }
  if (trimmed.startsWith('<')) {
    return content;
  }
  return renderMarkdown(content);
}
