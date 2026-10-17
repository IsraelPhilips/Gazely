import { actor, admin, allowedOrigin, currentStatus, json, owner, paystack, priceNgn, signLease, verifyLease } from '../_shared/core.ts';

const requestSizeLimit=32768;
function bad(message:string,status=400,origin?:string) { return json({error:message},status,origin); }
async function bodyOf(req:Request):Promise<Record<string,unknown>> {
  const raw=await req.text();
  if(raw.length>requestSizeLimit)throw new Error('request too large');
  const body=JSON.parse(raw);
  if(!body || typeof body!=='object' || Array.isArray(body))throw new Error('invalid request');
  return body;
}
function textField(value:unknown,max:number):string {
  if(typeof value!=='string' || !value.trim() || value.length>max)throw new Error('invalid field');
  return value.trim();
}
function uuid(value:unknown):string {
  const s=textField(value,36);
  if(!/^[a-f0-9]{8}-[a-f0-9]{4}-[1-8][a-f0-9]{3}-[89ab][a-f0-9]{3}-[a-f0-9]{12}$/i.test(s))throw new Error('invalid identity');
  return s;
}
async function lease(churchId:string,installationId:string) {
  const status=await currentStatus(churchId);
  // The status response deliberately omits installation IDs; query this one device privately.
  const match=await admin.from('devices').select('id,deactivated_at').eq('church_id',churchId).eq('installation_id',installationId).maybeSingle();
  if(match.error)throw match.error;
  if(!match.data || match.data.deactivated_at)throw new Error('device is not active');
  const authorized=(status.devices||[]).filter(d=>!d.deactivated_at)
    .sort((a,b)=>String(a.activated_at).localeCompare(String(b.activated_at)))
    .slice(0,status.device_limit).some(d=>d.id===match.data.id);
  if(!authorized)throw new Error('device seat unavailable on current plan');
  const now=Date.now();
  const limit=status.plan==='pro' ? now+30*86400000 : now+7*86400000;
  const paid=status.subscription.state==='past_due' ? status.subscription.grace_until : status.subscription.paid_through;
  const expires=status.billing_enabled
    ? Math.min(limit,status.plan==='pro' ? (paid ? Date.parse(paid) : now) : Date.parse(status.usage.period_end))
    : limit;
  if(!(expires>now))throw new Error('licence unavailable');
  return signLease({v:1,iss:'Gazely',church_id:churchId,device_id:match.data.id,
    installation_id:installationId,plan:status.plan,access_mode:status.access_mode,entitlements:status.entitlements,
    allowance_seconds:status.usage.allowance_seconds,accepted_seconds:status.usage.accepted_seconds,
    period_start:status.usage.period_start,period_end:status.usage.period_end,
    issued_at:new Date(now).toISOString(),expires_at:new Date(expires).toISOString()});
}

Deno.serve(async req=>{
  const origin=allowedOrigin(req);
  const suppliedOrigin=req.headers.get('origin');
  if(suppliedOrigin && !origin)return bad('origin denied',403);
  if(req.method==='OPTIONS')return new Response(null,{status:204,headers:{'access-control-allow-origin':origin||'',
    'access-control-allow-headers':'authorization,apikey,content-type','access-control-allow-methods':'POST,OPTIONS','vary':'Origin'}});
  if(req.method!=='POST')return bad('POST required',405,origin);
  try {
    const user=await actor(req);
    const body=await bodyOf(req);
    const action=textField(body.action,40);
    if(action==='create_church') {
      const name=textField(body.name,120);
      const country=textField(body.billing_country,2).toUpperCase();
      if(!/^[A-Z]{2}$/.test(country))throw new Error('invalid country');
      const result=await admin.rpc('create_church',{p_user:user.id,p_name:name,p_country:country});
      if(result.error)throw result.error;
      return json({church_id:result.data,status:await currentStatus(result.data)},200,origin);
    }
    const churchId=await owner(user.id);
    if(action==='status')return json(await currentStatus(churchId),200,origin);
    if(action==='activate_device') {
      const installation=uuid(body.installation_id);
      const label=textField(body.label,120);
      const result=await admin.rpc('activate_device',{p_church:churchId,p_installation:installation,p_label:label});
      if(result.error)throw result.error;
      return json({device_id:result.data,licence:await lease(churchId,installation),status:await currentStatus(churchId)},200,origin);
    }
    if(action==='deactivate_device') {
      const deviceId=uuid(body.device_id);
      const result=await admin.rpc('deactivate_device',{p_church:churchId,p_device:deviceId});
      if(result.error)throw result.error;
      return json({deactivated:result.data,status:await currentStatus(churchId)},200,origin);
    }
    if(action==='sync_usage') {
      const deviceId=uuid(body.device_id);
      const proof=await verifyLease(textField(body.licence,8192));
      if(proof.church_id!==churchId || proof.device_id!==deviceId)throw new Error('licence does not match usage');
      const events=body.events;
      if(!Array.isArray(events) || events.length>120 || events.some(e=>
        !e || typeof e!=='object' || Object.keys(e).sort().join(',')!=='id,seconds' ||
        typeof e.seconds!=='number' || !Number.isInteger(e.seconds) || e.seconds<1 || e.seconds>15 ||
        typeof e.id!=='string' || !/^[a-f0-9-]{36}$/i.test(e.id)))throw new Error('invalid usage events');
      const result=await admin.rpc('record_usage',{p_church:churchId,p_device:deviceId,
        p_period_start:proof.period_start,p_events:events});
      if(result.error)throw result.error;
      return json({accepted_seconds:result.data,status:await currentStatus(churchId)},200,origin);
    }
    if(action==='refresh_licence')return json({licence:await lease(churchId,uuid(body.installation_id)),status:await currentStatus(churchId)},200,origin);
    if(action==='checkout') {
      const interval=body.interval;
      if(interval!=='monthly' && interval!=='yearly')throw new Error('invalid interval');
      const status=await currentStatus(churchId);
      if(!status.billing_enabled)throw new Error('Gazely is free. Checkout is not available.');
      if(status.plan==='pro' && status.subscription.state!=='past_due')
        throw new Error('Pro is already active. Manage the existing subscription instead.');
      const country=status.church.billing_country;
      const amount=priceNgn(country,interval);
      const tier=amount===250000 || amount===2500000 ? 'AFRICA' : 'GLOBAL';
      const envName='PAYSTACK_PLAN_NGN_'+tier+'_'+interval.toUpperCase();
      const planCode=Deno.env.get(envName);
      if(!planCode)throw new Error('checkout plan unavailable');
      const plan=await paystack('/plan/'+encodeURIComponent(planCode));
      const paystackInterval=interval==='yearly'?'annually':'monthly';
      if(Number(plan.amount)!==amount || plan.currency!=='NGN' || plan.interval!==paystackInterval)throw new Error('Paystack plan price mismatch');
      const reference='gazely_'+crypto.randomUUID().replaceAll('-','');
      const intent=await admin.from('checkout_intents').insert({reference,church_id:churchId,interval,country,currency:'NGN',amount_minor:amount});
      if(intent.error)throw intent.error;
      const email=user.email!;
      const callback=Deno.env.get('GAZELY_PORTAL_ORIGIN')+'/billing/return';
      const transaction=await paystack('/transaction/initialize',{method:'POST',body:JSON.stringify({
        email,amount,currency:'NGN',plan:planCode,reference,callback_url:callback,
        metadata:{gazely_church_id:churchId,gazely_checkout_reference:reference},
      })});
      return json({authorization_url:transaction.authorization_url,reference,
        currency:'NGN',amount_minor:amount,display_charge:'₦'+(amount/100).toLocaleString('en-NG')},200,origin);
    }
    if(action==='cancel_at_period_end') {
      const status=await currentStatus(churchId);
      if(!status.subscription.can_manage)throw new Error('no subscription to manage');
      const row=await admin.from('subscriptions').select('paystack_subscription_code').eq('church_id',churchId).single();
      if(row.error)throw row.error;
      const sub=await paystack('/subscription/'+encodeURIComponent(row.data.paystack_subscription_code));
      if(!sub.email_token)throw new Error('subscription management unavailable');
      await paystack('/subscription/disable',{method:'POST',body:JSON.stringify({code:row.data.paystack_subscription_code,token:sub.email_token})});
      // The webhook confirms state. The paid-through date remains in force.
      return json({pending:true,status:await currentStatus(churchId)},200,origin);
    }
    if(action==='manage_subscription') {
      const status=await currentStatus(churchId);
      if(!status.subscription.can_manage)throw new Error('no subscription to manage');
      const row=await admin.from('subscriptions').select('paystack_subscription_code').eq('church_id',churchId).single();
      if(row.error)throw row.error;
      const managed=await paystack('/subscription/'+encodeURIComponent(row.data.paystack_subscription_code)+'/manage/link');
      if(typeof managed.link!=='string' || !managed.link.startsWith('https://'))throw new Error('subscription management unavailable');
      return json({url:managed.link},200,origin);
    }
    return bad('unknown action',404,origin);
  } catch(error) {
    const message=error instanceof Error?error.message:'request failed';
    const unauth=message==='sign in required' || message==='church owner required';
    return bad(message,unauth?401:400,origin);
  }
});
