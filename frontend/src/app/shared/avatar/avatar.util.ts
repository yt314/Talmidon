/** שתי אותיות ראשונות (או ראשי תיבות של שתי המילים הראשונות) לשימוש כתמונת פרופיל גנרית. */
export function getInitials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '';
  if (parts.length === 1) return parts[0].slice(0, 2);
  return parts[0][0] + parts[1][0];
}

const AVATAR_PALETTE = ['#0d9488', '#0891b2', '#7c3aed', '#c026d3', '#e11d48', '#ea580c', '#65a30d', '#4338ca'];

/** צבע יציב (לא רנדומלי מחדש בכל רינדור) שנגזר מהשם עצמו — כל מורה מקבלת אותו צבע תמיד. */
export function getAvatarColor(fullName: string): string {
  let hash = 0;
  for (let i = 0; i < fullName.length; i++) {
    hash = (hash << 5) - hash + fullName.charCodeAt(i);
    hash |= 0;
  }
  return AVATAR_PALETTE[Math.abs(hash) % AVATAR_PALETTE.length];
}
