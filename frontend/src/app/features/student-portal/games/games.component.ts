import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { DIFFICULTY_LABELS, Difficulty, GAMES, GameId, MathQuestion, ReadingPassage } from './games.models';
import {
  EnglishQuestion,
  nextEnglishQuestion,
  nextMathQuestion,
  nextPassage,
  passageCount,
  wordCount
} from './games.engine';

const ROUND_SECONDS = 60;
const BEST_SCORE_PREFIX = 'talmidon_game_best_';

/**
 * משחקי תרגול לתלמידה: חשבון, אוצר מילים באנגלית והבנת הנקרא.
 *
 * הכול רץ בדפדפן — אין קריאות שרת ואין עלות, ולכן אפשר לתרגל גם כשהחיבור גרוע.
 * השיא נשמר ב-localStorage בלבד: הוא נועד לעודד את התלמידה, לא לדווח למורה, ושמירה
 * בשרת הייתה דורשת טבלה שלמה עבור מספר אחד.
 */
@Component({
  selector: 'app-student-games',
  imports: [FormsModule, ButtonModule, CardModule, SelectButtonModule, TagModule, PageHeaderComponent],
  templateUrl: './games.component.html',
  styleUrl: './games.component.scss'
})
export class StudentGamesComponent implements OnDestroy {
  private readonly document = inject(DOCUMENT);

  protected readonly games = GAMES;
  protected readonly difficultyOptions = (['easy', 'medium', 'hard'] as Difficulty[]).map(value => ({
    label: DIFFICULTY_LABELS[value],
    value
  }));

  protected readonly activeGame = signal<GameId | null>(null);
  protected difficulty: Difficulty = 'easy';

  protected readonly score = signal(0);
  protected readonly streak = signal(0);
  protected readonly best = signal(0);
  protected readonly secondsLeft = signal(ROUND_SECONDS);
  protected readonly running = signal(false);
  protected readonly finished = signal(false);

  /** מה נבחר בסבב הנוכחי, ל"נכון/לא נכון" לפני המעבר לשאלה הבאה. */
  protected readonly picked = signal<number | null>(null);
  protected readonly wasCorrect = signal<boolean | null>(null);

  protected readonly mathQuestion = signal<MathQuestion | null>(null);
  protected readonly englishQuestion = signal<EnglishQuestion | null>(null);

  protected readonly passage = signal<ReadingPassage | null>(null);
  protected readonly questionIndex = signal(0);
  protected readonly readingAnswers = signal<number[]>([]);

  private timer: ReturnType<typeof setInterval> | null = null;

  protected readonly poolSize = computed(() =>
    this.activeGame() === 'english'
      ? wordCount(this.difficulty)
      : this.activeGame() === 'reading'
        ? passageCount(this.difficulty)
        : 0
  );

  protected readonly readingQuestion = computed(() => {
    const p = this.passage();
    return p ? p.questions[this.questionIndex()] ?? null : null;
  });

  ngOnDestroy(): void {
    this.stopTimer();
  }

  // ===== מסך הבחירה =====

  protected open(id: GameId): void {
    this.activeGame.set(id);
    this.resetRound();
    this.best.set(this.readBest(id));
  }

  protected back(): void {
    this.stopTimer();
    this.activeGame.set(null);
  }

  protected onDifficultyChange(): void {
    const id = this.activeGame();
    if (id) this.best.set(this.readBest(id));
    this.resetRound();
  }

  // ===== ניהול סבב =====

  protected start(): void {
    this.resetRound();
    this.running.set(true);

    if (this.activeGame() === 'reading') {
      // הבנת הנקרא אינה על זמן — קוראים ברוגע ועונים
      this.passage.set(nextPassage(this.difficulty));
      this.questionIndex.set(0);
      this.readingAnswers.set([]);
      return;
    }

    this.secondsLeft.set(ROUND_SECONDS);
    this.nextQuestion();
    this.timer = setInterval(() => {
      const left = this.secondsLeft() - 1;
      this.secondsLeft.set(left);
      if (left <= 0) this.finish();
    }, 1000);
  }

  private resetRound(): void {
    this.stopTimer();
    this.score.set(0);
    this.streak.set(0);
    this.picked.set(null);
    this.wasCorrect.set(null);
    this.finished.set(false);
    this.running.set(false);
    this.secondsLeft.set(ROUND_SECONDS);
    this.mathQuestion.set(null);
    this.englishQuestion.set(null);
    this.passage.set(null);
  }

  private nextQuestion(): void {
    this.picked.set(null);
    this.wasCorrect.set(null);
    if (this.activeGame() === 'math') this.mathQuestion.set(nextMathQuestion(this.difficulty));
    else this.englishQuestion.set(nextEnglishQuestion(this.difficulty));
  }

  private stopTimer(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  private finish(): void {
    this.stopTimer();
    this.running.set(false);
    this.finished.set(true);

    const id = this.activeGame();
    if (id && this.score() > this.readBest(id)) {
      this.writeBest(id, this.score());
      this.best.set(this.score());
    }
  }

  // ===== מענה =====

  protected answerMath(index: number): void {
    const question = this.mathQuestion();
    if (!question || this.picked() !== null) return;
    this.register(question.options[index] === question.answer, index);
  }

  protected answerEnglish(index: number): void {
    const question = this.englishQuestion();
    if (!question || this.picked() !== null) return;
    this.register(index === question.correctIndex, index);
  }

  private register(correct: boolean, index: number): void {
    this.picked.set(index);
    this.wasCorrect.set(correct);

    if (correct) {
      // רצף נכונות מזכה בבונוס — כך יש מה להפסיד, וזה מה שהופך תרגול למשחק
      this.streak.update(s => s + 1);
      this.score.update(s => s + 10 + Math.min(this.streak() - 1, 5) * 2);
    } else {
      this.streak.set(0);
    }

    setTimeout(() => {
      if (this.running()) this.nextQuestion();
    }, 650);
  }

  protected answerReading(index: number): void {
    const question = this.readingQuestion();
    if (!question || this.picked() !== null) return;

    const correct = index === question.correctIndex;
    this.picked.set(index);
    this.wasCorrect.set(correct);
    this.readingAnswers.update(a => [...a, index]);
    if (correct) this.score.update(s => s + 20);

    setTimeout(() => {
      const p = this.passage();
      if (!p) return;
      if (this.questionIndex() + 1 >= p.questions.length) {
        this.finish();
      } else {
        this.questionIndex.update(i => i + 1);
        this.picked.set(null);
        this.wasCorrect.set(null);
      }
    }, 900);
  }

  // ===== שיא אישי =====

  /**
   * שמירה מקומית עלולה להיכשל (גלישה פרטית, חסימת אחסון). כישלון כאן לא אמור
   * להפיל משחק, ולכן הכול עטוף.
   */
  private readBest(id: GameId): number {
    try {
      const raw = this.document.defaultView?.localStorage.getItem(`${BEST_SCORE_PREFIX}${id}_${this.difficulty}`);
      return raw ? Number(raw) || 0 : 0;
    } catch {
      return 0;
    }
  }

  private writeBest(id: GameId, value: number): void {
    try {
      this.document.defaultView?.localStorage.setItem(`${BEST_SCORE_PREFIX}${id}_${this.difficulty}`, String(value));
    } catch {
      // אין שיא שמור — לא נורא
    }
  }

  protected optionClass(index: number): string {
    if (this.picked() === null) return '';
    if (index === this.picked()) return this.wasCorrect() ? 'game-option-correct' : 'game-option-wrong';
    return '';
  }

  protected gameTitle(): string {
    return this.games.find(g => g.id === this.activeGame())?.title ?? '';
  }
}
