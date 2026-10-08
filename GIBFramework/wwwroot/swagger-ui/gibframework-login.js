(function () {
  'use strict';

  function waitForUi(callback, tries) {
    if (window.ui && document.querySelector('.swagger-ui .information-container')) {
      callback(window.ui);
    } else if ((tries || 0) < 100) {
      setTimeout(function () { waitForUi(callback, (tries || 0) + 1); }, 100);
    }
  }

  function el(tag, attrs, text) {
    var e = document.createElement(tag);
    Object.keys(attrs || {}).forEach(function (k) { e.setAttribute(k, attrs[k]); });
    if (text) { e.textContent = text; }
    return e;
  }

  var STORE = 'gibframework.swagger.token';

  function authorize(ui, token) {
    var schema = ui.specSelectors.specJson().getIn(['components', 'securitySchemes', 'Bearer']);
    ui.authActions.authorize({ Bearer: { name: 'Bearer', schema: schema, value: token } });
  }

  waitForUi(function (ui) {
    try {
      var saved = sessionStorage.getItem(STORE);
      if (saved) { authorize(ui, saved); }
    } catch (e) { }

    var host = document.querySelector('.swagger-ui .information-container');
    var panel = el('div', { 'class': 'gibframework-login' });
    var title = el('strong', {}, 'GIB Framework Giriş');
    var user = el('input', { type: 'text', placeholder: 'Kullanıcı kodu', autocomplete: 'username' });
    var pass = el('input', { type: 'password', placeholder: 'Şifre', autocomplete: 'current-password' });
    var totp = el('input', { type: 'text', placeholder: 'Doğrulama kodu (varsa)', inputmode: 'numeric', maxlength: '6' });
    var button = el('button', { type: 'button', 'class': 'btn authorize' }, 'Giriş yap ve yetkilendir');
    var status = el('span', { 'class': 'gibframework-login-status' });
    [title, user, pass, totp, button, status].forEach(function (c) { panel.appendChild(c); });
    host.parentNode.insertBefore(panel, host.nextSibling);

    button.addEventListener('click', function () {
      status.textContent = 'Giriş yapılıyor…';
      status.className = 'gibframework-login-status';
      fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userCode: user.value, password: pass.value, totpCode: totp.value || null })
      }).then(function (r) {
        return r.json().then(function (body) { return { ok: r.ok, body: body }; });
      }).then(function (res) {
        if (!res.ok) {
          status.textContent = (res.body && (res.body.title || res.body.code)) || 'Giriş başarısız.';
          status.className = 'gibframework-login-status error';
          return;
        }
        authorize(ui, res.body.accessToken);
        try { sessionStorage.setItem(STORE, res.body.accessToken); } catch (e) { }
        pass.value = '';
        totp.value = '';
        status.textContent = 'Yetkilendirildi: ' + res.body.user.displayName + ' (' + res.body.user.roles.join(', ') + ')';
        status.className = 'gibframework-login-status ok';
      }).catch(function () {
        status.textContent = 'Sunucuya ulaşılamadı.';
        status.className = 'gibframework-login-status error';
      });
    });
  });
})();
