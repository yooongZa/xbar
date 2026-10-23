// Intentionally narrow experiment. No fallback to browser databases, passwords,
// all-site cookies or private X endpoints. Values must never enter logs/storage.
const cookieNames = ['auth_token', 'ct0', 'twid'];
function selectCookies(cookies, now = Date.now() / 1000) {
  const selected = [];
  for (const c of cookies) {
    if (!cookieNames.includes(c.name) || !['x.com', '.x.com'].includes(c.domain)
        || c.path !== '/' || c.secure !== true || c.partitionKey
        || typeof c.value !== 'string' || c.value.length < 1 || c.value.length > 4096
        || /[\r\n\0]/.test(c.value)
        || (c.expirationDate != null && (!Number.isFinite(c.expirationDate) || c.expirationDate <= now))) continue;
    if (selected.some(item => item.name === c.name)) throw new Error('ambiguous_session');
    selected.push({name:c.name,value:c.value,domain:c.domain,path:c.path,secure:true,
      httpOnly:c.httpOnly === true,hostOnly:c.hostOnly === true,sameSite:c.sameSite,
      session:c.session === true,expirationDate:c.expirationDate});
  }
  if (!selected.some(c => c.name === 'auth_token')) throw new Error('no_session');
  return selected;
}

if (typeof module !== "undefined") module.exports = {cookieNames, selectCookies};
