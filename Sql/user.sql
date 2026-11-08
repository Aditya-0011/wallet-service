/*
    OutCodes:
        -1: Success

        0: Record exists

        1: Record not found

        2: Db Error

        3: Child records exists
*/

create table if not exists wallet.user (
    id int primary key,
    allIncome numeric(15, 2),
    allExpense numeric(15, 2),
    monthIncome numeric(15, 2),
    monthExpense numeric(15, 2),
    weekIncome numeric(15, 2),
    weekExpense numeric(15, 2),
    startDate timestamptz default now(),
    updatedAt timestamptz default now()
);

create or replace function wallet.initiate_user(
    in p_id int, 
    out v_startDate timestamptz,
    out outcode smallint
)
as
$$
begin
    insert into wallet.user(id, allIncome, allExpense, monthIncome, monthExpense, weekIncome, weekExpense)
    values(p_id, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00)
    on conflict (id) do nothing
    returning startDate into v_startDate;

    if found then
        outcode := -1;
        return;
    end if;

    select startDate into v_startDate from wallet.user where id = p_id;
    outcode := 0;

exception 
    when others then 
        raise warning 'DB Error: %', sqlerrm;
        outcode := 2;
end;
$$ language plpgsql security definer set search_path = '';

create or replace function wallet.get_summary(in p_id int)
returns table (
    allIncome numeric,
    allExpense numeric,
    monthIncome numeric,
    monthExpense numeric,
    weekIncome numeric,
    weekExpense numeric,
    startDate timestamptz
)
as $$
    select
        u.allIncome,
        u.allExpense,
        case when u.updatedAt >= date_trunc('month', now(), 'Asia/Kolkata') then u.monthIncome else 0 end,
        case when u.updatedAt >= date_trunc('month', now(), 'Asia/Kolkata') then u.monthExpense else 0 end,
        case when u.updatedAt >= date_trunc('week', now(), 'Asia/Kolkata') then u.weekIncome else 0 end,
        case when u.updatedAt >= date_trunc('week', now(), 'Asia/Kolkata') then u.weekExpense else 0 end,
        u.startDate
    from wallet.user u
    where u.id = p_id;
$$ language sql stable security definer set search_path = '';

revoke insert, update, delete on table wallet.user from wallet_service;
grant select on table wallet.user to wallet_service;

revoke execute on function
    wallet.initiate_user(int),
    wallet.get_summary(int)
from public;

grant execute on function
    wallet.initiate_user(int),
    wallet.get_summary(int)
to wallet_service;