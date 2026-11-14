import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, computed, inject, input, output, signal, viewChild, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ProgramImageDto } from '../../core/api.types';
import { problemMessage } from '../../core/problem';
import {
  Banner, ButtonDirective, DialogService, Field, Icon, InputDirective, Pill, ToastService,
} from '../../shared/ui';

/** Mirrors the API's limits (ProgramImage) so a bad file is refused before it is sent. */
export const IMAGE_LIMITS = { maxBytes: 5 * 1024 * 1024, maxImages: 20, coverMinWidth: 1200 };
export const IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

/** Why a picked file cannot be uploaded, or null when it can. */
export function fileProblem(file: Pick<File, 'name' | 'type' | 'size'>): string | null {
  if (!IMAGE_TYPES.includes(file.type)) return `${file.name}: only PNG, JPEG or WebP images.`;
  if (file.size === 0) return `${file.name} is empty.`;
  if (file.size > IMAGE_LIMITS.maxBytes) return `${file.name} is larger than 5 MB.`;
  return null;
}

interface ImageEdit {
  caption: string;
  altText: string;
  /** The server values this edit started from; when they change, the edit is stale. */
  from: string;
}

/** Upload, describe, order and pick the cover of a program's photos. */
@Component({
  selector: 'app-program-gallery',
  imports: [FormsModule, Banner, ButtonDirective, Field, Icon, InputDirective, Pill],
  templateUrl: './program-gallery.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './program-gallery.scss',
})
export class ProgramGallery {
  private readonly http = inject(HttpClient);
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  readonly programId = input.required<string>();
  readonly images = input.required<ProgramImageDto[]>();
  /** Something changed on the server; the parent reloads the program (and its readiness). */
  readonly changed = output<void>();

  readonly busy = signal(false);
  readonly progress = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly dragging = signal(false);

  readonly limits = IMAGE_LIMITS;
  readonly accept = IMAGE_TYPES.join(',');
  readonly full = computed(() => this.images().length >= IMAGE_LIMITS.maxImages);

  private readonly picker = viewChild<ElementRef<HTMLInputElement>>('picker');
  private edits: Record<string, ImageEdit> = {};

  edit(img: ProgramImageDto): ImageEdit {
    const from = JSON.stringify([img.caption, img.altText]);
    const current = this.edits[img.id];
    if (current && current.from === from) return current;
    return (this.edits[img.id] = { caption: img.caption ?? '', altText: img.altText ?? '', from });
  }

  dirty(img: ProgramImageDto): boolean {
    const e = this.edit(img);
    return e.caption.trim() !== (img.caption ?? '') || e.altText.trim() !== (img.altText ?? '');
  }

  kb(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  pick(): void {
    this.picker()?.nativeElement.click();
  }

  picked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    void this.upload(files);
  }

  dragOver(event: DragEvent): void {
    if (!event.dataTransfer?.types.includes('Files')) return;
    event.preventDefault();
    this.dragging.set(true);
  }

  drop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    void this.upload(Array.from(event.dataTransfer?.files ?? []));
  }

  async upload(files: File[]): Promise<void> {
    if (files.length === 0 || this.busy()) return;
    const room = IMAGE_LIMITS.maxImages - this.images().length;
    const problems = files.map(fileProblem).filter((p): p is string => p !== null);
    const ok = files.filter(f => fileProblem(f) === null).slice(0, Math.max(0, room));
    if (files.length - problems.length > room) problems.push(`A program can have at most ${IMAGE_LIMITS.maxImages} images.`);

    this.busy.set(true);
    this.error.set(null);
    let uploaded = 0;
    try {
      for (const file of ok) {
        this.progress.set(`Uploading ${uploaded + 1} of ${ok.length}…`);
        const form = new FormData();
        form.append('file', file, file.name);
        await firstValueFrom(this.http.post<ProgramImageDto>(`/api/v1/programs/${this.programId()}/images`, form));
        uploaded++;
      }
    } catch (err) {
      problems.push(problemMessage(err, 'An image could not be uploaded.'));
    } finally {
      this.busy.set(false);
      this.progress.set(null);
    }
    if (problems.length > 0) this.error.set(problems.join(' '));
    if (uploaded > 0) {
      this.toasts.show(uploaded === 1 ? 'Image uploaded' : `${uploaded} images uploaded`);
      this.changed.emit();
    }
  }

  async save(img: ProgramImageDto): Promise<void> {
    const e = this.edit(img);
    await this.act(() => this.put(img, { caption: e.caption, altText: e.altText, isCover: img.isCover }), 'Image details saved');
  }

  async makeCover(img: ProgramImageDto): Promise<void> {
    const e = this.edit(img);
    await this.act(() => this.put(img, { caption: e.caption, altText: e.altText, isCover: true }), 'Cover image changed');
  }

  async move(index: number, by: -1 | 1): Promise<void> {
    const ids = this.images().map(i => i.id);
    const target = index + by;
    if (target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    await this.act(() => firstValueFrom(this.http.put(`/api/v1/programs/${this.programId()}/images/order`, { imageIds: ids })),
      'Order saved');
  }

  async remove(img: ProgramImageDto): Promise<void> {
    const ok = await this.dialogs.confirm({
      title: 'Delete this image?',
      message: img.isCover
        ? 'It is the cover. The next image in the gallery becomes the cover.'
        : 'Agencies stop seeing it straight away.',
      confirmLabel: 'Delete image',
      destructive: true,
    });
    if (!ok) return;
    await this.act(() => firstValueFrom(this.http.delete(`/api/v1/programs/${this.programId()}/images/${img.id}`)),
      'Image deleted');
  }

  private put(img: ProgramImageDto, body: { caption: string; altText: string; isCover: boolean }): Promise<unknown> {
    return firstValueFrom(this.http.put(`/api/v1/programs/${this.programId()}/images/${img.id}`, {
      caption: body.caption.trim() || null,
      altText: body.altText.trim() || null,
      isCover: body.isCover,
    }));
  }

  private async act(request: () => Promise<unknown>, done: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await request();
      this.toasts.show(done);
      this.changed.emit();
    } catch (err) {
      this.error.set(problemMessage(err, 'The change was not saved.'));
    } finally {
      this.busy.set(false);
    }
  }
}
