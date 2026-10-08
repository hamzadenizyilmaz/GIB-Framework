export const REPO_URL = 'https://github.com/hamzadenizyilmaz/GIB-Framework';

export function LegalStrip({ className = '' }) {
  return (
    <footer className={`legal-strip ${className}`}>
      <div className="legal-strip-inner">
        <nav className="legal-strip-links" aria-label="API dokümantasyonu">
          <a href="/swagger" target="_blank" rel="noopener noreferrer"><i className="bi bi-braces me-1" />Swagger API Dokümantasyonu</a>
          <a href="/redoc" target="_blank" rel="noopener noreferrer"><i className="bi bi-book me-1" />ReDoc API Dokümantasyonu</a>
        </nav>
        <span className="legal-strip-copy">Telif Hakları 2019-2026 - Hamza Deniz Yılmaz</span>
        <a className="legal-strip-repo" href={REPO_URL} target="_blank" rel="noopener noreferrer">
          <i className="bi bi-github me-1" />GitHub Repos: {REPO_URL.replace('https://', '')}
        </a>
      </div>
    </footer>
  );
}

export function LoginIllustration() {
  return (
    <svg className="login-illustration" viewBox="0 0 520 380" role="img" aria-label="Bilgisayar başında çalışan kişi">
      <ellipse cx="260" cy="352" rx="235" ry="12" fill="#eef3f9" />
      <rect x="40" y="40" width="150" height="190" rx="6" fill="#eef1f5" />
      {[0, 1, 2, 3, 4].map((i) => <rect key={`b${i}`} x={58 + i * 24} y={150 - i * 14} width="12" height={70 + i * 14} rx="2" fill={i % 2 ? '#9cc6f5' : '#0778e6'} opacity={0.55 + i * 0.08} />)}
      <circle cx="318" cy="38" r="14" fill="#fff" stroke="#d6dde6" strokeWidth="3" />
      <path d="M318 30v9l6 4" stroke="#b9c3cf" strokeWidth="2.5" fill="none" strokeLinecap="round" />
      <rect x="208" y="66" width="208" height="140" rx="6" fill="#a9cdf7" />
      <rect x="262" y="88" width="100" height="10" rx="2" fill="#e9f2fd" />
      <rect x="262" y="108" width="100" height="14" rx="2" fill="#fff" />
      <rect x="262" y="130" width="100" height="14" rx="2" fill="#fff" />
      <circle cx="352" cy="137" r="3" fill="#a9cdf7" />
      <rect x="262" y="154" width="100" height="16" rx="3" fill="#0778e6" />
      <rect x="292" y="159" width="40" height="6" rx="3" fill="#fff" />
      <rect x="146" y="88" width="46" height="46" rx="9" fill="#cfe3fb" />
      <circle cx="169" cy="111" r="13" fill="#0778e6" />
      <rect x="82" y="214" width="250" height="10" rx="3" fill="#0778e6" />
      <rect x="96" y="224" width="8" height="118" fill="#5c6670" />
      <rect x="306" y="224" width="8" height="118" fill="#5c6670" />
      <rect x="176" y="150" width="78" height="54" rx="4" fill="#7c8791" />
      <rect x="181" y="155" width="68" height="44" rx="2" fill="#a6b0ba" />
      <rect x="208" y="204" width="12" height="10" fill="#7c8791" />
      <path d="M58 214 q-4 -40 18 -46 l26 0 q10 4 10 22 l0 40 l-54 0 z" fill="#0778e6" />
      <rect x="58" y="252" width="60" height="9" rx="3" fill="#5c6670" />
      <rect x="84" y="261" width="6" height="66" fill="#5c6670" />
      <path d="M58 340 h60" stroke="#3c444c" strokeWidth="6" strokeLinecap="round" />
      <circle cx="58" cy="344" r="5" fill="#3c444c" />
      <circle cx="118" cy="344" r="5" fill="#3c444c" />
      <path d="M100 160 q8 -28 32 -24 q20 4 18 30 l-6 50 l-40 0 z" fill="#4a535c" />
      <path d="M140 176 l40 18 l-4 10 l-44 -14 z" fill="#4a535c" />
      <circle cx="128" cy="124" r="18" fill="#c47a5a" />
      <path d="M110 118 q4 -24 26 -20 q14 4 12 18 q-10 -6 -22 -2 q-6 2 -16 4 z" fill="#1e252b" />
      <path d="M106 212 l64 0 q22 0 36 52 l14 70 l-14 4 l-20 -66 q-6 -18 -24 -18 l-58 0 z" fill="#5aa4f0" />
      <path d="M206 332 l22 -2 l2 10 l-26 2 z" fill="#1e252b" />
      <rect x="352" y="238" width="132" height="98" rx="4" fill="#eef1f5" />
      {[0, 1, 2, 3, 4, 5].map((i) => <rect key={`s${i}`} x={366 + i * 18} y={250 + (i % 2) * 6} width="8" height={30 - (i % 2) * 6} rx="1" fill={i % 3 ? '#0778e6' : '#9cc6f5'} />)}
      {[0, 1, 2, 3, 4, 5].map((i) => <rect key={`t${i}`} x={366 + i * 18} y={296 + (i % 3) * 4} width="8" height={30 - (i % 3) * 4} rx="1" fill={i % 2 ? '#9cc6f5' : '#0778e6'} />)}
      <path d="M452 236 q-6 -40 10 -56 q14 14 6 56 z" fill="#5aa4f0" />
      <path d="M462 236 q10 -30 28 -34 q-2 24 -22 36 z" fill="#0778e6" />
      <rect x="448" y="300" width="34" height="40" rx="3" fill="#1e252b" />
      <path d="M452 236 h34 l-4 64 h-26 z" fill="#2f3a44" />
      <rect x="330" y="292" width="22" height="48" rx="2" fill="#5aa4f0" opacity="0.8" />
    </svg>
  );
}

export function LoginArcs() {
  return (
    <svg className="login-arcs" viewBox="0 0 400 300" aria-hidden="true" preserveAspectRatio="xMaxYMax meet">
      <circle cx="400" cy="300" r="250" fill="none" stroke="rgba(255,255,255,.55)" strokeWidth="1.2" />
      <circle cx="400" cy="300" r="190" fill="none" stroke="rgba(255,255,255,.55)" strokeWidth="1.2" />
    </svg>
  );
}
