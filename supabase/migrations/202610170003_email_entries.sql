-- Unverified launch emails are contact submissions, never Auth users or entitlements.
create schema if not exists gazely;
revoke all on schema gazely from public, anon, authenticated;
grant usage on schema gazely to service_role;
create table gazely.email_entries (
  installation_id uuid primary key,
  email text not null check (length(email) between 5 and 254),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
alter table gazely.email_entries enable row level security;
revoke all on gazely.email_entries from public, anon, authenticated;
grant select, insert, update on gazely.email_entries to service_role;
