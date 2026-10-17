-- Launch mode is controlled only by a database administrator. No public client
-- can change this setting or write billing facts.
create table gazely.release_settings (
  singleton boolean primary key default true check (singleton),
  billing_enabled boolean not null default false,
  changed_at timestamptz not null default now()
);
insert into gazely.release_settings(singleton,billing_enabled) values(true,false);
alter table gazely.release_settings enable row level security;
revoke all on gazely.release_settings from public, anon, authenticated;
grant select on gazely.release_settings to service_role;

create or replace function gazely.activate_device(p_church uuid,p_installation uuid,p_label text)
returns uuid language plpgsql security definer set search_path = '' as $$
declare result uuid; seats integer; billing_on boolean;
begin
  perform 1 from gazely.churches where id=p_church for update;
  if not found then raise exception 'unknown church'; end if;
  select billing_enabled into billing_on from gazely.release_settings where singleton=true;
  if billing_on is null then raise exception 'release settings unavailable'; end if;
  select id into result from gazely.devices where church_id=p_church and installation_id=p_installation and deactivated_at is null;
  if result is not null then return result; end if;
  seats := case when not billing_on or gazely.is_pro(p_church) then 2 else 1 end;
  if (select count(*) from gazely.devices where church_id=p_church and deactivated_at is null) >= seats then
    raise exception 'device limit reached';
  end if;
  insert into gazely.devices(church_id,installation_id,label) values(p_church,p_installation,left(trim(p_label),120))
    on conflict (church_id,installation_id) do update set label=excluded.label,activated_at=now(),deactivated_at=null
    returning id into result;
  return result;
end $$;
