import { definePreset } from '@primeng/themes';
import Aura from '@primeng/themes/aura';

/**
 * ערכת העיצוב של תלמידון — מבוססת Aura, אך לא רק החלפת צבע: כאן מוגדרת שפת העיצוב
 * המלאה של המוצר (צבעים, רדיוסים, טיפוגרפיה, צללים ומצב כהה), כדי שכל מסך יקבל את
 * אותה תחושה בלי ש-CSS נקודתי יצטרך "לתקן" את PrimeNG.
 *
 * שלוש החלטות שמייחדות את המראה:
 *   1. primary בטורקיז (teal) — צבע המותג.
 *   2. משטחים בגוון חם (stone) במצב בהיר במקום האפור-כחלחל הרגיל — נותן תחושת "נייר"
 *      שמתאימה למוצר לימודי, ומבליט את הטורקיז הקר שמעליו.
 *   3. סולם משטחים כהה מותאם אישית (כחול-פחם עמוק) במקום zinc, כדי שהמצב הכהה ייראה
 *      מכוון ולא כמו היפוך אוטומטי.
 *
 * בנוסף מוגדרים כאן טוקנים משלנו תחת extend (‎--p-talmidon-*‎) — צללים, גרדיאנטים
 * ורקעים — כך שגם ה-CSS הגלובלי מתחלף אוטומטית בין בהיר לכהה.
 */
export const TalmidonPreset = definePreset(Aura, {
  primitive: {
    // רדיוסים רכים יותר מברירת המחדל — כרטיסים ודיאלוגים מעוגלים בעין נדיבה
    borderRadius: {
      none: '0',
      xs: '3px',
      sm: '6px',
      md: '10px',
      lg: '14px',
      xl: '18px'
    }
  },

  semantic: {
    primary: {
      50: '{teal.50}',
      100: '{teal.100}',
      200: '{teal.200}',
      300: '{teal.300}',
      400: '{teal.400}',
      500: '{teal.500}',
      600: '{teal.600}',
      700: '{teal.700}',
      800: '{teal.800}',
      900: '{teal.900}',
      950: '{teal.950}'
    },

    transitionDuration: '0.18s',

    // טבעת פוקוס עבה ובולטת — נגישות מקלדת אמיתית, לא קו שיער שקשה לראות
    focusRing: {
      width: '2px',
      style: 'solid',
      color: '{primary.color}',
      offset: '2px',
      shadow: 'none'
    },

    // שדות טופס מרווחים מעט יותר — נוח יותר במגע ובעברית
    formField: {
      paddingX: '0.875rem',
      paddingY: '0.625rem',
      borderRadius: '{border.radius.md}'
    },

    content: {
      borderRadius: '{border.radius.lg}'
    },

    colorScheme: {
      light: {
        // משטחים חמים (stone) במקום slate הקר של Aura
        surface: {
          0: '#ffffff',
          50: '{stone.50}',
          100: '{stone.100}',
          200: '{stone.200}',
          300: '{stone.300}',
          400: '{stone.400}',
          500: '{stone.500}',
          600: '{stone.600}',
          700: '{stone.700}',
          800: '{stone.800}',
          900: '{stone.900}',
          950: '{stone.950}'
        },
        // teal.500 חלש מדי על רקע לבן; 600 עומד בניגודיות AA לטקסט לבן
        primary: {
          color: '{primary.600}',
          contrastColor: '#ffffff',
          hoverColor: '{primary.700}',
          activeColor: '{primary.800}'
        },
        text: {
          color: '{surface.800}',
          hoverColor: '{surface.900}',
          mutedColor: '{surface.500}',
          hoverMutedColor: '{surface.600}'
        }
      },

      dark: {
        // סולם כהה מותאם — כחול-פחם, לא אפור ניטרלי
        surface: {
          0: '#ffffff',
          50: '#f4f6f8',
          100: '#e4e9ee',
          200: '#c6cfd9',
          300: '#9aa8b6',
          400: '#6e7e8e',
          500: '#51606f',
          600: '#3c4a58',
          700: '#2c3743',
          800: '#202a34',
          900: '#171f27',
          950: '#0f151b'
        },
        primary: {
          color: '{primary.400}',
          contrastColor: '{surface.950}',
          hoverColor: '{primary.300}',
          activeColor: '{primary.200}'
        },
        text: {
          color: '{surface.100}',
          hoverColor: '{surface.0}',
          mutedColor: '{surface.400}',
          hoverMutedColor: '{surface.300}'
        }
      }
    }
  },

  /**
   * טוקנים של תלמידון בלבד — נחשפים כמשתני CSS ‎--p-talmidon-*‎ ומתחלפים אוטומטית
   * בין בהיר לכהה, כך שאפשר להישען עליהם ב-styles.scss בלי צבעים קשיחים.
   */
  extend: {
    talmidon: {
      // סולם הגבהה אחיד לכל האפליקציה
      shadowSm: '0 1px 2px rgba(15, 23, 30, 0.06), 0 1px 3px rgba(15, 23, 30, 0.05)',
      shadowMd: '0 4px 12px -2px rgba(15, 23, 30, 0.08), 0 2px 6px -2px rgba(15, 23, 30, 0.06)',
      shadowLg: '0 12px 28px -8px rgba(15, 23, 30, 0.16), 0 6px 12px -6px rgba(15, 23, 30, 0.08)',
      shadowXl: '0 28px 56px -16px rgba(15, 23, 30, 0.22), 0 10px 20px -10px rgba(15, 23, 30, 0.1)'
    },

    colorScheme: {
      light: {
        talmidon: {
          // רקע העמוד — גוון חם עדין שמפריד בין הדף לכרטיסים הלבנים
          appBackground: '{surface.100}',
          // רצועת הפתיחה הציבורית
          heroFrom: '{primary.700}',
          heroVia: '{primary.500}',
          heroTo: '{cyan.500}',
          heroText: '#ffffff',
          // כותרת "זכוכית" נדבקת
          glassBackground: 'color-mix(in srgb, #ffffff 78%, transparent)',
          glassBorder: '{surface.200}',
          // צבע משני להדגשות (חיובים, תזכורות)
          accent: '{amber.500}',
          accentSoft: '{amber.100}',
          accentText: '{amber.800}',
          // רקעי מצב עדינים — מחליפים את ה-hex הקשיחים שהיו ב-styles.scss
          successSoft: '{green.100}',
          successText: '{green.800}',
          warnSoft: '{amber.100}',
          warnBorder: '{amber.300}',
          warnText: '{amber.800}',
          infoSoft: '{blue.100}',
          infoText: '{blue.800}',
          gridLine: 'color-mix(in srgb, {surface.900} 6%, transparent)'
        }
      },

      dark: {
        talmidon: {
          appBackground: '{surface.950}',
          heroFrom: '{primary.900}',
          heroVia: '{primary.700}',
          heroTo: '{cyan.800}',
          heroText: '{surface.0}',
          glassBackground: 'color-mix(in srgb, {surface.900} 82%, transparent)',
          glassBorder: '{surface.700}',
          accent: '{amber.400}',
          accentSoft: 'color-mix(in srgb, {amber.400}, transparent 84%)',
          accentText: '{amber.300}',
          successSoft: 'color-mix(in srgb, {green.400}, transparent 84%)',
          successText: '{green.300}',
          warnSoft: 'color-mix(in srgb, {amber.400}, transparent 86%)',
          warnBorder: 'color-mix(in srgb, {amber.400}, transparent 62%)',
          warnText: '{amber.300}',
          infoSoft: 'color-mix(in srgb, {blue.400}, transparent 84%)',
          infoText: '{blue.300}',
          gridLine: 'color-mix(in srgb, {surface.0} 7%, transparent)',
          // הצללים של המצב הבהיר נעלמים על רקע כהה — כאן הם עמוקים יותר
          shadowSm: '0 1px 2px rgba(0, 0, 0, 0.4)',
          shadowMd: '0 4px 12px -2px rgba(0, 0, 0, 0.5)',
          shadowLg: '0 12px 28px -8px rgba(0, 0, 0, 0.6)',
          shadowXl: '0 28px 56px -16px rgba(0, 0, 0, 0.7)'
        }
      }
    }
  },

  components: {
    card: {
      root: {
        borderRadius: '{border.radius.xl}',
        shadow: '{talmidon.shadow.sm}'
      },
      body: { padding: '1.5rem', gap: '0.75rem' },
      title: { fontSize: '1.125rem', fontWeight: '600' }
    },

    button: {
      root: {
        borderRadius: '{border.radius.md}',
        label: { fontWeight: '600' },
        paddingX: '1.1rem'
      }
    },

    // הרקע מגיע מהמעטפת (‎.app-shell‎) שמייצרת אפקט זכוכית — כאן רק מנטרלים את הרקע האטום
    menubar: {
      root: {
        background: 'transparent',
        borderColor: 'transparent',
        padding: '0.5rem 0'
      },
      baseItem: { borderRadius: '{border.radius.md}' },
      item: { borderRadius: '{border.radius.md}' }
    },

    tag: {
      root: {
        fontWeight: '600',
        borderRadius: '{border.radius.xl}',
        padding: '0.2rem 0.65rem'
      }
    },

    dialog: {
      root: { borderRadius: '{border.radius.xl}', shadow: '{talmidon.shadow.xl}' },
      title: { fontSize: '1.15rem', fontWeight: '700' }
    },

    datatable: {
      headerCell: { padding: '0.85rem 1rem' },
      bodyCell: { padding: '0.85rem 1rem' },
      columnTitle: { fontWeight: '700' }
    },

    toast: {
      root: { borderRadius: '{border.radius.lg}' }
    },

    popover: {
      root: { borderRadius: '{border.radius.lg}' }
    },

    // תפריט המשתמש/התראות — רדיוס תואם לשאר המעטפת
    menu: {
      root: { borderRadius: '{border.radius.lg}' }
    },

    tooltip: {
      root: { borderRadius: '{border.radius.sm}' }
    }
  }
});
