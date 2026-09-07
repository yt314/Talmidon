import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BetaNoticeComponent } from './shared/ui/beta-notice.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, BetaNoticeComponent],
  // תג ההרצה יושב כאן ולא בכל מסך בנפרד: כך הוא מופיע בכל עמוד, גם בהתחברות
  // ובפורטלים, ואינו יכול להופיע פעמיים כשמעטפת ומסך פנימי מוצגים יחד.
  template: '<router-outlet /><app-beta-notice />'
})
export class App {}
