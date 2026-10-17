// Public free-launch intake. An email here is unverified and grants no account rights.
const url=Deno.env.get('SUPABASE_URL')||'';
const secret=Deno.env.get('SUPABASE_SECRET_KEY')||Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')||'';
const publishable=Deno.env.get('SUPABASE_PUBLISHABLE_KEY')||Deno.env.get('SUPABASE_ANON_KEY')||'';
const response=(body:unknown,status:number)=>new Response(JSON.stringify(body),{
  status,headers:{'content-type':'application/json','cache-control':'no-store'}});

Deno.serve(async req=>{
  if(req.method!=='POST')return response({error:'POST required'},405);
  if(req.headers.get('origin'))return response({error:'Browser origin not supported'},403);
  if(!publishable || req.headers.get('apikey')!==publishable)return response({error:'Invalid project key'},401);
  if(Number(req.headers.get('content-length')||0)>1024)return response({error:'Request too large'},413);
  let body:Record<string,unknown>;
  try{
    const raw=await req.text();
    if(raw.length>1024)return response({error:'Request too large'},413);
    body=JSON.parse(raw);
    if(!body || typeof body!=='object' || Array.isArray(body))throw new Error();
  }catch{return response({error:'Invalid request'},400);}
  const email=typeof body.email==='string'?body.email.trim().toLowerCase():'';
  const installation=typeof body.installation_id==='string'?body.installation_id:'';
  if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length>254 ||
     !/^[a-f0-9]{8}-[a-f0-9]{4}-[1-8][a-f0-9]{3}-[89ab][a-f0-9]{3}-[a-f0-9]{12}$/i.test(installation))
    return response({error:'Enter a valid email address'},400);
  if(!url || !secret)return response({error:'Email service unavailable'},503);
  const headers:Record<string,string>={apikey:secret,'content-type':'application/json',
    'content-profile':'gazely','prefer':'resolution=merge-duplicates,return=minimal'};
  if(!secret.startsWith('sb_secret_'))headers.authorization='Bearer '+secret;
  try{
    const saved=await fetch(url+'/rest/v1/email_entries?on_conflict=installation_id',{
      method:'POST',headers,body:JSON.stringify({installation_id:installation,email,updated_at:new Date().toISOString()})});
    if(!saved.ok)return response({error:'Could not record email. Please retry.'},503);
    return response({recorded:true},200);
  }catch{return response({error:'Could not reach email service. Please retry.'},503);}
});
