export const ANONYMOUS_TCKN = '11111111111';

export const digitsOnly = (value, max = 11) => String(value || '').replace(/\D/g, '').slice(0, max);

function isValidVkn(v) {
  let sum = 0;
  for (let i = 0; i < 9; i += 1) {
    const tmp = (Number(v[i]) + 9 - i) % 10;
    if (tmp === 0) continue;
    const weighted = (tmp * (2 ** (9 - i))) % 9;
    sum += weighted === 0 ? 9 : weighted;
  }
  return (10 - (sum % 10)) % 10 === Number(v[9]);
}

function isValidTckn(t) {
  const d = [...t].map(Number);
  const odd = d[0] + d[2] + d[4] + d[6] + d[8];
  const even = d[1] + d[3] + d[5] + d[7];
  if ((((odd * 7) - even) % 10 + 10) % 10 !== d[9]) return false;
  return d.slice(0, 10).reduce((a, b) => a + b, 0) % 10 === d[10];
}

export function validateTaxId(value) {
  const v = String(value || '');
  if (!v) return 'VKN/TCKN zorunludur.';
  if (!/^\d+$/.test(v)) return 'Yalnızca rakam girilebilir.';
  if (v.length === 10) return isValidVkn(v) ? null : 'VKN kontrol hanesi geçersiz.';
  if (v.length === 11) {
    if (v === '11111111111') return null;
    if (v[0] === '0') return 'TCKN 0 ile başlayamaz.';
    return isValidTckn(v) ? null : 'TCKN kontrol haneleri geçersiz.';
  }
  return 'VKN 10, TCKN 11 haneli olmalıdır.';
}

export const taxIdKind = (value) => (String(value || '').length === 11 ? 'TCKN' : 'VKN');
