/* Reads only rendered post previews and metric labels in X's own analytics list.
   No cookies, private API calls, page-internal state, or message bridge. */
(() => {
  'use strict';
  const clean = value => (value || '').replace(/[\u200e\u200f\u202a-\u202e]/g, '').replace(/\s+/g, ' ').trim();
  const visible = el => !!el && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
  const valid = text => /^(?:0|[1-9]\d{0,14}|[1-9]\d{0,2}(?:,\d{3})+)(?:\.\d{1,3})?(?: ?[KMBkmb천만억万億])?$/.test(text) && text.length <= 24;
  const snapshot = () => {
    const now = new Date();
    const dayKey = `${now.getFullYear()}-${String(now.getMonth()+1).padStart(2,'0')}-${String(now.getDate()).padStart(2,'0')}`;
    const context = globalThis.XBarPageContext?.snapshot();
    const account = context?.account || null;
    const base = {account, dayKey};
    if (location.protocol !== 'https:' || !['x.com','www.x.com'].includes(location.hostname)) return {...base,status:'off_page'};
    if (!context || context.conflictingAccount) return {...base,status:'loading'};
    if (context.authentication) return {...base,status:context.authentication};
    if (location.pathname !== '/i/account_analytics/content') return {...base,status:'off_page'};
    const q = new URL(location.href).searchParams;
    if (q.get('type') !== 'posts' || q.get('sort') !== 'impressions' || q.get('dir') !== 'desc' ||
        ['type','sort','dir','from','to','days'].some(key => q.getAll(key).length > 1)) return {...base,status:'wrong_scope'};
    const dateKey = date => `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;
    const allowedStarts = [0,1,6].map(offset => { const d = new Date(now); d.setDate(d.getDate()-offset); return dateKey(d); });
    // X canonicalizes an explicit seven-day range to days=7 after navigation.
    const week = q.get('days') === '7' && !q.has('from') && !q.has('to');
    const from = week ? allowedStarts[2] : q.get('from'), to = week ? dayKey : q.get('to');
    if ((!week && q.has('days')) || !allowedStarts.includes(from) || to !== dayKey) return {...base,status:'wrong_scope'};
    base.range = {from,to};
    const dates = [...document.querySelectorAll('button[aria-label]')].filter(el => visible(el) && /^(Select date range|날짜 범위 선택|날짜 선택|日付範囲を選択|日付範囲を選択する|期間を選択)$/i.test(el.getAttribute('aria-label')));
    if (dates.some(el => clean(el.textContent) && !XBarPageContext.matchesRange(clean(el.textContent), from, to))) return {...base,status:'wrong_scope'};
    const updating = [...document.querySelectorAll('span,div')].some(el => !el.children.length && visible(el) && /^(Updating[.…]*|업데이트 중[.…]*|更新中[.…]*)$/i.test(clean(el.textContent)));
    if (!account || updating || [...document.querySelectorAll('[role="progressbar"], [aria-busy="true"]')].some(visible)) return {...base,status:'loading'};
    const links = [...document.querySelectorAll('a[href][aria-label]')].filter(el => {
      const url = new URL(el.getAttribute('href'), location.origin);
      return url.origin === location.origin && /^\/i\/account_analytics\/content\/[0-9]{10,25}$/.test(url.pathname) && visible(el);
    });
    const posts = [], seen = new Set();
    for (const link of links.slice(0,20)) {
      const id = new URL(link.href).pathname.split('/').pop();
      if (seen.has(id)) continue;
      const row = link.parentElement;
      const metric = icon => {
        const icons = [...row.querySelectorAll(`svg[data-icon="${icon}"]`)].filter(visible);
        const values = [...new Set(icons.map(el => clean(el.parentElement.textContent)))];
        return values.length === 1 && valid(values[0]) ? values[0] : null;
      };
      const impressions = metric('icon-bar-chart'), likes = metric('icon-heart-stroke'), reposts = metric('icon-retweet-stroke');
      if (impressions === null || likes === null || reposts === null) return {...base,status:'unrecognized'};
      const preview = [...row.querySelectorAll('[dir="auto"]')].find(visible);
      const title = Array.from(clean(preview?.textContent || link.getAttribute('aria-label')).replace(/https?:\/\/t\.co\/\S+/g,'').trim()).slice(0,500).join('') || '__XBAR_MEDIA_POST__';
      seen.add(id); posts.push({id,title,impressions,likes,reposts});
    }
    if (!posts.length) {
      const empty = [...document.querySelectorAll('main, main *')].some(el => visible(el) && ['선택한 기간 동안 게시물이 없습니다.', 'No posts during the selected period.', 'No posts for the selected period.', '選択した期間に投稿はありません。', '選択した期間のポストはありません。'].includes(clean(el.textContent)));
      return empty ? {...base,status:'empty',posts:[]} : {...base,status:'unavailable'};
    }
    return {...base,status:'ready',posts};
  };
  Object.defineProperty(globalThis,'TodayBarContentExtractor',{value:Object.freeze({snapshot}),configurable:false});
})();
