import { Pipe, PipeTransform } from '@angular/core';
import { formatDate, formatDateTime } from '../../core/format';

/** Calendar dates (check-in, issue date): formatted literally, never shifted by the viewer's zone. */
@Pipe({ name: 'odDate' })
export class OdDatePipe implements PipeTransform {
  transform(iso: string | null | undefined): string {
    return formatDate(iso);
  }
}

/** Instants (status changes, deadlines): date and time in the viewer's zone. */
@Pipe({ name: 'odDateTime' })
export class OdDateTimePipe implements PipeTransform {
  transform(iso: string | null | undefined): string {
    return formatDateTime(iso);
  }
}
