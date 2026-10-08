const KEY = 'gibframework.appearance';

export const ACCENTS = {
  blue: { label: 'Mavi', color: '#0778e6', dark: '#0667c7', rgb: '7, 120, 230' },
  indigo: { label: 'Lacivert', color: '#3f51d6', dark: '#3242b8', rgb: '63, 81, 214' },
  teal: { label: 'Turkuaz', color: '#0f9488', dark: '#0b7a70', rgb: '15, 148, 136' },
  green: { label: 'Yeşil', color: '#1e8e3e', dark: '#187634', rgb: '30, 142, 62' },
  purple: { label: 'Mor', color: '#7c3aed', dark: '#6a2fd0', rgb: '124, 58, 237' },
  orange: { label: 'Turuncu', color: '#e8590c', dark: '#c94c0a', rgb: '232, 89, 12' },
};

export const DEFAULT_APPEARANCE = { theme: 'auto', accent: 'blue', density: 'comfortable', width: 'wide' };

export function readAppearance() {
  try {
    return { ...DEFAULT_APPEARANCE, ...JSON.parse(localStorage.getItem(KEY) || '{}') };
  } catch {
    return { ...DEFAULT_APPEARANCE };
  }
}

const media = () => (typeof window !== 'undefined' && window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null);

export function resolvedTheme(theme) {
  if (theme === 'dark' || theme === 'light') return theme;
  return media()?.matches ? 'dark' : 'light';
}

export function applyAppearance(value = readAppearance()) {
  const root = document.documentElement;
  root.setAttribute('data-bs-theme', resolvedTheme(value.theme));
  const accent = ACCENTS[value.accent] || ACCENTS.blue;
  root.style.setProperty('--app-blue', accent.color);
  root.style.setProperty('--app-blue-600', accent.dark);
  root.style.setProperty('--bs-primary', accent.color);
  root.style.setProperty('--bs-primary-rgb', accent.rgb);
  root.style.setProperty('--bs-link-color', accent.color);
  root.style.setProperty('--bs-link-color-rgb', accent.rgb);
  root.style.setProperty('--bs-link-hover-color', accent.dark);
  root.classList.toggle('density-compact', value.density === 'compact');
  root.classList.toggle('layout-boxed', value.width === 'boxed');
}

export function saveAppearance(value) {
  try { localStorage.setItem(KEY, JSON.stringify(value)); } catch { }
  applyAppearance(value);
}

export function watchSystemTheme() {
  const m = media();
  if (!m) return;
  m.addEventListener('change', () => {
    if (readAppearance().theme === 'auto') applyAppearance();
  });
}
