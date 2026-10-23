const status = document.querySelector('#status');
const send = document.querySelector('#send');
const consent = document.querySelector('#consent');
send.addEventListener('click', async () => {
 if (send.disabled) return;
 if (!consent.checked) {status.textContent=t('confirm_first');return;}
 send.disabled=true;
 let cookies=[];
 let success=false;
 try {
  status.textContent=t('connecting');
  const ready=await browser.runtime.sendNativeMessage('com.limod.xbar',{action:'prepare'});
  if (!ready?.ok) {status.textContent=t('app_not_ready');return;}
  for(const name of cookieNames){const c=await browser.cookies.get({url:'https://x.com/',name});if(c)cookies.push(c);}
  cookies=selectCookies(cookies);
  const result=await browser.runtime.sendNativeMessage('com.limod.xbar',{action:'connect',id:ready.id,cookies});
  success=result?.ok===true;
  status.textContent=success?t('success'):t('expired');
 } catch(error) {
  status.textContent=error.message==='no_session'?t('no_session'):t('failed');
 } finally {
  cookies.length=0;
  send.textContent=success?t('checking'):t('retry');
  send.disabled=success;
  if(success)consent.checked=false;
 }
});
