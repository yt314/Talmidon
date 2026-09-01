/** חומר לימוד כפי שהמורה רואה אותו. */
export interface StudentResource {
  id: string;
  studentId: string;
  title: string;
  url: string;
  description: string | null;
  createdAt: string;
}

/** חומר לימוד בפורטל ההורה/התלמיד — כולל את שם התלמיד, כי להורה יש כמה ילדים. */
export interface PortalResource extends StudentResource {
  studentName: string;
}

export interface CreateStudentResourceRequest {
  title: string;
  url: string;
  description: string | null;
}

/** סוג הקישור — נגזר מהכתובת, לצורך אייקון וצבע. אינו נשמר בשרת. */
export type ResourceKind = 'video' | 'drive' | 'document' | 'pdf' | 'link';

interface ResourceKindStyle {
  icon: string;
  /** משתנה CSS של הצבע המשויך לסוג — נצרך דרך ‎--resource-tone‎. */
  color: string;
  label: string;
}

const KIND_STYLES: Record<ResourceKind, ResourceKindStyle> = {
  video: { icon: 'pi-video', color: 'var(--p-red-500)', label: 'סרטון' },
  drive: { icon: 'pi-cloud', color: 'var(--p-amber-500)', label: 'קובץ בענן' },
  document: { icon: 'pi-file-edit', color: 'var(--p-blue-500)', label: 'מסמך' },
  pdf: { icon: 'pi-file-pdf', color: 'var(--p-orange-500)', label: 'PDF' },
  link: { icon: 'pi-link', color: 'var(--p-primary-color)', label: 'קישור' }
};

/**
 * מזהה את סוג הקישור לפי הדומיין/הסיומת שלו, כדי לתת לכל חומר אייקון וצבע משלו
 * במקום רשימת קישורים אחידה ואפורה. זיהוי כושל נופל בחזרה ל-'link' הגנרי.
 */
export function detectResourceKind(url: string): ResourceKind {
  let host: string;
  let path: string;
  try {
    const parsed = new URL(url);
    host = parsed.hostname.toLowerCase();
    path = parsed.pathname.toLowerCase();
  } catch {
    return 'link';
  }

  if (host.includes('youtube.com') || host.includes('youtu.be') || host.includes('vimeo.com')) return 'video';
  if (path.endsWith('.pdf')) return 'pdf';
  if (host.includes('docs.google.com')) return 'document';
  if (host.includes('drive.google.com') || host.includes('dropbox.com') || host.includes('onedrive')) return 'drive';
  return 'link';
}

export function resourceIcon(url: string): string {
  return KIND_STYLES[detectResourceKind(url)].icon;
}

export function resourceColor(url: string): string {
  return KIND_STYLES[detectResourceKind(url)].color;
}

export function resourceKindLabel(url: string): string {
  return KIND_STYLES[detectResourceKind(url)].label;
}

/** הדומיין בלבד — מוצג מתחת לכותרת, כדי שרואים לאן הקישור מוביל לפני שלוחצים. */
export function resourceHost(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return url;
  }
}
