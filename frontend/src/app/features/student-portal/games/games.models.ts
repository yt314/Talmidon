/** מזהי המשחקים. משמשים גם כמפתח לשמירת השיא בדפדפן. */
export type GameId = 'math' | 'english' | 'reading';

export interface GameDefinition {
  id: GameId;
  title: string;
  description: string;
  icon: string;
  tone: string;
}

export const GAMES: readonly GameDefinition[] = [
  {
    id: 'math',
    title: 'חשבון מהיר',
    description: 'תרגילי חיבור, חיסור, כפל וחילוק — כמה תצליחו בדקה?',
    icon: 'pi-calculator',
    tone: 'primary'
  },
  {
    id: 'english',
    title: 'אוצר מילים באנגלית',
    description: 'מתאימים בין המילה באנגלית לתרגום בעברית.',
    icon: 'pi-language',
    tone: 'info'
  },
  {
    id: 'reading',
    title: 'הבנת הנקרא',
    description: 'קוראים קטע קצר ועונים על שאלות.',
    icon: 'pi-book',
    tone: 'success'
  }
];

export type Difficulty = 'easy' | 'medium' | 'hard';

export const DIFFICULTY_LABELS: Record<Difficulty, string> = {
  easy: 'קל',
  medium: 'בינוני',
  hard: 'מאתגר'
};

export interface MathQuestion {
  text: string;
  answer: number;
  options: number[];
}

export interface WordPair {
  english: string;
  hebrew: string;
  level: Difficulty;
}

export interface ReadingQuestion {
  question: string;
  options: string[];
  correctIndex: number;
}

export interface ReadingPassage {
  title: string;
  text: string;
  level: Difficulty;
  questions: ReadingQuestion[];
}
