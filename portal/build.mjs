import { copyFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const url=process.env.SUPABASE_URL;
const key=process.env.SUPABASE_PUBLISHABLE_KEY;
if(!/^https:\/\/[a-z0-9-]+\.supabase\.co$/.test(url||'') || !key?.startsWith('sb_publishable_'))
  throw new Error('Set SUPABASE_URL and SUPABASE_PUBLISHABLE_KEY for this Vercel environment.');
const output=join(process.cwd(),'dist');
mkdirSync(output,{recursive:true});
for(const file of ['index.html','style.css','app.js'])copyFileSync(join(process.cwd(),file),join(output,file));
writeFileSync(join(output,'config.json'),JSON.stringify({supabaseUrl:url,publishableKey:key}));
console.log('Built Gazely account portal.');
