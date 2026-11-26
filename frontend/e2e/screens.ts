/**
 * המסכים העיקריים של כל תפקיד, לבדיקות שעוברות על כולם (נגישות, תצוגה בטלפון). מסך
 * חדש שנוסף לתפריט של תפקיד נוסף גם כאן, וכך נבדק בכל אחת מהן.
 */
export const SCREENS = {
  public: ['/', '/login', '/register'],
  teacher: ['dashboard', 'students', 'lessons', 'payments', 'reports', 'messages', 'profile'].map(p => `/app/${p}`),
  parent: ['dashboard', 'lessons', 'notes', 'payments', 'resources', 'messages'].map(p => `/parent/${p}`),
  student: ['dashboard', 'lessons', 'notes', 'resources', 'games', 'messages'].map(p => `/student/${p}`)
};
