/* Shared, isolated-world identity/authentication checks. Never reads credential values. */
(() => {
  'use strict';
  const clean = text => (text || '').replace(/[\u200e\u200f\u202a-\u202e]/g, '').replace(/\s+/g, ' ').trim();
  const visible = el => !!el && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
  let credentialsStarted = false;
  document.addEventListener('input', event => {
    if (event.target instanceof HTMLElement && visible(event.target) &&
        (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement || event.target.isContentEditable)) credentialsStarted = true;
  }, true);
  const snapshot = () => {
    const handles = [];
    for (const button of document.querySelectorAll('button[aria-label]')) {
      if (visible(button) && /^(Account menu|계정 메뉴|アカウントメニュー|アカウント メニュー)$/i.test(button.getAttribute('aria-label'))) {
        const handle = clean(button.textContent).match(/@[A-Za-z0-9_]{1,15}\b/)?.[0];
        if (handle) handles.push(handle);
      }
    }
    for (const link of document.querySelectorAll('a[href]')) {
      if (!visible(link) || !/^(Profile|프로필|プロフィール)$/i.test(link.getAttribute('aria-label') || clean(link.textContent))) continue;
      const path = link.getAttribute('href');
      if (/^\/[A-Za-z0-9_]{1,15}$/.test(path)) handles.push('@' + path.slice(1));
    }
    const conflictingAccount = new Set(handles.map(value => value.toLowerCase())).size > 1;
    const account = conflictingAccount ? null : handles[0] || null;
    const loginRoute = /^\/i\/flow\/(login|signup)/.test(location.pathname) || /^\/i\/jf\/onboarding\//.test(location.pathname);
    const authenticationRoute = loginRoute || /^\/(login|signup|account\/access)(?:\/|$)/.test(location.pathname) || /^\/i\/flow\//.test(location.pathname);
    const inputs = [...document.querySelectorAll('input')].filter(visible);
    const authenticationStep = inputs.some(el => el.type === 'password' || ['current-password', 'one-time-code'].includes(el.autocomplete));
    const editing = credentialsStarted || inputs.includes(document.activeElement) ||
      document.activeElement instanceof HTMLTextAreaElement || document.activeElement?.isContentEditable;
    const authentication = authenticationRoute || authenticationStep
      ? (loginRoute && account && !editing && !authenticationStep ? 'login_session_detected' : 'login_required') : null;
    return {account, conflictingAccount, authentication, canRecover: !conflictingAccount && !authentication && !editing};
  };
  // Validate a rendered date control when present, including locale-compacted ranges.
  const matchesRange = (label, from, to) => {
    const date = key => new Date(...key.split('-').map((n, i) => Number(n) - (i === 1 ? 1 : 0)));
    const start = date(from), end = date(to);
    const normalize = text => clean(text).replace(/\s/g, '').replace(/[–—~〜～]/g, '-');
    const forms = d => [`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`,
      `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`];
    const starts = forms(start), ends = forms(end), ranges = [];
    for (const locale of ['ko-KR','en-US','en-GB','ja-JP']) for (const dateStyle of ['short','medium','long']) {
      const format = new Intl.DateTimeFormat(locale,{dateStyle});
      starts.push(format.format(start)); ends.push(format.format(end));
      ranges.push(format.formatRange(start,end));
    }
    if (from === to) ranges.push(...starts);
    else for (const a of starts) for (const b of ends) ranges.push(`${a} - ${b}`);
    return ranges.some(value => normalize(value) === normalize(label));
  };
  Object.defineProperty(globalThis,'XBarPageContext',{value:Object.freeze({snapshot,matchesRange}), configurable:false});
})();
