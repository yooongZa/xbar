const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const resources = path.join(__dirname, '../SafariExtension/Resources');
function harness(native, locale='ko') {
 const messages=JSON.parse(fs.readFileSync(path.join(resources,'_locales',locale,'messages.json'),'utf8'));
 const calls=[];
 const nodes={status:{textContent:''},send:{disabled:false,textContent:'𝕏-Bar에 연결'},consent:{checked:false}};
 let click;
 nodes.send.addEventListener=(_,fn)=>click=fn;
 const context=vm.createContext({document:{documentElement:{},querySelectorAll:()=>[],querySelector:s=>nodes[s.slice(1)]}, browser:{
  i18n:{getMessage:key=>messages[key]?.message||'',getUILanguage:()=>locale},
  runtime:{sendNativeMessage:async(_,m)=>{calls.push({action:m.action,id:m.id}); return native(m);}},
  cookies:{get:async({name})=>{calls.push({action:'cookie',name});return {name,value:'synthetic-only',domain:'.x.com',path:'/',secure:true};}}
 }});
 for (const file of ['localize.js','cookie-policy.js','popup.js']) vm.runInContext(fs.readFileSync(path.join(resources,file),'utf8'),context);
 return {calls,nodes,click:()=>click()};
}
test('no handshake or cookie access during preparation or before consent', async()=>{
 const h=harness(()=>({ok:true,id:'a'}));
 assert.equal(h.calls.length,0); await h.click(); assert.equal(h.calls.length,0);
});
test('failed preparation never reads cookies and allows retry without logging out', async()=>{
 const h=harness(()=>({ok:false,error:'app_not_ready'}));h.nodes.consent.checked=true;
 await h.click();assert.deepEqual(h.calls,[{action:'prepare',id:undefined}]);
 assert.equal(h.nodes.send.textContent,'다시 연결');assert.equal(h.nodes.send.disabled,false);assert.equal(h.nodes.consent.checked,true);
});
test('one click prepares first, reads only three X cookies, then sends that request', async()=>{
 const h=harness(m=>m.action==='prepare'?{ok:true,id:'request-one'}:{ok:true});h.nodes.consent.checked=true;
 await h.click();assert.deepEqual(h.calls.map(c=>c.action),['prepare','cookie','cookie','cookie','connect']);
 assert.deepEqual(h.calls.filter(c=>c.action==='cookie').map(c=>c.name),['auth_token','ct0','twid']);
 assert.equal(h.calls.at(-1).id,'request-one');assert.equal(h.nodes.send.disabled,true);assert.equal(h.nodes.consent.checked,false);
 await h.click();assert.equal(h.calls.length,5);
});
test('expired transfer retries with a new request; simultaneous clicks are ignored', async()=>{
 let release;let count=0;
 const h=harness(async m=>{
  if(m.action==='prepare'){count++;if(count===1)await new Promise(r=>release=r);return {ok:true,id:`request-${count}`};}
  return {ok:count===2};
 });h.nodes.consent.checked=true;
 const first=h.click();await h.click();assert.equal(count,1);release();await first;
 assert.equal(h.nodes.send.disabled,false);assert.equal(h.nodes.consent.checked,true);
 await h.click();assert.equal(count,2);assert.equal(h.calls.at(-1).id,'request-2');assert.equal(h.nodes.send.disabled,true);
});

for (const locale of ['ko','en','ja']) {
 test(`localized consent, error and retry keep the handshake unchanged: ${locale}`, async()=>{
  const messages=JSON.parse(fs.readFileSync(path.join(resources,'_locales',locale,'messages.json'),'utf8'));
  const h=harness(()=>({ok:false}),locale);
  await h.click(); assert.equal(h.nodes.status.textContent,messages.confirm_first.message);
  assert.equal(h.calls.length,0);
  h.nodes.consent.checked=true;await h.click();
  assert.equal(h.nodes.status.textContent,messages.app_not_ready.message);
  assert.equal(h.nodes.send.textContent,messages.retry.message);
  assert.equal(h.calls.filter(c=>c.action==='cookie').length,0);
 });
}
