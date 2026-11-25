/** שתי אותיות ראשונות (או ראשי תיבות של שתי המילים הראשונות) לשימוש כתמונת פרופיל גנרית. */
export function getInitials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '';
  if (parts.length === 1) return parts[0].slice(0, 2);
  return parts[0][0] + parts[1][0];
}

/**
 * ראשי התיבות כתובים בלבן, ולכן כל צבע כאן נותן לפחות 4.5:1 מול לבן. teal, cyan, orange
 * ו-lime הם בגוון 700, כי ב-600 הם נותנים 3.1–3.7:1 בלבד.
 */
const AVATAR_PALETTE = ['#0f766e', '#0e7490', '#7c3aed', '#c026d3', '#e11d48', '#c2410c', '#4d7c0f', '#4338ca'];

/** צבע יציב (לא רנדומלי מחדש בכל רינדור) שנגזר מהשם עצמו — כל מורה מקבלת אותו צבע תמיד. */
export function getAvatarColor(fullName: string): string {
  let hash = 0;
  for (let i = 0; i < fullName.length; i++) {
    hash = (hash << 5) - hash + fullName.charCodeAt(i);
    hash |= 0;
  }
  return AVATAR_PALETTE[Math.abs(hash) % AVATAR_PALETTE.length];
}
