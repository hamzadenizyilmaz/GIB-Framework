const { chromium } = require(process.env.IVORY_PLAYWRIGHT || 'playwright-core');
const assert = require('node:assert/strict');
const allRoutes = [
  '/login',
  '/change-password',
  '/',
  '/invoices',
  '/invoices/new',
  '/invoices/:id',
  '/incoming',
  '/gib-portal',
  '/gib-portal/documents',
  '/customers',
  '/products',
  '/settings',
  '/settings/company',
  '/settings/invoice',
  '/settings/notes',
  '/settings/banks',
  '/settings/numbering',
  '/settings/users',
  '/settings/roles',
  '/settings/api-keys',
  '/settings/locations',
  '/settings/account',
  '/settings/security',
  '/settings/security/ip-access',
  '/settings/certificate',
  '/settings/appearance',
  '/settings/system',
  '/settings/email',
  '/settings/sms',
  '/settings/templates',
  '/settings/messages',
  '/settings/integrations',
  '/settings/integrations/:id',
  '/settings/audit',
  '/settings/tax-offices',
  '/settings/gib-portal',
  '/settings/developers',
  '/settings/tenants',
  '/settings/announcements',
  '/developers',
  '/compliance',
  '/tax-offices',
];
const base = process.env.IVORY_BASE_URL || 'http://127.0.0.1:5173/app/';
const tenant = {
  id: 'tenant-a',
  name: 'IVORY Test Firması',
  profile: {
    title: 'IVORY Test Firması',
    city: 'İstanbul',
    taxOffice: 'Test',
    taxId: '11111111111',
  },
};
const policies = [
  'InvoiceRead',
  'InvoiceFullDocument',
  'InvoiceCreate',
  'InvoiceSign',
  'InvoiceApprove',
  'InvoiceTransmit',
  'GibPortal',
  'CompanyManage',
  'SettingsManage',
  'ApiKeyManage',
  'MessagingManage',
  'UserManage',
  'PlatformAdmin',
  'IpAccessManage',
  'SecurityManage',
  'SystemRead',
  'IntegrationManage',
  'AuditRead',
  'IncomingManage',
];
const browserPath = process.env.IVORY_BROWSER;
let authMode = 'normal';
let loginMode = 'deny';
function data(path, method, body) {
  if (path.endsWith('/auth/login')) {
    if (loginMode === 'deny')
      return [
        401,
        {
          code: 'INVALID_CREDENTIALS',
          title: 'Kullanıcı kodu veya şifre hatalı.',
        },
      ];
    if (!body?.totpCode) return [401, { code: 'TOTP_REQUIRED' }];
    authMode = 'password';
    return [
      200,
      {
        accessToken: 'offline-fixture',
        user: { displayName: 'Test', mustChangePassword: true },
      },
    ];
  }
  if (path.endsWith('/auth/permissions'))
    return [
      200,
      {
        roles:
          authMode === 'restricted'
            ? []
            : ['TenantOwner', 'PlatformSuperAdmin'],
        policies: authMode === 'restricted' ? [] : policies,
        tenantId: tenant.id,
        userId: 'test',
        displayName: 'Test',
        mustChangePassword: authMode === 'password',
        mfaRequired: authMode === 'mfa',
        integratorAvailable: true,
      },
    ];
  if (path.endsWith('/auth/change-password')) {
    authMode = 'normal';
    return [204, null];
  }
  if (path.endsWith('/auth/password-reset-requests'))
    return [200, { message: 'Talep kaydedildi.' }];
  if (path.endsWith('/tenants/current')) return [200, tenant];
  if (path.endsWith('/tenants'))
    return [
      200,
      [
        tenant,
        {
          id: 'tenant-b',
          name: 'İkinci firma',
          isActive: true,
          profile: { taxId: '11111111111' },
        },
      ],
    ];
  if (path.endsWith('/logo-info')) return [200, { hasLogo: false }];
  if (path.endsWith('/invoices/overview'))
    return [
      200,
      [
        {
          currency: 'TRY',
          status: 'Sent',
          month: new Date().toISOString().slice(0, 7),
          amount: 1200,
          count: 2,
        },
      ],
    ];
  if (
    path.includes('/invoices/page') ||
    path.includes('/invoices/tasks/')
  )
    return [200, { items: [], total: 0 }];
  if (path.endsWith('/audit/events')) return [200, []];
  if (path.endsWith('/audit/verify'))
    return [200, { isValid: true, eventCount: 0, lastHash: 'fixture' }];
  if (path.endsWith('/gib-portal/status'))
    return [200, { connected: false, productionAllowed: false }];
  if (path.endsWith('/locations/status'))
    return [200, { provinces: 81, districts: 900, neighborhoods: 30000 }];
  if (path.endsWith('/compliance/tax-definitions'))
    return [
      200,
      {
        vatRates: [20, 10, 1, 0],
        exemptions: [],
        withholdings: [],
        units: [],
      },
    ];
  if (path.endsWith('/compliance/rules'))
    return [200, { rules: [], ruleSetHash: 'fixture' }];
  if (path === '/api/v1/settings')
    return [
      200,
      method === 'PUT'
        ? body
        : {
            defaults: {
              profile: 'TEMELFATURA',
              currency: 'TRY',
              vatRate: 20,
              unitCode: 'C62',
              saveCustomer: true,
            },
            noteTemplates: [],
            bankAccounts: [],
          },
    ];
  if (path.endsWith('/locations/provinces'))
    return [200, [{ id: 34, name: 'İstanbul' }]];
  if (path.endsWith('/roles')) return [200, { roles: [], policies: [] }];
  if (path.endsWith('/api-keys/roles')) return [200, []];
  if (path.endsWith('/integrations/catalog'))
    return [
      200,
      {
        kinds: [
          {
            kind: 'Whmcs',
            label: 'WHMCS',
            category: 'Hosting faturalama',
            icon: 'plug',
            fields: [],
            inbound: true,
          },
        ],
        events: [],
      },
    ];
  if (path.endsWith('/integrations/example'))
    return [
      200,
      {
        kind: 'Whmcs',
        name: 'WHMCS',
        settings: {},
        secrets: {},
        events: [],
        isActive: false,
      },
    ];
  if (path.endsWith('/integrations/example/setup'))
    return [200, { steps: [], inboundUrl: 'https://example.invalid/' }];
  if (path.endsWith('/messaging/templates')) return [200, []];
  if (path.endsWith('/messaging/settings')) return [200, { smtp: {}, sms: {} }];
  if (path.endsWith('/settings/security'))
    return [200, { policy: {}, platform: { passwordRules: [] } }];
  if (path.endsWith('/settings/ip-access')) return [200, method === 'PUT' ? body : { enabled: false, allow: [], deny: [], scope: 'Tenant', clientIp: '127.0.0.1' }];
  if (path === '/api/v1/taxpayers/1234567890') return [200, { inSystem: true, isEFaturaRegistered: true }];
  if (path.endsWith('/system/status'))
    return [
      200,
      {
        application: { version: 'test', environment: 'Development' },
        database: {},
        security: { headers: [] },
        totals: {},
      },
    ];
  if (path.endsWith('/messaging/messages'))
    return [200, { items: [], total: 0 }];
  if (path.endsWith('/invoices/example/pdf')) return [200, 'fixture pdf'];
  if (path.endsWith('/invoices/example/preview'))
    return [200, '<!doctype html><html lang="tr"><body><h1>Örnek fatura</h1></body></html>'];
  if (path.endsWith('/invoices/example/history'))
    return [200, { history: [], actions: [] }];
  if (path.endsWith('/invoices/example'))
    return [
      200,
      {
        id: 'example',
        draftNumber: 'TEST-0001',
        status: 'Draft',
        documentType: 'EArsiv',
        currency: 'TRY',
        lines: [],
        notes: [],
        customer: { title: 'Test müşteri' },
        supplier: { title: 'Test satıcı' },
        totals: { vatSubtotals: [], payableAmount: 1200 },
      },
    ];
  if (path.endsWith('/invoices') && method === 'POST')
    return [
      201,
      { id: 'example', draftNumber: 'TEST-0001', documentType: 'EArsiv' },
    ];
  if (path.endsWith('/auth/me'))
    return [200, { displayName: 'Test', roles: ['TenantOwner'] }];
  if (path.endsWith('/announcements/active')) return [200, []];
  if (path.endsWith('/auth/logout')) return [204, null];
  return [200, []];
}
async function configure(page, loggedIn) {
  if (loggedIn)
    await page.addInitScript(() => {
      if (window.top === window) sessionStorage.setItem('gibframework.token', 'offline-fixture');
    });
  await page.route('**/api/**', (r) => {
    const q = r.request(),
      p = new URL(q.url()).pathname;
    let body;
    try {
      body = q.postDataJSON();
    } catch {}
    const [status, out] = data(p, q.method(), body);
    if (p.endsWith('/preview')) return r.fulfill({ status, contentType: 'text/html; charset=utf-8', body: out });
    return r.fulfill(out === null ? { status } : { status, json: out });
  });
}
(async () => {
  const browser = await chromium.launch({
    executablePath: browserPath,
    headless: true,
  });
  try {
    const page = await browser.newPage({
      viewport: { width: 1440, height: 920 },
      colorScheme: 'light',
    });
    const failures = [];
    page.on('pageerror', (e) => failures.push(e.message));
    await configure(page, false);
    await page.goto(base + '#/login');
    await page
      .getByRole('heading', { name: 'Hesabınıza giriş yapın' })
      .waitFor();
    await page.getByLabel('Kullanıcı kodu', { exact: true }).fill('test');
    await page.getByLabel('Şifre', { exact: true }).fill('fixture');
    await page.getByRole('button', { name: 'Şifreyi göster' }).click();
    assert.equal(await page.locator('#password').getAttribute('type'), 'text');
    await page.getByRole('button', { name: 'Güvenli giriş yap' }).click();
    await page.getByText('Kullanıcı kodu veya şifre hatalı.').waitFor();
    loginMode = 'mfa';
    await page.getByLabel('Şifre', { exact: true }).fill('fixture');
    await page.getByRole('button', { name: 'Güvenli giriş yap' }).click();
    await page.getByLabel('Doğrulama kodu').fill('123456');
    await page.getByRole('button', { name: 'Güvenli giriş yap' }).click();
    await page
      .getByRole('heading', { name: 'Şifre değiştir', exact: true })
      .waitFor({ timeout: 5000 })
      .catch(async (e) => {
        console.log(
          page.url(),
          await page.locator('body').innerText(),
          failures,
        );
        throw e;
      });
    await page.getByLabel('Mevcut şifre', { exact: true }).fill('fixture');
    await page.getByLabel('Yeni şifre', { exact: true }).fill('Fixture12345');
    await page
      .getByLabel('Yeni şifre (tekrar)', { exact: true })
      .fill('Fixture12345');
    await page.getByRole('button', { name: 'Şifreyi değiştir' }).click();
    await page
      .getByRole('heading', { name: 'Hesabınıza giriş yapın' })
      .waitFor();
    await page.getByRole('button', { name: 'Şifremi unuttum' }).click();
    await page.getByRole('dialog').getByLabel('Kullanıcı kodu').fill('test');
    await page.getByRole('button', { name: 'Talep gönder' }).click();
    await page.getByText('Talep kaydedildi.').waitFor();
    await page.keyboard.press('Escape');
    await page.getByRole('dialog').waitFor({ state: 'hidden' });
    console.log(
      'PASS login failure, password visibility, MFA, forced password change and reset',
    );
    for (const width of [360, 390, 768, 1024, 1280, 1440, 1920]) {
      await page.setViewportSize({ width, height: 920 });
      assert.equal(
        await page.evaluate(
          () => document.documentElement.scrollWidth > innerWidth,
        ),
        false,
        'login ' + width,
      );
    }
    const app = await browser.newPage({
      viewport: { width: 1440, height: 920 },
      colorScheme: 'light',
    });
    app.on('pageerror', (e) => failures.push(e.message));
    await configure(app, true);
    await app.goto(base + '#/');
    await app.getByRole('heading', { name: 'Genel bakış' }).waitFor();
    await app
      .getByRole('img', { name: /Oca|Ara|Kas|Tem/ })
      .first()
      .waitFor();
    const nav = app.getByRole('navigation', { name: 'Ana menü' });
    await nav.getByRole('button', { name: 'Faturalar', exact: true }).click();
    await nav.getByRole('link', { name: 'Yeni fatura', exact: true }).focus();
    await app.keyboard.press('Escape');
    assert.equal(
      await nav
        .getByRole('button', { name: 'Faturalar', exact: true })
        .getAttribute('aria-expanded'),
      'false',
    );
    await nav.getByRole('button', { name: 'Kayıtlar', exact: true }).click();
    await app.getByRole('heading', { name: 'Genel bakış' }).click();
    assert.equal(
      await nav
        .getByRole('button', { name: 'Kayıtlar', exact: true })
        .getAttribute('aria-expanded'),
      'false',
    );
    await app.getByLabel('Faturalarda ara').fill('IVORY-42');
    const request = app.waitForRequest(
      (r) => r.url().includes('/invoices/page') && r.url().includes('IVORY-42'),
    );
    await app.getByLabel('Faturalarda ara').press('Enter');
    await request;
    await nav.getByRole('button', { name: 'Hesabım', exact: true }).click();
    await nav
      .getByRole('combobox', { name: 'Firma seç' })
      .selectOption('tenant-b');
    await app.waitForFunction(
      () => sessionStorage.getItem('gibframework.tenant') === 'tenant-b',
    );
    await app
      .getByRole('heading', { name: 'Faturalar', exact: true })
      .waitFor();
    console.log(
      'PASS header search, outside click, Escape and tenant selection',
    );
    for (const target of [
      '#/',
      '#/settings',
      '#/settings/company',
      '#/invoices',
      '#/invoices/new',
    ]) {
      await app.goto(base + target);
      await app.locator('main h1').first().waitFor({ timeout: 10000 });
      for (const width of [360, 390, 768, 1024, 1200, 1280, 1351, 1366, 1440, 1920]) {
        await app.setViewportSize({ width, height: 920 });
        assert.equal(
          await app.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
          ),
          false,
          target + ' ' + width,
        );
        if (width >= 1200) {
          const navBounds = await nav.locator('.app-primary-nav').boundingBox();
          const toolsBounds = await nav.locator('.app-nav-tools').boundingBox();
          assert.ok(navBounds.x + navBounds.width <= toolsBounds.x, `Header overlaps at ${width}px`);
        }
      }
    }
    await app.setViewportSize({ width: 390, height: 920 });
    await app.goto(base + '#/');
    await nav.getByRole('button', { name: 'Menüyü aç/kapat' }).click();
    await nav.getByRole('link', { name: 'Ayarlar', exact: true }).waitFor();
    await app.keyboard.press('Escape');
    assert.equal(
      await nav
        .getByRole('button', { name: 'Menüyü aç/kapat' })
        .getAttribute('aria-expanded'),
      'false',
    );
    console.log(
      'PASS responsive checks at 7 widths across 6 screens and mobile navigation',
    );
    await app.goto(base + '#/invoices/new');
    await app.locator('#title').waitFor();
    await app.locator('#taxId').fill('1234567890');
    const taxDialog = app.getByRole('dialog', { name: 'e-Fatura seçildi' });
    await taxDialog.waitFor();
    assert.equal(await app.locator('#isEFatura').isChecked(), true);
    await app.keyboard.press('Escape');
    await taxDialog.waitFor({ state: 'hidden' });
    await app.locator('#taxId').fill('');
    await app
      .getByRole('button', { name: /Taslak oluştur/ })
      .first()
      .click();
    await app.getByText('Eksik veya hatalı alanlar var.').waitFor();
    await app.locator('#title').fill('Yerel test alıcısı');
    await app.locator('#loc-city').selectOption('İstanbul');
    await app.locator('input[id^="n-"]').first().fill('Test hizmeti');
    await app.locator('input[id^="pr-"]').first().fill('100');
    const draftRequest = app.waitForRequest(
      (r) =>
        new URL(r.url()).pathname === '/api/v1/invoices' &&
        r.method() === 'POST',
    );
    await app
      .getByRole('button', { name: /Taslak oluştur/ })
      .first()
      .click();
    const draft = await draftRequest;
    assert.ok(draft.headers()['idempotency-key']);
    assert.equal(draft.postDataJSON().lines[0].unitPrice, 100);
    await app.getByRole('heading', { name: 'Fatura ayrıntısı' }).waitFor();
    const clipboardWrite = app.evaluate(() => {
      Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async (url) => { window.__invoiceUrl = url; } } });
    });
    await clipboardWrite;
    await app.getByRole('button', { name: 'URL Kopyala' }).click();
    await app.waitForFunction(() => window.__invoiceUrl?.endsWith('#/invoices/example'));
    await app.getByRole('button', { name: 'Faturayı görüntüle' }).click();
    await app.frameLocator('iframe[title="Fatura"]').getByRole('heading', { name: 'Örnek fatura' }).waitFor();
    await app.keyboard.press('Escape');
    const pdf = app.waitForEvent('download');
    await app.getByRole('button', { name: /PDF indir/ }).click();
    assert.match((await pdf).suggestedFilename(), /\.pdf$/);
    await app.goto(base + '#/settings/company');
    await app.locator('#c-name').fill('IVORY Test Firması Düzenlendi');
    const save = app.waitForRequest(
      (r) => r.method() === 'PUT' && r.url().endsWith('/tenants/current'),
    );
    await app.getByRole('button', { name: /Kaydet/ }).click();
    assert.equal(
      (await save).postDataJSON().name,
      'IVORY Test Firması Düzenlendi',
    );
    await app.goto(base + '#/settings/security');
    await app.getByRole('link', { name: 'IP Erişimi' }).click();
    await app.getByRole('heading', { name: 'IP Erişimi' }).waitFor();
    await app.getByLabel('IP adresi veya CIDR').fill('192.0.2.10');
    await app.getByLabel('Kural', { exact: true }).selectOption('deny');
    await app.getByRole('button', { name: 'Kural ekle' }).click();
    await app.getByRole('cell', { name: '192.0.2.10', exact: true }).waitFor();
    await app.goto(base + '#/gib-portal');
    assert.equal(await app.locator('#env-prod').isDisabled(), true);
    console.log(
      'PASS invoice validation and create, PDF download, company update and disabled production portal',
    );
    await app.goto(base + '#/settings/invoice');
    await app.getByLabel('Para birimi', { exact: true }).selectOption('EUR');
    assert.equal(
      await app.getByText('EUR', { exact: true }).last().textContent(),
      'EUR',
    );
    const [saveDefaults] = await Promise.all([
      app.waitForRequest(
        (request) =>
          request.method() === 'PUT' &&
          request.url().endsWith('/api/v1/settings'),
      ),
      app.getByRole('button', { name: 'Kaydet', exact: true }).click(),
    ]);
    assert.equal(saveDefaults.postDataJSON().defaults.currency, 'EUR');
    await app.goto(base + '#/settings/notes');
    await app.getByRole('button', { name: 'Not ekle' }).click();
    await app.getByLabel('Şablon başlığı').fill('Test notu');
    await app.getByLabel('Not metni').fill('Yerel not önizlemesi.');
    await app
      .getByText('Yerel not önizlemesi.', { exact: true })
      .last()
      .waitFor();
    const [saveNotes] = await Promise.all([
      app.waitForRequest(
        (request) =>
          request.method() === 'PUT' &&
          request.url().endsWith('/api/v1/settings'),
      ),
      app.getByRole('button', { name: 'Kaydet', exact: true }).click(),
    ]);
    assert.equal(saveNotes.postDataJSON().noteTemplates[0].title, 'Test notu');
    console.log('PASS invoice defaults and note template requests');
    await app.goto(base + '#/settings/audit');
    await app.getByRole('button', { name: 'Hash zincirini doğrula' }).click();
    await app.getByText('Bütünlük doğrulandı', { exact: true }).waitFor();
    await app.getByText('Kayıt yok.', { exact: true }).waitFor();
    console.log('PASS audit verification and empty results');
    let checks = 0;
    const problems = [];
    for (const route of allRoutes.filter((x) => x !== '/login')) {
      const current = route.replace(':id', 'example');
      try {
        const errorCount = failures.length;
        await app.goto(base + '#' + current);
        await app.reload();
        await app.locator('main h1').first().waitFor({ timeout: 3000 });
        for (const width of [360, 390, 768, 1024, 1280, 1440, 1920]) {
          await app.setViewportSize({ width, height: 920 });
          if (
            await app.evaluate(
              () => document.documentElement.scrollWidth > innerWidth,
            )
          )
            problems.push(current + ' overflow ' + width);
        }
        for (const err of failures.slice(errorCount))
          problems.push(current + ' runtime ' + err);
        checks++;
      } catch (e) {
        problems.push(
          current +
            ' ' +
            e.message.split('\n')[0] +
            ' ' +
            (await app.locator('body').innerText()).slice(0, 160) +
            ' errors=' +
            failures.slice(-2).join(','),
        );
      }
    }
    console.log(
      'Authenticated route smoke checks:',
      checks,
      'of',
      allRoutes.length - 1,
    );
    assert.deepEqual(problems, []);

    const redirects = {
      '/account': '/settings/account',
      '/integration': '/settings/api-keys',
      '/users': '/settings/users',
      '/audit': '/settings/audit',
      '/tenants': '/settings/tenants',
      '/announcements': '/settings/announcements',
      '/unknown': '/',
    };
    for (const [from, to] of Object.entries(redirects)) {
      await app.goto(base + '#' + from);
      await app.waitForURL(base + '#' + to);
    }
    await nav.getByRole('button', { name: 'Hesabım', exact: true }).click();
    await nav.getByRole('button', { name: 'Koyu temaya geç' }).click();
    assert.equal(
      await app.locator('html').getAttribute('data-bs-theme'),
      'dark',
    );
    await app.reload();
    assert.equal(
      await app.locator('html').getAttribute('data-bs-theme'),
      'dark',
    );
    await nav.getByRole('button', { name: 'Hesabım', exact: true }).click();
    await nav.getByRole('button', { name: 'Açık temaya geç' }).click();
    assert.equal(
      await app.locator('html').getAttribute('data-bs-theme'),
      'light',
    );
    console.log('PASS seven redirects and theme persistence');
    await app.setViewportSize({ width: 390, height: 920 });
    authMode = 'restricted';
    await app.goto(base + '#/invoices/new');
    await app.reload();
    await app.getByRole('heading', { name: /403/ }).waitFor();
    authMode = 'mfa';
    await app.reload();
    await app.waitForURL('**/#/settings/account');
    authMode = 'normal';
    await app.goto(base + '#/settings');
    await app.reload();
    await app.getByRole('button', { name: 'Menüyü aç/kapat' }).click();
    await app.getByRole('button', { name: 'Hesabım', exact: true }).click();
    await app.getByRole('button', { name: 'Çıkış', exact: true }).click();
    await app
      .getByRole('heading', { name: 'Hesabınıza giriş yapın' })
      .waitFor();
    console.log('PASS permission denial, MFA enrollment redirect and logout');
    assert.deepEqual(failures, []);
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exit(1);
});
