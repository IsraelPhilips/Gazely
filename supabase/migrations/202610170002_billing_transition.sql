-- The one-time billing switch starts fresh allowance periods. Previously
-- signed free-launch usage may still reconcile into its original periods.
create or replace function gazely.billing_activation_guard()
returns trigger language plpgsql set search_path = '' as $$
begin
  if old.billing_enabled and not new.billing_enabled then
    raise exception 'billing activation is one-way';
  end if;
  if not old.billing_enabled and new.billing_enabled then new.changed_at:=clock_timestamp(); end if;
  return new;
end $$;
revoke all on function gazely.billing_activation_guard() from public, anon, authenticated;
create trigger billing_activation_before_update before update on gazely.release_settings
  for each row execute function gazely.billing_activation_guard();

create or replace function gazely.current_period(p_church uuid, p_at timestamptz default now())
returns table(period_start timestamptz, period_end timestamptz)
language plpgsql stable set search_path = '' as $$
declare anchor timestamptz; billing_on boolean; billing_start timestamptz;
begin
  select activation_anniversary into anchor from gazely.churches where id=p_church;
  if anchor is null then raise exception 'unknown church'; end if;
  select billing_enabled,changed_at into billing_on,billing_start from gazely.release_settings where singleton=true;
  if billing_on is null then raise exception 'release settings unavailable'; end if;
  if billing_on then anchor:=greatest(anchor,billing_start); end if;
  period_start := anchor;
  while period_start + interval '30 days' <= p_at loop period_start := period_start + interval '30 days'; end loop;
  period_end := period_start + interval '30 days';
  return next;
end $$;

create or replace function gazely.record_usage(p_church uuid,p_device uuid,p_period_start timestamptz,p_events jsonb)
returns integer language plpgsql security definer set search_path = '' as $$
declare item jsonb; total integer; delta integer; event_uuid uuid; anchor timestamptz;
  billing_on boolean; billing_start timestamptz; billing_anchor timestamptz; valid_period boolean;
begin
  if jsonb_typeof(p_events) <> 'array' or jsonb_array_length(p_events)>120 then raise exception 'invalid usage batch'; end if;
  perform 1 from gazely.churches where id=p_church for update;
  if not found then raise exception 'unknown church'; end if;
  if not exists(select 1 from gazely.devices where id=p_device and church_id=p_church) then raise exception 'unknown device'; end if;
  select activation_anniversary into anchor from gazely.churches where id=p_church;
  select billing_enabled,changed_at into billing_on,billing_start from gazely.release_settings where singleton=true;
  if billing_on is null then raise exception 'release settings unavailable'; end if;
  billing_anchor:=greatest(anchor,billing_start);
  valid_period:=mod(extract(epoch from p_period_start-anchor)::numeric,2592000)=0;
  if billing_on and not valid_period then
    valid_period:=mod(extract(epoch from p_period_start-billing_anchor)::numeric,2592000)=0;
  end if;
  if p_period_start<anchor or p_period_start>clock_timestamp() or p_period_start+interval '60 days'<clock_timestamp() or not valid_period then
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
