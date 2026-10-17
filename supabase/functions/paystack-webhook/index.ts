import { admin, json, paystack } from '../_shared/core.ts';

function hex(bytes:ArrayBuffer):string { return [...new Uint8Array(bytes)].map(x=>x.toString(16).padStart(2,'0')).join(''); }
function equalHex(a:string,b:string):boolean {
  if(a.length!==b.length)return false;
  let difference=0;for(let i=0;i<a.length;i++)difference|=a.charCodeAt(i)^b.charCodeAt(i);
  return difference===0;
}
async function signed(raw:Uint8Array,signature:string):Promise<boolean> {
  const secret=Deno.env.get('PAYSTACK_SECRET_KEY');
  const mode=Deno.env.get('PAYSTACK_MODE');
  if(!secret || (mode==='test' && !secret.startsWith('sk_test_')) ||
     (mode==='live' && !secret.startsWith('sk_live_')) ||
     (mode!=='test' && mode!=='live') || !/^[a-f0-9]{128}$/i.test(signature))return false;
  const key=await crypto.subtle.importKey('raw',new TextEncoder().encode(secret),{name:'HMAC',hash:'SHA-512'},false,['sign']);
  const expected=hex(await crypto.subtle.sign('HMAC',key,raw));
  return equalHex(expected,signature.toLowerCase());
}
function periodEnd(start:string,interval:string):string {
  const d=new Date(start);if(!Number.isFinite(d.getTime()))throw new Error('invalid paid date');
  const target=new Date(d);const original=d.getUTCDate();target.setUTCDate(1);
  if(interval==='monthly')target.setUTCMonth(target.getUTCMonth()+1);
  else if(interval==='yearly')target.setUTCFullYear(target.getUTCFullYear()+1);
  else throw new Error('invalid interval');
  const last=new Date(Date.UTC(target.getUTCFullYear(),target.getUTCMonth()+1,0)).getUTCDate();
  target.setUTCDate(Math.min(original,last));return target.toISOString();
}
async function churchForCustomer(code:string) {
  if(!code)throw new Error('unknown customer');
  const result=await admin.from('subscriptions').select('church_id').eq('paystack_customer_code',code).maybeSingle();
  if(result.error)throw result.error;
  if(!result.data)throw new Error('unknown customer');
  return result.data.church_id as string;
}

Deno.serve(async req=>{
  if(req.method!=='POST')return json({error:'POST required'},405);
  const bytes=new Uint8Array(await req.arrayBuffer());
  if(bytes.length>65536)return json({error:'event too large'},413);
  if(!await signed(bytes,req.headers.get('x-paystack-signature')||''))return json({error:'invalid signature'},401);
  try {
    const hash=hex(await crypto.subtle.digest('SHA-256',bytes));
    const event=JSON.parse(new TextDecoder().decode(bytes));
    const kind=String(event.event||'');
    const data=event.data||{};
    const supported=new Set(['charge.success','subscription.create','invoice.payment_failed',
      'subscription.not_renew','subscription.disable','refund.processed','charge.dispute.create']);
    if(!supported.has(kind))return json({ignored:true});
    const expectedDomain=Deno.env.get('PAYSTACK_MODE');
    if(expectedDomain!=='test' && expectedDomain!=='live')throw new Error('Paystack mode missing');
    if(data.domain && data.domain!==expectedDomain)throw new Error('Paystack mode mismatch');
    let reference:string|null=null,churchId:string,subscriptionCode:string|null=null;
    let customerCode:string|null=null,planCode:string|null=null,interval:string|null=null;
    let currency:string|null=null,amount:number|null=null,paidAt:string|null=null,paidThrough:string|null=null;
    if(kind==='charge.success') {
      reference=String(data.reference||'');
      if(!/^[A-Za-z0-9_.=-]{6,100}$/.test(reference))throw new Error('invalid charge reference');
      const verified=await paystack('/transaction/verify/'+encodeURIComponent(reference));
      if(verified.status!=='success' || verified.reference!==reference || verified.domain!==expectedDomain)
        throw new Error('unverified payment');
      currency=String(verified.currency);amount=Number(verified.amount);
      customerCode=String(verified.customer?.customer_code||'');
      subscriptionCode=String(data.subscription?.subscription_code||data.subscription_code||verified.subscription?.subscription_code||'')||null;
      planCode=String(verified.plan?.plan_code||verified.plan||data.plan?.plan_code||'')||null;
      const intent=await admin.from('checkout_intents').select('church_id,interval,currency,amount_minor,status').eq('reference',reference).maybeSingle();
      if(intent.error)throw intent.error;
      if(intent.data) {
        churchId=intent.data.church_id;interval=intent.data.interval;
        if(intent.data.currency!==currency || intent.data.amount_minor!==amount)throw new Error('price mismatch');
      } else {
        if(!subscriptionCode && (!customerCode || !planCode))throw new Error('unmatched renewal');
        const row=await admin.from('subscriptions').select('church_id,billing_interval,currency,amount_minor,paystack_subscription_code,paystack_plan_code,paystack_customer_code')
          .eq(subscriptionCode?'paystack_subscription_code':'paystack_customer_code',subscriptionCode||customerCode).maybeSingle();
        if(row.error)throw row.error;
        if(!row.data || !row.data.paystack_subscription_code || row.data.paystack_customer_code!==customerCode ||
           row.data.paystack_plan_code!==planCode || row.data.currency!==currency || row.data.amount_minor!==amount)
          throw new Error('unmatched renewal');
        subscriptionCode=row.data.paystack_subscription_code;
        churchId=row.data.church_id;interval=row.data.billing_interval;
      }
      paidAt=String(verified.paid_at||'');paidThrough=periodEnd(paidAt,interval!);
    } else if(kind==='refund.processed' || kind==='charge.dispute.create') {
      reference=String(data.transaction_reference||data.transaction?.reference||data.reference||'');
      if(!reference)throw new Error('missing charge reference');
      const transaction=await paystack('/transaction/verify/'+encodeURIComponent(reference));
      if(transaction.reference!==reference || transaction.domain!==expectedDomain)throw new Error('unverified transaction');
      const row=await admin.from('subscriptions').select('church_id').eq('last_charge_reference',reference).maybeSingle();
      if(row.error)throw row.error;if(!row.data)throw new Error('unknown charge');churchId=row.data.church_id;
    } else {
      subscriptionCode=String(data.subscription_code||data.subscription?.subscription_code||'');
      customerCode=String(data.customer?.customer_code||'');
      if(!subscriptionCode)throw new Error('missing subscription code');
      const provider=await paystack('/subscription/'+encodeURIComponent(subscriptionCode));
      if(provider.subscription_code!==subscriptionCode)throw new Error('unverified subscription');
      customerCode=String(provider.customer?.customer_code||customerCode);
      churchId=await churchForCustomer(customerCode);
      if(kind==='subscription.create') {
        planCode=String(provider.plan?.plan_code||'')||null;
      }
    }
    const eventAt=String(data.paid_at||data.created_at||event.created_at||new Date().toISOString());
    if(!Number.isFinite(Date.parse(eventAt)))throw new Error('invalid event time');
    // The hash identifies exact retries; SQL also rejects stale or unmatched state transitions.
    const result=await admin.rpc('apply_payment_event',{
      p_key:kind+':'+hash,p_type:kind,p_reference:reference,p_church:churchId!,p_hash:hash,
      p_event_at:paidAt||eventAt,p_paid_through:paidThrough,p_subscription_code:subscriptionCode,
      p_customer_code:customerCode,p_plan_code:planCode,p_interval:interval,p_currency:currency,p_amount:amount,
    });
    if(result.error)throw result.error;
    return json({result:result.data});
  } catch(error) {
    console.error('Paystack reconciliation failed',error);
    return json({error:'reconciliation failed'},500);
  }
});
