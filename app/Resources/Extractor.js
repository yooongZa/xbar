/* Runs only in WKContentWorld.defaultClient. Reads rendered analytics controls.
   No cookies, credentials, app internals, network requests or native message bridge. */
(() => {
  'use strict';
  const clean = text => (text || '').replace(/[\u200e\u200f\u202a-\u202e]/g, '').replace(/\s+/g, ' ').trim();
  const visible = el => !!el && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
  const validNumber = text => /^(?:0|[1-9]\d{0,14}|[1-9]\d{0,2}(?:,\d{3})+)(?:\.\d{1,3})?(?: ?[KMBkmb천만억万億])?$/.test(text) && text.length <= 24;
  let selectionStep = 'start';
  let settleUntil = 0;
  let ownsCalendar = false;
  const today = () => {
    const date = new Date();
    return {
      date,
      dayKey: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`,
      timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone
    };
  };
  const dateButton = () => [...document.querySelectorAll('button[aria-label]')].find(el =>
    visible(el) && /^(Select date range|날짜 범위 선택|날짜 선택|日付範囲を選択|日付範囲を選択する|期間を選択)$/i.test(el.getAttribute('aria-label')));
  const singleToday = (label, date) => {
    const y = date.getFullYear(), m = date.getMonth() + 1, d = date.getDate();
    const forms = [`${y}. ${m}. ${d}.`, `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`, `${m}/${d}/${y}`];
    for (const locale of ['ko-KR', 'en-US', 'en-GB', 'ja-JP']) {
      for (const dateStyle of ['short', 'medium', 'long']) forms.push(new Intl.DateTimeFormat(locale, {dateStyle}).format(date));
    }
    // Compare complete strings: a range ending today must never qualify.
    return forms.some(form => clean(form).replace(/\s/g, '') === clean(label).replace(/\s/g, ''));
  };
  const snapshot = () => {
    const now = today();
    const context = globalThis.XBarPageContext?.snapshot();
    const base = {dayKey: now.dayKey, timeZone: now.timeZone, account: context?.account || null};
    if (!['x.com', 'www.x.com'].includes(location.hostname) || location.protocol !== 'https:') return {...base, status: 'off_page'};
    if (!context || context.conflictingAccount) return {...base, status:'loading'};
    if (context.authentication) return {...base, status:context.authentication};
    if (!/^\/i\/account_analytics(?:\/overview)?\/?$/.test(location.pathname)) return {...base, status:'off_page'};
    const button = dateButton();
    if (!button) return {...base, status: 'unavailable', action: 'date_control_missing'};
    const dateLabel = clean(button.textContent);
    if (!singleToday(dateLabel, now.date)) return {...base, dateLabel, status: 'wrong_day'};
    if (Date.now() < settleUntil || [...document.querySelectorAll('[role="progressbar"], [aria-busy="true"]')].some(visible)) {
      return {...base, dateLabel, status: 'loading'};
    }
    const cards = [...document.querySelectorAll('button[aria-haspopup="dialog"]')].filter(el =>
      visible(el) && el !== button && /^(?:노출\s?수|Impressions|インプレッション|表示回数)(?:\s|\d|$)/i.test(clean(el.textContent)));
    if (cards.length !== 1) return {...base, dateLabel, status: 'unavailable', action: cards.length === 0 ? 'metric_card_missing' : 'metric_card_ambiguous'};
    // X animates digits in an aria-hidden subtree. The sr-only value is the single accessible number.
    const accessibleValue = cards[0].querySelector('.sr-only');
    let display = clean(accessibleValue?.textContent);
    if (!accessibleValue) {
      const leaves = [...cards[0].querySelectorAll('*')].filter(el => !el.children.length && !el.closest('[aria-hidden="true"]'));
      const values = [...new Set(leaves.map(el => clean(el.textContent)).filter(validNumber))];
      display = values.length === 1 ? values[0] : '';
    }
    if (!validNumber(display)) return {...base, dateLabel, status: 'unavailable', action: 'metric_value_unrecognized'};
    return {...base, dateLabel, status: 'ready', display};
  };
  const advance = () => {
    const reading = snapshot();
    if (reading.status === 'ready' && ownsCalendar) {
      const button = dateButton();
      if (button?.getAttribute('aria-expanded') === 'true') button.click();
      ownsCalendar = false;
    }
    if (reading.status !== 'wrong_day') return reading;
    const button = dateButton();
    const popup = document.getElementById(button.getAttribute('aria-controls'));
    if (!popup || !visible(popup) || button.getAttribute('aria-expanded') !== 'true') {
      selectionStep = 'start';
      ownsCalendar = true;
      button.click();
      return {...reading, status: 'selecting', action: 'open_calendar'};
    }
    const buttons = [...popup.querySelectorAll('button')].filter(visible);
    const start = buttons.find(el => /^(시작하기|시작|Start|開始)/i.test(clean(el.textContent)));
    const end = buttons.find(el => /^(종료|끝|End|終了)/i.test(clean(el.textContent)));
    const target = selectionStep === 'start' ? start : end;
    if (!target) return {...reading, status: 'manual_date'};
    if (target.getAttribute('aria-pressed') !== 'true') {
      target.click();
      return {...reading, status: 'selecting', action: 'select_' + selectionStep};
    }
    const date = today().date;
    const labels = ['ko-KR', 'en-US', 'en-GB', 'ja-JP'].map(locale => clean(new Intl.DateTimeFormat(locale, {
      weekday: 'long', year: 'numeric', month: 'long', day: 'numeric'
    }).format(date)));
    const day = buttons.find(el => !el.disabled && el.closest('[role="grid"]') && labels.includes(clean(el.getAttribute('aria-label'))));
    if (!day) return {...reading, status: 'manual_date'};
    day.click();
    selectionStep = selectionStep === 'start' ? 'end' : 'start';
    settleUntil = Date.now() + 4000;
    return {...reading, status: 'selecting', action: 'set_today'};
  };
  Object.defineProperty(globalThis, 'TodayBarExtractor', {value: Object.freeze({snapshot, advance}), configurable: false});
})();
