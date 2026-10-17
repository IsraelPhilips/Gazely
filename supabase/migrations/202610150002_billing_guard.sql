-- Preserve paid access after Paystack stops future renewal, while refusing
-- incomplete charge facts from the reconciliation function.
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
    if p_reference is null or p_event_at is null or p_paid_through is null or p_paid_through <= p_event_at
      or p_currency is null or p_amount is null or p_interval is null then raise exception 'invalid charge'; end if;
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
      update gazely.subscriptions set state=case when paid_through>now() then 'cancel_at_period_end' else 'cancelled' end,
        grace_until=null,updated_at=now() where church_id=p_church;
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
