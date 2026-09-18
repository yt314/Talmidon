import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

const APP_NAME = 'תלמידון';

/**
 * מעדכן את כותרת הדף לפי המסלול.
 *
 * באפליקציה של עמוד יחיד הכותרת לא מתחלפת מעצמה, וכל המסכים נראו בלשונית
 * הדפדפן ובהיסטוריה בשם אחד — "תלמידון". למי שעובדת עם קורא מסך זו גם ההודעה
 * היחידה שאומרת "עברת מסך", כי אין טעינה מחדש שתכריז על זה.
 *
 * הכותרת של כל מסלול זהה לכותרת שרואים על המסך, כדי שלשונית והמסך יאמרו
 * אותו דבר.
 */
@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page ? `${page} · ${APP_NAME}` : APP_NAME);
  }
}
