import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BetaNoticeComponent } from './shared/ui/beta-notice.component';
import { IdleWarningComponent } from './shared/ui/idle-warning.component';
import { ServerWakeNoticeComponent } from './shared/ui/server-wake-notice.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, BetaNoticeComponent, IdleWarningComponent, ServerWakeNoticeComponent],
  // תג ההרצה יושב כאן ולא בכל מסך בנפרד: כך הוא מופיע בכל עמוד, גם בהתחברות
  // ובפורטלים, ואינו יכול להופיע פעמיים כשמעטפת ומסך פנימי מוצגים יחד.
  // אזהרת ההתנתקות כאן מאותה סיבה — היא נוגעת לכל מסך שמאחורי התחברות.
  // והודעת "השרת מתעורר" כאן כי היא נוגעת דווקא למסכים הציבוריים, שבהם מבקר
  // חדש פוגש שרת שנרדם ואין לו שום סיבה להמתין בלי הסבר.
  template: '<router-outlet /><app-beta-notice /><app-idle-warning /><app-server-wake-notice />'
})
export class App {}
