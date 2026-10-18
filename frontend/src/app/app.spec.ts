import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  /**
   * שרידי התבנית של ng new בדקו כאן כותרת "Hello, talmidon" שנמחקה מזמן, והבדיקה
   * נכשלה בשקט — חבילה אדומה מלמדת להפסיק להסתכל עליה. מה שהשורש באמת מבטיח הוא
   * שהרכיבים הכלל-אפליקטיביים מורכבים פעם אחת, מכאן ולא מתוך מעטפת זו או אחרת.
   */
  it('מרכיב את תג ההרצה ואת אזהרת ההתנתקות בשורש', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('app-beta-notice')).toBeTruthy();
    expect(compiled.querySelector('app-idle-warning')).toBeTruthy();
    expect(compiled.querySelector('app-server-wake-notice')).toBeTruthy();
  });
});
