import { marked } from 'marked';

marked.setOptions({
  gfm: true,
  breaks: true,
});

/** Renders Markdown to sanitized-ish HTML for in-app preview. */
export function renderMarkdown(source: string): string {
  if (!source.trim()) {
    return '<p class="text-slate-500 italic">No content yet.</p>';
  }
  return marked.parse(source, { async: false }) as string;
}
