/* Gazely account portal. No billing tables are written from the browser. */
const $=id=>document.getElementById(id);
const show=(id,on)=>{ $(id).hidden=!on; };
const message=text=>{ $('notice').textContent=text||''; };
const countries='AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW'.split(' ');
let config,session,account;
function setSession(value){session=value;if(value)sessionStorage.setItem('gazely-session',JSON.stringify(value));else sessionStorage.removeItem('gazely-session');}
async function request(path,body,token){
  const response=await fetch(config.supabaseUrl+path,{method:'POST',headers:{'content-type':'application/json','apikey':config.publishableKey,...(token?{authorization:'Bearer '+token}:{})},body:JSON.stringify(body)});
  const result=await response.json().catch(()=>({}));if(!response.ok)throw new Error(result.msg||result.error_description||result.error||'Request failed');return result;
}
async function currentToken(){
  if(!session)throw new Error('Sign in required');
  if(Date.now()<session.expires_at*1000-60000)return session.access_token;
  const fresh=await request('/auth/v1/token?grant_type=refresh_token',{refresh_token:session.refresh_token});
  setSession({...fresh,expires_at:Math.floor(Date.now()/1000)+fresh.expires_in});return session.access_token;
}
async function api(action,fields={}){return request('/functions/v1/gazely-api',{action,...fields},await currentToken());}
const date=value=>value?new Intl.DateTimeFormat(undefined,{dateStyle:'medium'}).format(new Date(value)):'—';
function money(minor){return new Intl.NumberFormat('en-NG',{style:'currency',currency:'NGN',maximumFractionDigits:0}).format(minor/100);}
function draw(status){
  account=status;show('signin',false);show('new-church',false);show('dashboard',true);
  $('church-title').textContent=status.church.name;
  const launchFree=!status.billing_enabled;
  $('plan-name').textContent=launchFree?'Free · full access':status.plan==='pro'?'Pro':'Free';
  $('plan-caption').textContent=launchFree?'Every Gazely feature is included after sign-in.':
    status.plan==='pro'?'Local recognition with advanced automation.':'Local recognition, scripture detection and Mini.';
  const sub=status.subscription;
  $('renewal').textContent=launchFree?'No payment is required during the free launch.':
    sub.state==='cancel_at_period_end'?'Pro continues until '+date(sub.paid_through)+'.':
    sub.state==='past_due'?'Renewal failed. Grace ends '+date(sub.grace_until)+'.':
    status.plan==='pro'?'Paid through '+date(sub.paid_through)+'.':'No subscription yet.';
  show('billing',!launchFree);
  $('repair').hidden=sub.state!=='past_due';$('manage').hidden=!sub.can_manage;$('cancel').hidden=!sub.can_manage||sub.state==='cancel_at_period_end';
  $('monthly').hidden=status.plan==='pro'&&sub.state!=='past_due';
  $('yearly').hidden=status.plan==='pro'&&sub.state!=='past_due';
  const usage=status.usage;
  $('usage-value').textContent=usage.allowance_seconds==null?'Unlimited':Math.max(0,Math.floor((3600-usage.accepted_seconds)/60))+' min left';
  $('usage-detail').textContent=usage.allowance_seconds==null?'Listening is unlimited.':
    Math.floor(usage.accepted_seconds/60)+' of 60 minutes used · resets '+date(usage.period_end);
  $('usage-fill').style.width=usage.allowance_seconds==null?'0%':Math.min(100,100*usage.accepted_seconds/3600)+'%';
  const active=(status.devices||[]).filter(d=>!d.deactivated_at);
  $('device-limit').textContent=active.length+' of '+status.device_limit+' active';
  $('devices').replaceChildren();
  for(const item of active){
    const row=document.createElement('div');row.className='device';
    const info=document.createElement('div'),name=document.createElement('strong'),when=document.createElement('p');
    name.textContent=item.label;when.textContent='Activated '+date(item.activated_at);info.append(name,when);
    const button=document.createElement('button');button.className='quiet';button.textContent='Deactivate';
    button.addEventListener('click',async()=>{if(!confirm('Deactivate '+item.label+'?'))return;await run(async()=>draw((await api('deactivate_device',{device_id:item.id})).status));});
    row.append(info,button);$('devices').append(row);
  }
  const region=['DZ','AO','BJ','BW','BF','BI','CV','CM','CF','TD','KM','CG','CD','CI','DJ','EG','GQ','ER','SZ','ET','GA','GM','GH','GN','GW','KE','LS','LR','LY','MG','MW','ML','MR','MU','MA','MZ','NA','NE','NG','RW','ST','SN','SC','SL','SO','ZA','SS','SD','TZ','TG','TN','UG','ZM','ZW'].includes(status.church.billing_country);
  $('price-note').textContent='Actual Paystack charge: '+money(region?250000:665000)+'/month or '+money(region?2500000:6650000)+'/year, in NGN.';
}
async function load(){
  if(!session){show('signin',true);show('dashboard',false);return;}
  try{draw(await api('status'));}catch(error){
    if(/owner required/i.test(error.message)){show('new-church',true);show('signin',false);show('dashboard',false);return;}
    if(/sign in|required|invalid jwt/i.test(error.message)){setSession(null);show('signin',true);show('dashboard',false);return;}
    throw error;
  }
}
async function run(work){
  message('');document.querySelectorAll('button').forEach(b=>b.disabled=true);
  try{await work();}catch(error){message(error.message||'Something went wrong. Please retry.');}
  finally{document.querySelectorAll('button').forEach(b=>b.disabled=false);}
}
async function initialize(){
  const response=await fetch('/config.json',{cache:'no-store'});
  if(!response.ok)throw new Error('Portal configuration is not ready.');
  config=await response.json();
  if(!/^https:\/\/[a-z0-9-]+\.supabase\.co$/.test(config.supabaseUrl)||!config.publishableKey)
    throw new Error('Portal configuration is invalid.');
  const labels=new Intl.DisplayNames([navigator.language],{type:'region'});
  for(const code of countries){const option=document.createElement('option');option.value=code;option.textContent=labels.of(code)+' ('+code+')';$('country').append(option);}
  $('country').value='NG';
  try{session=JSON.parse(sessionStorage.getItem('gazely-session')||'null');}catch{session=null;}
  $('email-form').addEventListener('submit',event=>{event.preventDefault();run(async()=>{
    const email=$('email').value.trim().toLowerCase();await request('/auth/v1/otp',{email,create_user:true});
    show('code-form',true);message('Check your email for the one-time code.');
  });});
  $('code-form').addEventListener('submit',event=>{event.preventDefault();run(async()=>{
    const result=await request('/auth/v1/verify',{type:'email',email:$('email').value.trim().toLowerCase(),token:$('code').value.trim()});
    setSession({...result,expires_at:Math.floor(Date.now()/1000)+result.expires_in});await load();
  });});
  $('church-form').addEventListener('submit',event=>{event.preventDefault();run(async()=>{
    const response=await api('create_church',{name:$('church-name').value.trim(),billing_country:$('country').value});draw(response.status);
  });});
  $('signout').addEventListener('click',()=>{setSession(null);message('Signed out.');load();});
  for(const interval of ['monthly','yearly'])$(interval).addEventListener('click',()=>run(async()=>{
    const result=await api('checkout',{interval});
    if(!confirm('Paystack will charge '+result.display_charge+' in NGN for the '+interval+' plan. Continue?'))return;
    location.assign(result.authorization_url);
  }));
  $('cancel').addEventListener('click',()=>run(async()=>{
    if(!confirm('Cancel renewal? Pro remains active through the paid date.'))return;
    const result=await api('cancel_at_period_end');draw(result.status);message('Cancellation requested. Status will update after Paystack confirms it.');
  }));
  $('manage').addEventListener('click',()=>run(async()=>{
    const result=await api('manage_subscription');location.assign(result.url);
  }));
  if(location.pathname==='/billing/return')message('Payment is pending verification. Your plan updates after Paystack confirms it.');
  await load();
  if(session)setInterval(()=>{api('status').then(draw).catch(()=>{})},60000);
}
initialize().catch(error=>message(error.message));
