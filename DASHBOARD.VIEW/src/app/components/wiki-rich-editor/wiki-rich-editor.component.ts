import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EditorComponent } from '@tinymce/tinymce-angular';

import 'tinymce/tinymce';
import 'tinymce/themes/silver';
import 'tinymce/icons/default';
import 'tinymce/models/dom';

import 'tinymce/plugins/advlist';
import 'tinymce/plugins/autolink';
import 'tinymce/plugins/lists';
import 'tinymce/plugins/link';
import 'tinymce/plugins/image';
import 'tinymce/plugins/charmap';
import 'tinymce/plugins/preview';
import 'tinymce/plugins/anchor';
import 'tinymce/plugins/searchreplace';
import 'tinymce/plugins/visualblocks';
import 'tinymce/plugins/code';
import 'tinymce/plugins/fullscreen';
import 'tinymce/plugins/insertdatetime';
import 'tinymce/plugins/media';
import 'tinymce/plugins/table';
import 'tinymce/plugins/help';
import 'tinymce/plugins/wordcount';

@Component({
  selector: 'app-wiki-rich-editor',
  standalone: true,
  imports: [EditorComponent, FormsModule],
  template: `
    <editor
      class="wiki-tinymce block w-full"
      [init]="init"
      [ngModel]="value"
      (ngModelChange)="valueChange.emit($event)"
    />
  `,
  styleUrl: './wiki-rich-editor.component.css',
})
export class WikiRichEditorComponent {
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();

  readonly init: EditorComponent['init'] = {
    height: 520,
    menubar: 'file edit view insert format table tools help',
    branding: false,
    promotion: false,
    license_key: 'gpl',
    skin: 'oxide-dark',
    content_css: 'dark',
    plugins: [
      'advlist',
      'autolink',
      'lists',
      'link',
      'image',
      'charmap',
      'preview',
      'anchor',
      'searchreplace',
      'visualblocks',
      'code',
      'fullscreen',
      'insertdatetime',
      'media',
      'table',
      'help',
      'wordcount',
    ],
    toolbar:
      'undo redo | blocks fontsize | bold italic underline strikethrough | ' +
      'forecolor backcolor | alignleft aligncenter alignright alignjustify | ' +
      'bullist numlist outdent indent | link image media table | charmap anchor | ' +
      'code fullscreen preview help',
    block_formats:
      'Paragraph=p; Heading 1=h1; Heading 2=h2; Heading 3=h3; Heading 4=h4; Preformatted=pre',
    font_size_formats: '12px 14px 16px 18px 24px 32px',
    table_toolbar:
      'tableprops tabledelete | tableinsertrowbefore tableinsertrowafter tabledeleterow | tableinsertcolbefore tableinsertcolafter tabledeletecol',
    content_style: `
      body {
        font-family: "Segoe UI", system-ui, sans-serif;
        font-size: 14px;
        line-height: 1.6;
        color: #e2e8f0;
        background: #0f172a;
        margin: 12px;
      }
      a { color: #38bdf8; }
      table { border-collapse: collapse; width: 100%; }
      td, th { border: 1px solid #334155; padding: 8px; }
      pre, code { background: #1e293b; border-radius: 4px; }
      blockquote { border-left: 4px solid #0ea5e9; padding-left: 12px; color: #94a3b8; }
    `,
    image_advtab: true,
    paste_data_images: true,
    automatic_uploads: false,
    file_picker_types: 'image',
    file_picker_callback: (callback, _value, meta) => {
      if (meta['filetype'] !== 'image') {
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
        if (file.size > 400_000) {
          window.alert('Image is too large for localStorage (~400KB). Use a URL instead.');
          return;
        }
        const reader = new FileReader();
        reader.onload = () => {
          callback(reader.result as string, { title: file.name, alt: file.name });
        };
        reader.readAsDataURL(file);
      };
      input.click();
    },
  };
}
