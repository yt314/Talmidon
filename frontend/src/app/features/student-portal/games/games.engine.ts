import { Difficulty, MathQuestion, ReadingPassage, WordPair } from './games.models';
import { READING_PASSAGES, WORD_PAIRS } from './games.content';

/** בוחר איבר אקראי מתוך מערך לא ריק. */
export function pick<T>(items: readonly T[]): T {
  return items[Math.floor(Math.random() * items.length)];
}

export function shuffle<T>(items: readonly T[]): T[] {
  const copy = [...items];
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy;
}

const RANGES: Record<Difficulty, { max: number; ops: string[] }> = {
  easy: { max: 10, ops: ['+', '-'] },
  medium: { max: 20, ops: ['+', '-', '×'] },
  hard: { max: 12, ops: ['+', '-', '×', ':'] }
};

/**
 * מייצר תרגיל חשבון עם ארבע אפשרויות.
 *
 * חיסור וחילוק נבנים "אחורה" מהתוצאה, כדי שהתשובה תמיד תהיה מספר שלם ואי-שלילי —
 * תלמידה בכיתה ג' שמקבלת ‎3 − 8‎ פשוט נתקעת.
 */
export function nextMathQuestion(difficulty: Difficulty): MathQuestion {
  const { max, ops } = RANGES[difficulty];
  const op = pick(ops);
  const n = () => 1 + Math.floor(Math.random() * max);

  let a: number;
  let b: number;
  let answer: number;

  switch (op) {
    case '+':
      a = n(); b = n(); answer = a + b;
      break;
    case '-':
      b = n(); answer = n(); a = b + answer;   // a - b = answer, תמיד אי-שלילי
      break;
    case '×':
      a = n(); b = n(); answer = a * b;
      break;
    default:
      b = n(); answer = n(); a = b * answer;   // a : b = answer, תמיד שלם
      break;
  }

  return { text: `${a} ${op} ${b}`, answer, options: buildOptions(answer) };
}

/**
 * שלוש הסחות דעת סביב התשובה. כולן שונות זו מזו ואי-שליליות, אחרת אפשר לפסול
 * אפשרות בלי לחשב.
 */
function buildOptions(answer: number): number[] {
  const options = new Set<number>([answer]);
  let spread = 1;
  while (options.size < 4) {
    const delta = (Math.random() < 0.5 ? -1 : 1) * (1 + Math.floor(Math.random() * spread));
    const candidate = answer + delta;
    if (candidate >= 0) options.add(candidate);
    spread++;
  }
  return shuffle([...options]);
}

export interface EnglishQuestion {
  prompt: string;
  options: string[];
  correctIndex: number;
  /** האם הוצגה המילה באנגלית והתשובות בעברית, או להפך. */
  englishToHebrew: boolean;
}

export function nextEnglishQuestion(difficulty: Difficulty): EnglishQuestion {
  const pool = WORD_PAIRS.filter(w => w.level === difficulty);
  const correct = pick(pool);
  const englishToHebrew = Math.random() < 0.5;

  const distractors = shuffle(pool.filter(w => w.english !== correct.english)).slice(0, 3);
  const chosen = shuffle([correct, ...distractors]);

  return {
    prompt: englishToHebrew ? correct.english : correct.hebrew,
    options: chosen.map(w => (englishToHebrew ? w.hebrew : w.english)),
    correctIndex: chosen.findIndex(w => w.english === correct.english),
    englishToHebrew
  };
}

export function nextPassage(difficulty: Difficulty, exclude?: string): ReadingPassage {
  const pool = READING_PASSAGES.filter(p => p.level === difficulty && p.title !== exclude);
  return pool.length > 0 ? pick(pool) : pick(READING_PASSAGES.filter(p => p.level === difficulty));
}

export function wordCount(difficulty: Difficulty): number {
  return WORD_PAIRS.filter(w => w.level === difficulty).length;
}

export function passageCount(difficulty: Difficulty): number {
  return READING_PASSAGES.filter(p => p.level === difficulty).length;
}
