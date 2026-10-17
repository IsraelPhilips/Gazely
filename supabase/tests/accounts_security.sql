-- Run on the TEST project after migration; the transaction leaves no test data.
begin;
do $$
declare church_a uuid; church_b uuid; device_a uuid; started timestamptz; old_period timestamptz; rejected boolean:=false;
  usage_id uuid:=gen_random_uuid(); checkout_ref text:='test_'||replace(gen_random_uuid()::text,'-',''); paid_at timestamptz:=now();
begin
  if has_table_privilege('authenticated','gazely.churches','SELECT') or
     has_table_privilege('authenticated','gazely.subscriptions','UPDATE') or
     has_table_privilege('authenticated','gazely.release_settings','SELECT') or
     has_function_privilege('authenticated','gazely.activate_device(uuid,uuid,text)','EXECUTE') then
    raise exception 'client database privileges are too broad';
  end if;
  insert into gazely.churches(name,billing_country) values('Test Church A','NG') returning id into church_a;
  insert into gazely.churches(name,billing_country) values('Test Church B','GB') returning id into church_b;
  device_a:=gazely.activate_device(church_a,gen_random_uuid(),'First PC');
  perform gazely.activate_device(church_a,gen_random_uuid(),'Second PC');
  begin
    perform gazely.activate_device(church_a,gen_random_uuid(),'Third PC');
    raise exception 'free launch device limit failed';
  exception when raise_exception then
    if sqlerrm <> 'device limit reached' then raise; end if;
  end;
  select period_start into old_period from gazely.current_period(church_a);
  perform gazely.record_usage(church_a,device_a,old_period,jsonb_build_array(jsonb_build_object('id',gen_random_uuid(),'seconds',15)));
  update gazely.release_settings set billing_enabled=true where singleton=true;
  begin
    update gazely.release_settings set billing_enabled=false where singleton=true;
    raise exception 'billing switch reversed';
  exception when raise_exception then
    if sqlerrm <> 'billing activation is one-way' then raise; end if;
  end;
  select period_start into started from gazely.current_period(church_a);
  if started<=old_period then raise exception 'billing activation did not start a new usage period'; end if;
  perform gazely.record_usage(church_a,device_a,old_period,jsonb_build_array(jsonb_build_object('id',gen_random_uuid(),'seconds',15)));
  if (select accepted_seconds from gazely.usage_periods where church_id=church_a and period_start=started) is not null then
    raise exception 'free-launch usage consumed billed allowance';
  end if;
  perform gazely.activate_device(church_b,gen_random_uuid(),'Billing Free PC');
  begin
    perform gazely.activate_device(church_b,gen_random_uuid(),'Billing Free second PC');
    raise exception 'billing Free device limit failed';
  exception when raise_exception then
    if sqlerrm <> 'device limit reached' then raise; end if;
  end;
  if gazely.deactivate_device(church_b,device_a) then raise exception 'cross-church device deactivation succeeded'; end if;
  perform gazely.record_usage(church_a,device_a,started,jsonb_build_array(jsonb_build_object('id',usage_id,'seconds',15)));
  perform gazely.record_usage(church_a,device_a,started,jsonb_build_array(jsonb_build_object('id',usage_id,'seconds',15)));
  if (select accepted_seconds from gazely.usage_periods where church_id=church_a and period_start=started)<>15 then
    raise exception 'usage did not advance';
  end if;
  -- Exact event retries are idempotent.
  begin
    perform gazely.apply_payment_event('test-event-1','charge.success','test-checkout',church_a,'test-hash',now(),now()+interval '1 month',
      'SUB_TEST','CUS_TEST','PLN_TEST','monthly','NGN',250000);
  exception when raise_exception then
    if sqlerrm='unmatched renewal' then rejected:=true; else raise; end if;
  end;
  if not rejected then raise exception 'unverified checkout was accepted'; end if;
  insert into gazely.checkout_intents(reference,church_id,interval,country,currency,amount_minor)
    values(checkout_ref,church_a,'monthly','NG','NGN',250000);
  if gazely.apply_payment_event('success-1','charge.success',checkout_ref,church_a,'hash-1',paid_at,
    paid_at+interval '1 month','SUB_TEST','CUS_TEST','PLN_TEST','monthly','NGN',250000)<>'applied' then
    raise exception 'valid payment did not apply';
  end if;
  if not gazely.is_pro(church_a) or (select billing_country_locked from gazely.churches where id=church_a) is not true then
    raise exception 'verified payment did not grant Pro and lock country';
  end if;
  if gazely.apply_payment_event('success-1','charge.success',checkout_ref,church_a,'hash-1',paid_at,
    paid_at+interval '1 month','SUB_TEST','CUS_TEST','PLN_TEST','monthly','NGN',250000)<>'duplicate' then
    raise exception 'duplicate payment was applied';
  end if;
  perform gazely.apply_payment_event('failed-old','invoice.payment_failed',null,church_a,'hash-2',paid_at,null,
    'SUB_TEST',null,null,null,null,null);
  if (select state from gazely.subscriptions where church_id=church_a)<>'active' then raise exception 'stale failure revoked Pro'; end if;
  perform gazely.apply_payment_event('cancel-1','subscription.not_renew',null,church_a,'hash-3',now(),null,
    'SUB_TEST',null,null,null,null,null);
  if not gazely.is_pro(church_a) then raise exception 'cancellation removed paid time'; end if;
  perform gazely.apply_payment_event('refund-1','refund.processed',checkout_ref,church_a,'hash-4',now(),null,
    null,null,null,null,null,null);
  if gazely.is_pro(church_a) then raise exception 'processed refund retained Pro'; end if;
end $$;
rollback;
