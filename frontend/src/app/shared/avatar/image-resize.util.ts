/**
 * חותך את התמונה לריבוע מרכזי ומקטין אותה, בדפדפן, לפני ההעלאה.
 *
 * למה בדפדפן: כך השרת לא צריך ספריית עיבוד תמונה, התעבורה קטנה, והתמונה
 * שנשמרת במסד היא כבר בגודל שבו היא מוצגת. תמונה מהטלפון היא כמה מגה-בייט —
 * אחרי הטיפול היא בסביבות 30–60KB.
 */
export async function cropToSquareJpeg(file: File, size = 400, quality = 0.85): Promise<Blob> {
  const bitmap = await createImageBitmap(file);
  try {
    // חיתוך מרכזי: לוקחים את הריבוע הגדול ביותר שנכנס בתמונה
    const edge = Math.min(bitmap.width, bitmap.height);
    const sx = (bitmap.width - edge) / 2;
    const sy = (bitmap.height - edge) / 2;

    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('2d context unavailable');
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(bitmap, sx, sy, edge, edge, 0, 0, size, size);

    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(blob => (blob ? resolve(blob) : reject(new Error('toBlob failed'))), 'image/jpeg', quality)
    );
  } finally {
    bitmap.close();
  }
}
