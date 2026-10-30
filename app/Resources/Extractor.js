/* Runs only in WKContentWorld.defaultClient. Reads rendered analytics controls.
   No cookies, credentials, app internals, network requests or native message bridge. */
(() => {
  'use strict';
  const clean = text => (text || '').replace(/[\u200e\u200f\u202a-\u202e]/g, '').replace(/\s+/g, ' ').trim();
  const visible = el => !!el && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
  const locales = ['ko-KR', 'en-US', 'en-GB', 'ja-JP'];
  const normalize = text => clean(text).replace(/\s/g, '');
  const validNumber = text => /^(?:0|[1-9]\d{0,14}|[1-9]\d{0,2}(?:,\d{3})+)(?:\.\d{1,3})?(?: ?[KMBkmb천만억万億])?$/.test(text) && text.length <= 24;
  let selectionStep = 'start';
  let settleUntil = 0;
  let ownsCalendar = false;
  let readySamples = 0;
  const today = () => {
    const date = new Date();
    return {
      date,
      dayKey: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`,
      timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone
    };
  };
  // Use independently observable semantics; translated text is only one signal.
  // Multiple candidates are never resolved by DOM order.
  const dateButtons = () => [...document.querySelectorAll('button')].filter(el => visible(el) && (
    /^(Select date range|Select period|Date range|날짜 범위 선택|날짜 선택|기간 선택|日付範囲を選択|日付範囲を選択する|期間を選択|期間を選択する)$/i.test(el.getAttribute('aria-label') || '') ||
    el.getAttribute('aria-haspopup') === 'dialog' && !!el.querySelector('svg[data-icon="icon-calendar"]') ||
    el.getAttribute('aria-haspopup') === 'dialog' && !!document.getElementById(el.getAttribute('aria-controls'))?.querySelector('[role="grid"]')));
  const dateButton = () => { const buttons = dateButtons(); return buttons.length === 1 ? buttons[0] : null; };
  const calendar = button => {
    if (!button || button.getAttribute('aria-expanded') !== 'true') return null;
    const linked = document.getElementById(button.getAttribute('aria-controls'));
    if (visible(linked) && linked.querySelector('[role="grid"]')) return linked;
    // Some renderers attach aria-controls after opening the portal.
    const dialogs = [...document.querySelectorAll('[role="dialog"]')].filter(el => visible(el) && el.querySelector('[role="grid"]'));
    return dialogs.length === 1 && !button.getAttribute('aria-controls') ? dialogs[0] : null;
  };
  const endpoints = popup => {
    if (!popup) return [];
    const buttons = [...popup.querySelectorAll('button[aria-pressed]')].filter(el => visible(el) && !el.closest('[role="grid"]'));
    // The range selector has exactly two sibling toggles, in start/end order.
    return buttons.length === 2 && buttons[0].parentElement === buttons[1].parentElement ? buttons : [];
  };
  const dayLabels = date => locales.flatMap(locale => [
    new Intl.DateTimeFormat(locale, {weekday:'long', year:'numeric', month:'long', day:'numeric'}).format(date),
    new Intl.DateTimeFormat(locale, {year:'numeric', month:'long', day:'numeric'}).format(date)
  ]).map(normalize);
  const isDay = (button, date) => dayLabels(date).includes(normalize(button.getAttribute('aria-label')));
  const endpointMatches = (button, date) => {
    const forms = locales.flatMap(locale => ['long', 'short', 'numeric'].map(month =>
      new Intl.DateTimeFormat(locale, {month, day:'numeric'}).format(date))).map(normalize);
    const leaves = [...button.querySelectorAll('*')].filter(el => !el.children.length);
    return [button, ...leaves].some(el => forms.includes(normalize(el.textContent)) || singleToday(clean(el.textContent), date));
  };
  const calendarIsToday = (popup, date) => {
    const ends = endpoints(popup);
    const selected = popup ? [...popup.querySelectorAll('[role="gridcell"][aria-selected="true"]')].filter(visible) : [];
    // The full date on the selected cell verifies the year, which endpoint labels omit.
    return ends.length === 2 && ends.every(el => endpointMatches(el, date)) && selected.length === 1 &&
      [...selected[0].querySelectorAll('button')].some(el => visible(el) && isDay(el, date));
  };
  const singleToday = (label, date) => {
    const y = date.getFullYear(), m = date.getMonth() + 1, d = date.getDate();
    const forms = [`${y}. ${m}. ${d}.`, `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`, `${m}/${d}/${y}`];
    for (const locale of locales) {
      for (const dateStyle of ['short', 'medium', 'long']) forms.push(new Intl.DateTimeFormat(locale, {dateStyle}).format(date));
    }
    // Compare complete strings: a range ending today must never qualify.
    return forms.some(form => clean(form).replace(/\s/g, '') === clean(label).replace(/\s/g, ''));
  };
  const snapshot = () => {
    const now = today();
    const context = globalThis.XBarPageContext?.snapshot();
    const base = {dayKey: now.dayKey, timeZone: now.timeZone, account: context?.account || null, canRecover: context?.canRecover === true};
    if (!['x.com', 'www.x.com'].includes(location.hostname) || location.protocol !== 'https:') return {...base, status: 'off_page'};
    if (!context || context.conflictingAccount) return {...base, status:'loading'};
    if (context.authentication) return {...base, status:context.authentication};
    if (!/^\/i\/account_analytics(?:\/overview)?\/?$/.test(location.pathname)) return {...base, status:'off_page'};
    const button = dateButton();
    if (!button) return {...base, status: 'unavailable', action: dateButtons().length ? 'date_control_ambiguous' : 'date_control_missing'};
    const dateLabel = clean(button.textContent);
    const popup = calendar(button);
    const hasSelection = !!popup?.querySelector('[role="gridcell"][aria-selected]');
    if (!(hasSelection ? calendarIsToday(popup, now.date) : singleToday(dateLabel, now.date)))
      return {...base, dateLabel, status: 'wrong_day', action: 'date_range_unverified'};
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
    readySamples = reading.status === 'ready' ? readySamples + 1 : 0;
    // Keep live date evidence visible for both samples required by the native reader.
    // No cached range survives a close, another selection, midnight or navigation.
    if (reading.status === 'ready' && ownsCalendar && readySamples >= 2) {
      const button = dateButton();
      if (button?.getAttribute('aria-expanded') === 'true') button.click();
      ownsCalendar = false;
    }
    if (reading.status !== 'wrong_day') return reading;
    const button = dateButton();
    const popup = calendar(button);
    if (button.getAttribute('aria-expanded') !== 'true') {
      selectionStep = 'start';
      ownsCalendar = true;
      button.click();
      return {...reading, status: 'selecting', action: 'open_calendar'};
    }
    if (!popup) return {...reading, status:'selecting', action:'calendar_loading'};
    const buttons = [...popup.querySelectorAll('button')].filter(visible);
    const ends = endpoints(popup);
    const start = ends[0];
    const end = ends[1];
    const target = selectionStep === 'start' ? start : end;
    if (!target) return {...reading, status: 'manual_date', action:'calendar_structure_unrecognized'};
    if (target.getAttribute('aria-pressed') !== 'true') {
      target.click();
      return {...reading, status: 'selecting', action: 'select_' + selectionStep};
    }
    const date = today().date;
    const days = buttons.filter(el => !el.disabled && el.closest('[role="grid"]') && isDay(el, date));
    const day = days.length === 1 ? days[0] : null;
    if (!day) {
      // After midnight, a calendar may still show the previous month. Only move
      // when its explicit month/year heading identifies a supported adjacent month.
      for (const offset of [-1, 1]) {
        const month = new Date(date.getFullYear(), date.getMonth() + offset, 1);
        const labels = locales.map(locale => normalize(new Intl.DateTimeFormat(locale, {year:'numeric',month:'long'}).format(month)));
        if (!buttons.some(el => labels.includes(normalize(el.getAttribute('aria-label'))))) continue;
        const icon = offset < 0 ? 'icon-chevron-right-md' : 'icon-chevron-left-md';
        const label = offset < 0 ? 'Next month' : 'Previous month';
        const navigation = buttons.filter(el => !el.disabled && (el.getAttribute('aria-label') === label || el.querySelector(`svg[data-icon="${icon}"]`)));
        if (navigation.length === 1) { navigation[0].click(); return {...reading,status:'selecting',action:'show_current_month'}; }
      }
      return {...reading, status: 'manual_date', action:'calendar_day_unrecognized'};
    }
    // Wait for a previous selection to render before issuing another click.
    if (Date.now() < settleUntil) return {...reading,status:'selecting',action:'date_selection_settling'};
    day.click();
    selectionStep = selectionStep === 'start' ? 'end' : 'start';
    settleUntil = Date.now() + 4000;
    return {...reading, status: 'selecting', action: 'set_today'};
  };
  Object.defineProperty(globalThis, 'TodayBarExtractor', {value: Object.freeze({snapshot, advance}), configurable: false});
})();
