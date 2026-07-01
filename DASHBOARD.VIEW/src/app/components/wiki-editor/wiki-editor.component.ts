import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { QuillModule } from 'ngx-quill';

@Component({
  selector: 'app-wiki-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, QuillModule],
  template: `
    <div class="wiki-editor w-full rounded-3xl border border-slate-800 bg-slate-950/80 p-3 shadow-inner shadow-slate-950/20">
      <quill-editor
        class="w-full min-h-[40rem] rounded-3xl bg-slate-950/90 text-slate-100"
        [modules]="modules"
        [formats]="formats"
        [(ngModel)]="value"
        (ngModelChange)="onValueChange($event)"
        (onEditorCreated)="handleEditorCreated($event)"
      ></quill-editor>
    </div>
  `,
  styleUrl: './wiki-editor.component.css',
})
export class WikiEditorComponent {
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();

  editor: any;

  formats = [
    'header',
    'bold',
    'italic',
    'underline',
    'strike',
    'blockquote',
    'code-block',
    'list',
    'bullet',
    'indent',
    'link',
    'image',
    'video',
    'color',
    'background',
    'align',
    'clean',
  ];

  modules = {
    toolbar: {
      container: [
        [{ header: [1, 2, 3, false] }],
        ['bold', 'italic', 'underline', 'strike'],
        ['blockquote', 'code-block'],
        [{ list: 'ordered' }, { list: 'bullet' }],
        [{ indent: '-1' }, { indent: '+1' }],
        [{ align: [] }],
        [{ color: [] }, { background: [] }],
        ['link', 'image', 'video'],
        ['clean'],
      ],
      handlers: {
        image: () => this.imageHandler(),
      },
    },
  };

  handleEditorCreated(editor: any): void {
    this.editor = editor;
  }

  onValueChange(value: string): void {
    this.value = value;
    this.valueChange.emit(value);
  }

  imageHandler(): void {
    if (!this.editor) {
      return;
    }
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.onchange = () => {
      const file = input.files?.[0];
      if (!file) {
        return;
      }
      const reader = new FileReader();
      reader.onload = () => {
        const base64 = reader.result as string;
        const range = this.editor.getSelection(true);
        this.editor.insertEmbed(range?.index ?? 0, 'image', base64, 'user');
        this.editor.setSelection((range?.index ?? 0) + 1);
      };
      reader.readAsDataURL(file);
    };
    input.click();
  }
}
