import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BetaNoticeComponent } from './shared/ui/beta-notice.component';
import { IdleWarningComponent } from './shared/ui/idle-warning.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, BetaNoticeComponent, IdleWarningComponent],
  // תג ההרצה יושב כאן ולא בכל מסך בנפרד: כך הוא מופיע בכל עמוד, גם בהתחברות
  // ובפורטלים, ואינו יכול להופיע פעמיים כשמעטפת ומסך פנימי מוצגים יחד.
  // אזהרת ההתנתקות כאן מאותה סיבה — היא נוגעת לכל מסך שמאחורי התחברות.
  template: '<router-outlet /><app-beta-notice /><app-idle-warning />'
})
export class App {}
