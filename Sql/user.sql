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
    allIncome numeric(3, 2),
    allExpense numeric(3, 2),
    monthIncome numeric(3, 2),
    monthExpense numeric(3, 2),
    weekIncome numeric(3, 2),
    weekExpense numeric(3, 2),
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
    perform 1 from wallet.user where id = p_id;
    if found then 
        v_startDate := null;
        outcode := 0;
        return;
    end if;

    v_startDate := now();

    insert into wallet.user(id, allIncome, allExpense, monthIncome, monthExpense, weekIncome, weekExpense, startDate)
    values(p_id, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, v_startDate);

    outcode := -1;

exception 
    when others then 
        raise warning 'DB Error: %', sqlerrm;
        outcode := 2;
end;
$$ language plpgsql security definer;

revoke insert, update, delete on table wallet.user from wallet_service;
grant select on table wallet.user to wallet_service;

grant execute on function
    wallet.initiate_user(int)
to wallet_service;