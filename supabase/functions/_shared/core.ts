export const africa = new Set('DZ AO BJ BW BF BI CV CM CF TD KM CG CD CI DJ EG GQ ER SZ ET GA GM GH GN GW KE LS LR LY MG MW ML MR MU MA MZ NA NE NG RW ST SN SC SL SO ZA SS SD TZ TG TN UG ZM ZW'.split(' '));
export type Interval = 'monthly' | 'yearly';
export const priceNgn = (country: string, interval: Interval) =>
  (africa.has(country) ? (interval === 'monthly' ? 250000 : 2500000) :
    (interval === 'monthly' ? 665000 : 6650000)); // kobo
// Reserved until merchant USD support and the whole subscription flow are verified.
export const preparedUsdMinor = (country: string, interval: Interval) =>
  africa.has(country) && country !== 'NG' ? (interval === 'monthly' ? 189 : 1899) :
    !africa.has(country) ? (interval === 'monthly' ? 499 : 4990) : null;

const url = Deno.env.get('SUPABASE_URL')!;
const secret = Deno.env.get('SUPABASE_SECRET_KEY') || Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!;
const publishable = Deno.env.get('SUPABASE_PUBLISHABLE_KEY') || Deno.env.get('SUPABASE_ANON_KEY')!;
type DbResult={data:any,error:Error|null};
const dbHeaders:Record<string,string>={apikey:secret,'accept-profile':'gazely','content-profile':'gazely'};
if(!secret.startsWith('sb_secret_'))dbHeaders.authorization='Bearer '+secret;
async function db(path:string,method='GET',body?:unknown,extra?:Record<string,string>):Promise<DbResult> {
  const response=await fetch(url+'/rest/v1/'+path,{method,headers:{...dbHeaders,...extra,
    ...(body===undefined?{}:{'content-type':'application/json'})},body:body===undefined?undefined:JSON.stringify(body)});
  const raw=await response.text();
  let parsed:any=null;
  try{parsed=raw?JSON.parse(raw):null;}catch{return {data:null,error:new Error('invalid database response')};}
  return response.ok?{data:parsed,error:null}:{data:null,error:new Error(parsed?.message||'database request failed')};
}
class Query implements PromiseLike<DbResult> {
  private params=new URLSearchParams();
  constructor(private table:string){}
  select(fields:string){this.params.set('select',fields);return this;}
  eq(column:string,value:unknown){this.params.set(column,'eq.'+String(value));return this;}
  order(column:string,options:{ascending:boolean}){this.params.set('order',column+(options.ascending?'.asc':'.desc'));return this;}
  private async read(one:boolean):Promise<DbResult>{
    if(one)this.params.set('limit','1');
    const result=await db(this.table+'?'+this.params.toString());
    if(result.error)return result;
    return one?{data:result.data?.[0]||null,error:null}:result;
  }
  single():Promise<DbResult>{return this.read(true).then(result=>result.data?result:{data:null,error:result.error||new Error('row not found')});}
  maybeSingle():Promise<DbResult>{return this.read(true);}
  then<TResult1=DbResult,TResult2=never>(resolve?:((value:DbResult)=>TResult1|PromiseLike<TResult1>)|null,
    reject?:((reason:any)=>TResult2|PromiseLike<TResult2>)|null):Promise<TResult1|TResult2>{return this.read(false).then(resolve,reject);}
  insert(value:unknown):Promise<DbResult>{return db(this.table,'POST',value,{'prefer':'return=minimal'});}
}
export const admin={
  from:(table:string)=>new Query(table),
  rpc:(name:string,params:Record<string,unknown>)=>db('rpc/'+name,'POST',params),
};

export function json(value: unknown, status = 200, origin?: string): Response {
  return new Response(JSON.stringify(value), { status, headers: {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
    ...(origin ? { 'access-control-allow-origin': origin, 'vary': 'Origin' } : {}),
  } });
}
export function allowedOrigin(request: Request): string | undefined {
  const origin = request.headers.get('origin');
  const expected = Deno.env.get('GAZELY_PORTAL_ORIGIN');
  return origin && expected && origin === expected ? origin : undefined;
}
export async function actor(request: Request) {
  const header = request.headers.get('authorization') || '';
  if (!header.startsWith('Bearer ')) throw new Error('sign in required');
  const response=await fetch(url+'/auth/v1/user',{headers:{apikey:publishable,authorization:header}});
  if(!response.ok)throw new Error('sign in required');
  const user=await response.json();
  if(!user?.id || !user?.email)throw new Error('sign in required');
  return user;
}
export async function owner(userId: string) {
  const { data, error } = await admin.from('memberships').select('church_id,role').eq('user_id',userId).maybeSingle();
  if (error) throw error;
  if (!data || data.role !== 'owner') throw new Error('church owner required');
  return data.church_id as string;
}
export async function currentStatus(churchId: string) {
  const now = new Date();
  const [church, subscription, devices, period, release] = await Promise.all([
    admin.from('churches').select('id,name,billing_country,billing_country_locked').eq('id',churchId).single(),
    admin.from('subscriptions').select('state,paid_through,grace_until,next_payment_at,billing_interval,currency,amount_minor,paystack_subscription_code').eq('church_id',churchId).maybeSingle(),
    admin.from('devices').select('id,label,activated_at,deactivated_at').eq('church_id',churchId).order('activated_at',{ascending:false}),
    admin.rpc('current_period',{p_church:churchId,p_at:now.toISOString()}),
    admin.from('release_settings').select('billing_enabled').eq('singleton',true).single(),
  ]);
  for (const result of [church,subscription,devices,period,release]) if (result.error) throw result.error;
  const sub = subscription.data;
  const pro = !!sub && ((['active','cancel_at_period_end'].includes(sub.state) && !!sub.paid_through && new Date(sub.paid_through)>now) ||
    (sub.state==='past_due' && !!sub.grace_until && new Date(sub.grace_until)>now));
  const billingEnabled=release.data.billing_enabled===true;
  const fullAccess=!billingEnabled || pro;
  const bounds = period.data?.[0];
  if (!bounds) throw new Error('usage period unavailable');
  const usage = await admin.from('usage_periods').select('accepted_seconds').eq('church_id',churchId).eq('period_start',bounds.period_start).maybeSingle();
  if (usage.error) throw usage.error;
  return {
    church: church.data,
    billing_enabled:billingEnabled,
    access_mode:billingEnabled?'billing':'launch_free',
    plan: billingEnabled && pro ? 'pro' : 'free',
    entitlements: { core_recognition:true, scripture_detection:true, mini:true, manual_present:true,
      auto_prepare:fullAccess, automatic_live:fullAccess },
    device_limit:fullAccess?2:1,
    devices:devices.data,
    usage:{ period_start:bounds.period_start, period_end:bounds.period_end,
      accepted_seconds:usage.data?.accepted_seconds||0, allowance_seconds:fullAccess?null:3600 },
    subscription:{ state:sub?.state||'free', paid_through:sub?.paid_through||null,
      grace_until:sub?.grace_until||null, next_payment_at:sub?.next_payment_at||null,
      billing_interval:sub?.billing_interval||null, currency:sub?.currency||null,
      amount_minor:sub?.amount_minor||null,
      can_manage:!!sub?.paystack_subscription_code },
  };
}
export function b64url(bytes: Uint8Array): string {
  let s=''; for (const byte of bytes) s+=String.fromCharCode(byte);
  return btoa(s).replaceAll('+','-').replaceAll('/','_').replaceAll('=','');
}
function fromB64url(value:string):Uint8Array {
  if(!/^[A-Za-z0-9_-]+$/.test(value))throw new Error('invalid licence encoding');
  const raw=atob(value.replaceAll('-','+').replaceAll('_','/'));
  return Uint8Array.from(raw,c=>c.charCodeAt(0));
}
export async function verifyLease(token:string) {
  if(token.length>8192)throw new Error('invalid licence');
  const pieces=token.split('.');if(pieces.length!==2)throw new Error('invalid licence');
  const spki=Deno.env.get('GAZELY_LICENSE_PUBLIC_KEY_SPKI_BASE64');
  if(!spki)throw new Error('licence verifier is not configured');
  const key=await crypto.subtle.importKey('spki',Uint8Array.from(atob(spki),c=>c.charCodeAt(0)),
    {name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['verify']);
  const valid=await crypto.subtle.verify('RSASSA-PKCS1-v1_5',key,fromB64url(pieces[1]),new TextEncoder().encode(pieces[0]));
  if(!valid)throw new Error('invalid licence signature');
  const claim=JSON.parse(new TextDecoder().decode(fromB64url(pieces[0])));
  if(claim.v!==1 || claim.iss!=='Gazely' || !claim.church_id || !claim.device_id || !claim.period_start ||
     Date.parse(claim.expires_at)+30*86400000<Date.now())throw new Error('licence outside reconciliation window');
  return claim;
}
export async function signLease(payload: Record<string,unknown>): Promise<string> {
  const encoded = b64url(new TextEncoder().encode(JSON.stringify(payload)));
  const privateKey = Deno.env.get('GAZELY_LICENSE_PRIVATE_KEY_PKCS8_BASE64');
  if (!privateKey) throw new Error('licence signer is not configured');
  const der = Uint8Array.from(atob(privateKey),c=>c.charCodeAt(0));
  const key = await crypto.subtle.importKey('pkcs8',der,{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['sign']);
  const signature = new Uint8Array(await crypto.subtle.sign('RSASSA-PKCS1-v1_5',key,new TextEncoder().encode(encoded)));
  return encoded+'.'+b64url(signature);
}
export async function paystack(path: string, init?: RequestInit) {
  const key=Deno.env.get('PAYSTACK_SECRET_KEY');
  if (!key) throw new Error('Paystack is not configured');
  const mode=Deno.env.get('PAYSTACK_MODE');
  if((mode==='test' && !key.startsWith('sk_test_')) || (mode==='live' && !key.startsWith('sk_live_')) ||
     (mode!=='test' && mode!=='live'))throw new Error('Paystack key and mode mismatch');
  const response=await fetch('https://api.paystack.co'+path,{...init,headers:{authorization:'Bearer '+key,'content-type':'application/json',...(init?.headers||{})}});
  const body=await response.json();
  if (!response.ok || !body.status) throw new Error('Paystack request failed');
  return body.data;
}
