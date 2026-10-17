-- Gazely account data. Apply to test before production with Supabase migrations.
create extension if not exists pgcrypto;

create schema if not exists gazely;
revoke all on schema gazely from public, anon, authenticated;
grant usage on schema gazely to service_role;

create table gazely.churches (
  id uuid primary key default gen_random_uuid(),
  name text not null check (length(trim(name)) between 2 and 120),
  billing_country char(2) not null check (billing_country ~ '^[A-Z]{2}$'),
  billing_country_locked boolean not null default false,
  created_at timestamptz not null default now(),
  activation_anniversary timestamptz not null default now()
);
create table gazely.memberships (
  church_id uuid not null references gazely.churches(id) on delete cascade,
  user_id uuid not null references auth.users(id) on delete cascade,
  role text not null check (role in ('owner','member')),
  created_at timestamptz not null default now(),
  primary key (church_id,user_id)
);
create unique index one_church_per_user_launch on gazely.memberships(user_id);
create unique index one_owner_per_church on gazely.memberships(church_id) where role='owner';

create table gazely.devices (
  id uuid primary key default gen_random_uuid(),
  church_id uuid not null references gazely.churches(id) on delete cascade,
  installation_id uuid not null,
  label text not null check (length(label) between 1 and 120),
  activated_at timestamptz not null default now(),
  deactivated_at timestamptz,
  unique (church_id, installation_id)
);
create index active_devices_by_church on gazely.devices(church_id) where deactivated_at is null;

create table gazely.subscriptions (
  church_id uuid primary key references gazely.churches(id) on delete cascade,
  paystack_customer_code text,
  paystack_subscription_code text unique,
  paystack_plan_code text,
  billing_interval text check (billing_interval in ('monthly','yearly')),
  currency char(3) check (currency in ('NGN','USD')),
  amount_minor integer check (amount_minor > 0),
  state text not null default 'free' check (state in ('free','pending','active','past_due','cancel_at_period_end','cancelled','refunded','disputed')),
  paid_through timestamptz,
  grace_until timestamptz,
  next_payment_at timestamptz,
  last_charge_reference text,
  last_paid_at timestamptz,
  updated_at timestamptz not null default now()
);
create table gazely.checkout_intents (
  reference text primary key,
  church_id uuid not null references gazely.churches(id) on delete cascade,
  interval text not null check (interval in ('monthly','yearly')),
  country char(2) not null,
  currency char(3) not null,
  amount_minor integer not null,
  status text not null default 'pending' check (status in ('pending','verified','failed')),
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default (now() + interval '1 day')
);
create index checkout_intents_by_church on gazely.checkout_intents(church_id,created_at desc);

create table gazely.usage_periods (
  church_id uuid not null references gazely.churches(id) on delete cascade,
  period_start timestamptz not null,
  period_end timestamptz not null,
  accepted_seconds integer not null default 0 check (accepted_seconds >= 0),
  primary key (church_id,period_start),
  check (period_end > period_start)
);
create table gazely.usage_events (
  church_id uuid not null references gazely.churches(id) on delete cascade,
  device_id uuid not null references gazely.devices(id),
  event_id uuid not null,
  period_start timestamptz not null,
  seconds integer not null check (seconds between 1 and 15),
  received_at timestamptz not null default now(),
  primary key (church_id,event_id),
  foreign key (church_id,period_start) references gazely.usage_periods(church_id,period_start)
);
create table gazely.payment_events (
  event_key text primary key,
  event_type text not null,
  reference text,
  church_id uuid references gazely.churches(id),
  payload_hash text not null,
  processed_at timestamptz not null default now(),
  result text not null
);

alter table gazely.churches enable row level security;
alter table gazely.memberships enable row level security;
alter table gazely.devices enable row level security;
alter table gazely.subscriptions enable row level security;
alter table gazely.checkout_intents enable row level security;
alter table gazely.usage_periods enable row level security;
alter table gazely.usage_events enable row level security;
alter table gazely.payment_events enable row level security;

-- Deliberately no client policies. All reads/writes go through authenticated
-- Edge Functions with a server-only service role; clients cannot set a plan.
revoke all on all tables in schema gazely from public, anon, authenticated;
grant all on all tables in schema gazely to service_role;

-- All mutating operations lock the church row, serializing seat and allowance
-- decisions across multiple desktop clients.
create or replace function gazely.current_period(p_church uuid, p_at timestamptz default now())
returns table(period_start timestamptz, period_end timestamptz)
language plpgsql stable set search_path = '' as $$
declare anchor timestamptz;
begin
  select activation_anniversary into anchor from gazely.churches where id=p_church;
  if anchor is null then raise exception 'unknown church'; end if;
  period_start := anchor;
  while period_start + interval '30 days' <= p_at loop period_start := period_start + interval '30 days'; end loop;
  period_end := period_start + interval '30 days';
  return next;
end $$;

create or replace function gazely.is_pro(p_church uuid, p_at timestamptz default now())
returns boolean language sql stable set search_path = '' as $$
  select coalesce((state in ('active','cancel_at_period_end') and paid_through > p_at)
    or (state='past_due' and grace_until > p_at),false)
  from gazely.subscriptions where church_id=p_church
$$;

create or replace function gazely.activate_device(p_church uuid,p_installation uuid,p_label text)
returns uuid language plpgsql security definer set search_path = '' as $$
declare result uuid; seats integer;
begin
  perform 1 from gazely.churches where id=p_church for update;
  if not found then raise exception 'unknown church'; end if;
  select id into result from gazely.devices where church_id=p_church and installation_id=p_installation and deactivated_at is null;
  if result is not null then return result; end if;
  seats := case when gazely.is_pro(p_church) then 2 else 1 end;
  if (select count(*) from gazely.devices where church_id=p_church and deactivated_at is null) >= seats then
    raise exception 'device limit reached';
  end if;
  insert into gazely.devices(church_id,installation_id,label) values(p_church,p_installation,left(trim(p_label),120))
    on conflict (church_id,installation_id) do update set label=excluded.label,activated_at=now(),deactivated_at=null
    returning id into result;
  return result;
end $$;

create or replace function gazely.record_usage(p_church uuid,p_device uuid,p_period_start timestamptz,p_events jsonb)
returns integer language plpgsql security definer set search_path = '' as $$
declare item jsonb; total integer; delta integer; event_uuid uuid; anchor timestamptz;
begin
  if jsonb_typeof(p_events) <> 'array' or jsonb_array_length(p_events)>120 then raise exception 'invalid usage batch'; end if;
  perform 1 from gazely.churches where id=p_church for update;
  if not found then raise exception 'unknown church'; end if;
  if not exists(select 1 from gazely.devices where id=p_device and church_id=p_church) then raise exception 'unknown device'; end if;
  select activation_anniversary into anchor from gazely.churches where id=p_church;
  if p_period_start<anchor or p_period_start>now() or p_period_start+interval '60 days'<now()
    or mod(extract(epoch from p_period_start-anchor)::numeric,2592000)<>0 then
    raise exception 'usage period outside server anniversary';
  end if;
  insert into gazely.usage_periods(church_id,period_start,period_end) values(p_church,p_period_start,p_period_start+interval '30 days')
    on conflict do nothing;
  for item in select value from jsonb_array_elements(p_events) loop
    event_uuid := (item->>'id')::uuid;
    delta := (item->>'seconds')::integer;
    if delta < 1 or delta > 15 then raise exception 'invalid usage seconds'; end if;
    insert into gazely.usage_events(church_id,device_id,event_id,period_start,seconds)
      values(p_church,p_device,event_uuid,p_period_start,delta) on conflict do nothing;
    if found then
      update gazely.usage_periods set accepted_seconds=accepted_seconds+delta
        where church_id=p_church and period_start=p_period_start;
    end if;
  end loop;
  select accepted_seconds into total from gazely.usage_periods where church_id=p_church and period_start=p_period_start;
  return total;
end $$;

create or replace function gazely.create_church(p_user uuid,p_name text,p_country char(2))
returns uuid language plpgsql security definer set search_path = '' as $$
declare result uuid;
begin
  if length(trim(p_name)) not between 2 and 120 or p_country !~ '^[A-Z]{2}$' then raise exception 'invalid church details'; end if;
  if exists(select 1 from gazely.memberships where user_id=p_user) then raise exception 'account already belongs to a church'; end if;
  insert into gazely.churches(name,billing_country) values(trim(p_name),p_country) returning id into result;
  insert into gazely.memberships(church_id,user_id,role) values(result,p_user,'owner');
  return result;
end $$;

create or replace function gazely.deactivate_device(p_church uuid,p_device uuid)
returns boolean language plpgsql security definer set search_path = '' as $$
begin
  perform 1 from gazely.churches where id=p_church for update;
  update gazely.devices set deactivated_at=now() where id=p_device and church_id=p_church and deactivated_at is null;
  return found;
end $$;

create or replace function gazely.apply_payment_event(
  p_key text,p_type text,p_reference text,p_church uuid,p_hash text,
  p_event_at timestamptz,p_paid_through timestamptz,p_subscription_code text,
  p_customer_code text,p_plan_code text,p_interval text,p_currency char(3),p_amount integer)
returns text language plpgsql security definer set search_path = '' as $$
declare previous gazely.subscriptions%rowtype; intent gazely.checkout_intents%rowtype; new_intent boolean;
begin
  perform 1 from gazely.churches where id=p_church for update;
  if not found then raise exception 'unknown church'; end if;
  select * into previous from gazely.subscriptions where church_id=p_church;
  if exists(select 1 from gazely.payment_events where event_key=p_key) then return 'duplicate'; end if;
  if p_type='charge.success' then
    if p_reference is null or p_event_at is null or p_paid_through <= p_event_at then raise exception 'invalid charge'; end if;
    select * into intent from gazely.checkout_intents where reference=p_reference and church_id=p_church and status='pending';
    new_intent := found;
    if not new_intent then
      if previous.paystack_subscription_code is null or previous.paystack_subscription_code<>p_subscription_code
        or previous.currency<>p_currency or previous.amount_minor<>p_amount then raise exception 'unmatched renewal'; end if;
    else
      if intent.currency<>p_currency or intent.amount_minor<>p_amount or intent.interval<>p_interval
        or intent.created_at > p_event_at or intent.expires_at < p_event_at then raise exception 'checkout mismatch'; end if;
      update gazely.checkout_intents set status='verified' where reference=p_reference;
      update gazely.churches set billing_country_locked=true where id=p_church;
    end if;
    if previous.last_paid_at is null or p_event_at > previous.last_paid_at then
      insert into gazely.subscriptions(church_id,paystack_customer_code,paystack_subscription_code,paystack_plan_code,
        billing_interval,currency,amount_minor,state,paid_through,last_charge_reference,last_paid_at,updated_at)
      values(p_church,p_customer_code,p_subscription_code,p_plan_code,p_interval,p_currency,p_amount,'active',
        p_paid_through,p_reference,p_event_at,now())
      on conflict(church_id) do update set
        paystack_customer_code=coalesce(excluded.paystack_customer_code,gazely.subscriptions.paystack_customer_code),
        paystack_subscription_code=case when new_intent then excluded.paystack_subscription_code
          else coalesce(excluded.paystack_subscription_code,gazely.subscriptions.paystack_subscription_code) end,
        paystack_plan_code=coalesce(excluded.paystack_plan_code,gazely.subscriptions.paystack_plan_code),
        billing_interval=excluded.billing_interval,currency=excluded.currency,amount_minor=excluded.amount_minor,
        state='active',paid_through=excluded.paid_through,grace_until=null,last_charge_reference=excluded.last_charge_reference,
        last_paid_at=excluded.last_paid_at,updated_at=now();
    end if;
  elsif p_type='subscription.create' then
    if previous.paystack_subscription_code is null and p_subscription_code is not null and previous.state<>'free' then
      update gazely.subscriptions set paystack_subscription_code=p_subscription_code,updated_at=now() where church_id=p_church;
    end if;
  elsif p_type='invoice.payment_failed' then
    if previous.paystack_subscription_code=p_subscription_code and previous.state='active'
      and p_event_at >= previous.paid_through and p_event_at > previous.last_paid_at then
      update gazely.subscriptions set state='past_due',grace_until=p_event_at+interval '7 days',updated_at=now()
        where church_id=p_church;
    end if;
  elsif p_type='subscription.not_renew' then
    if previous.paystack_subscription_code=p_subscription_code and previous.state in ('active','past_due') then
      update gazely.subscriptions set state='cancel_at_period_end',grace_until=null,updated_at=now() where church_id=p_church;
    end if;
  elsif p_type='subscription.disable' then
    if previous.paystack_subscription_code=p_subscription_code and p_event_at >= previous.last_paid_at then
      update gazely.subscriptions set state='cancelled',grace_until=null,updated_at=now() where church_id=p_church;
    end if;
  elsif p_type in ('refund.processed','charge.dispute.create') then
    if previous.last_charge_reference=p_reference then
      update gazely.subscriptions set state=case when p_type='refund.processed' then 'refunded' else 'disputed' end,
        paid_through=least(coalesce(paid_through,now()),now()),grace_until=null,updated_at=now() where church_id=p_church;
    end if;
  else
    raise exception 'unsupported payment event';
  end if;
  insert into gazely.payment_events(event_key,event_type,reference,church_id,payload_hash,result)
    values(p_key,p_type,p_reference,p_church,p_hash,'applied');
  return 'applied';
end $$;

revoke all on function gazely.current_period(uuid,timestamptz), gazely.is_pro(uuid,timestamptz),
  gazely.activate_device(uuid,uuid,text), gazely.record_usage(uuid,uuid,timestamptz,jsonb),
  gazely.create_church(uuid,text,char(2)), gazely.deactivate_device(uuid,uuid),
  gazely.apply_payment_event(text,text,text,uuid,text,timestamptz,timestamptz,text,text,text,text,char(3),integer)
  from public, anon, authenticated;
grant execute on function gazely.current_period(uuid,timestamptz), gazely.is_pro(uuid,timestamptz),
  gazely.activate_device(uuid,uuid,text), gazely.record_usage(uuid,uuid,timestamptz,jsonb),
  gazely.create_church(uuid,text,char(2)), gazely.deactivate_device(uuid,uuid),
  gazely.apply_payment_event(text,text,text,uuid,text,timestamptz,timestamptz,text,text,text,text,char(3),integer)
  to service_role;
