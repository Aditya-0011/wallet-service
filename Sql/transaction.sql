/*
    OutCodes:
        -1: Success

        0: Record exists

        1: Record not found

        2: Db Error

        3: Child records exists
*/

/*
    create table if not exists wallet.user (
        id int primary key,
        allIncome numeric(15, 2),
        allExpense numeric(15, 2),
        monthIncome numeric(15, 2),
        monthExpense numeric(15, 2),
        weekIncome numeric(15, 2),
        weekExpense numeric(15, 2),
        updatedAt timestamptz default now()
    );
*/

/*
    create table if not exists wallet.category(
        id int generated always as identity primary key,
        userId int not null,
        name varchar(100) not null,
        icon varchar(20) not null,
        type smallint not null,
        createdAt timestamptz default now(),
        updatedAt timestamptz default now(),
        unique(userId, icon, type)
    );
*/

create table if not exists wallet.transaction(
    id int generated always as identity primary key,
    userId int not null,
    categoryId int not null,
    description varchar(255) not null,
    amount numeric(15,2) not null,
    createdAt timestamptz default now(),
    updatedAt timestamptz default now()
);
create index if not exists idx_wallet_transaction_userId_categoryId_createdAt on wallet.transaction(userId, categoryId, createdAt desc);
create index if not exists idx_wallet_transaction_userId_createdAt on wallet.transaction(userId, createdAt desc);

create table if not exists wallet.transaction_update(
    id int generated always as identity primary key,
    transactionId int not null,
    userId int not null,
    previousCategoryId int not null,
    currentCategoryId int not null,
    previousDescription varchar(255) not null,
    currentDescription varchar(255) not null,
    previousAmount numeric(15,2) not null,
    currentAmount numeric(15,2) not null,
    createdAt timestamptz default now()
);
create index if not exists idx_wallet_transaction_updates_transactionId_userId on wallet.transaction_update(transactionId, userId);

create or replace function wallet.edit_transaction(
    in p_id int,
    in p_userId int,
    in p_categoryId int,
    in p_description varchar,
    in p_amount numeric,
    out outcode smallint
)
as $$
declare
    i_old_amount numeric;
    i_old_createdAt timestamptz;
    i_old_category_type smallint;
    i_old_cataegory_id int;
    i_old_description varchar;

    i_category_type smallint;
    i_new_createdAt timestamptz;
    i_current_month_start timestamptz;
    i_current_week_start timestamptz;
    i_account_updated_at timestamptz;

    i_reset_month boolean := false;
    i_reset_week boolean := false;
begin
    insert into wallet.user(id, allIncome, allExpense, monthIncome, monthExpense, weekIncome, weekExpense)
    values(p_userId, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00)
    on conflict (id) do nothing;

    select updatedAt into i_account_updated_at from wallet.user where id = p_userId for update;
    if not found then
        outcode := 1;
        return;
    end if;

    select type into i_category_type from wallet.category where id = p_categoryId and userId = p_userId for share;
    if not found then
        outcode := 1;
        return;
    end if;

    i_current_month_start := date_trunc('month', now(), 'Asia/Kolkata');
    i_current_week_start := date_trunc('week', now(), 'Asia/Kolkata');

    if i_account_updated_at < i_current_month_start then
        i_reset_month := true;
    end if;

    if i_account_updated_at < i_current_week_start then
        i_reset_week := true;
    end if;

    if p_id > 0 then
        select t1.amount, t1.description, t1.createdAt, t2.id, t2.type 
        into i_old_amount, i_old_description, i_old_createdAt, i_old_cataegory_id, i_old_category_type
        from wallet.transaction t1 join wallet.category t2 
        on t1.categoryId = t2.id 
        where t1.id = p_id and t1.userId = p_userId for update;
        if not found then
            outcode := 1;
            return;
        end if;
        
        i_new_createdAt := i_old_createdAt;
        
        update wallet.transaction set 
            categoryId = p_categoryId,
            description = p_description,
            amount = p_amount,
            updatedAt = now()
        where id = p_id and userId = p_userId;

        insert into wallet.transaction_update(
            transactionId,
            userId,
            previousCategoryId,
            currentCategoryId,
            previousDescription,
            currentDescription,
            previousAmount,
            currentAmount
        )
        values(
            p_id,
            p_userId,
            i_old_cataegory_id,
            p_categoryId,
            i_old_description,
            p_description,
            i_old_amount,
            p_amount
        );

        update wallet.user set
            allIncome = allIncome
                        - case when i_old_category_type = 1 then i_old_amount else 0 end
                        + case when i_category_type = 1 then p_amount else 0 end,
            allExpense = allExpense
                        - case when i_old_category_type = 2 then i_old_amount else 0 end
                        + case when i_category_type = 2 then p_amount else 0 end,

            monthIncome = case 
                when i_reset_month then case when i_category_type = 1 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
                else monthIncome 
                    - case when i_old_category_type = 1 and i_old_createdAt >= i_current_month_start then i_old_amount else 0 end
                    + case when i_category_type = 1 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
            end,

            monthExpense = case 
                when i_reset_month then case when i_category_type = 2 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
                else monthExpense 
                    - case when i_old_category_type = 2 and i_old_createdAt >= i_current_month_start then i_old_amount else 0 end
                    + case when i_category_type = 2 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
            end,

            weekIncome = case 
                when i_reset_week then case when i_category_type = 1 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
                else weekIncome 
                    - case when i_old_category_type = 1 and i_old_createdAt >= i_current_week_start then i_old_amount else 0 end
                    + case when i_category_type = 1 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
            end,

            weekExpense = case 
                when i_reset_week then case when i_category_type = 2 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
                else weekExpense 
                    - case when i_old_category_type = 2 and i_old_createdAt >= i_current_week_start then i_old_amount else 0 end
                    + case when i_category_type = 2 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
            end,

            updatedAt = now()
        where id = p_userId;

        outcode := -1;

    else
        i_new_createdAt := now();

        insert into wallet.transaction(userId, categoryId, description, amount)
        values(p_userId, p_categoryId, p_description, p_amount);

        update wallet.user set
            allIncome = allIncome + case when i_category_type = 1 then p_amount else 0 end,
            allExpense = allExpense + case when i_category_type = 2 then p_amount else 0 end,

            monthIncome = case 
                when i_reset_month then case when i_category_type = 1 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
                else monthIncome + case when i_category_type = 1 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
            end,
            monthExpense = case 
                when i_reset_month then case when i_category_type = 2 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
                else monthExpense + case when i_category_type = 2 and i_new_createdAt >= i_current_month_start then p_amount else 0 end
            end,

            weekIncome = case 
                when i_reset_week then case when i_category_type = 1 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
                else weekIncome + case when i_category_type = 1 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
            end,
            weekExpense = case 
                when i_reset_week then case when i_category_type = 2 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
                else weekExpense + case when i_category_type = 2 and i_new_createdAt >= i_current_week_start then p_amount else 0 end
            end,

            updatedAt = now()
        where id = p_userId;

        outcode := -1;
        
    end if;
    
exception 
    when others then 
        raise warning 'DB Error: %', sqlerrm;
        outcode := 2;
end;
$$ language plpgsql security definer set search_path = '';

create or replace function wallet.delete_transaction(
    in p_id int,
    in p_userId int,
    out outcode smallint
)as 
$$
declare
    i_amount numeric;
    i_createdAt timestamptz;
    i_category_type smallint;

    i_current_month_start timestamptz;
    i_current_week_start timestamptz;
    i_account_updated_at timestamptz;

    i_reset_month boolean := false;
    i_reset_week boolean := false;
begin
    select updatedAt into i_account_updated_at from wallet.user where id = p_userId for update;
    if not found then
        outcode := 1;
        return;
    end if;

    i_current_month_start := date_trunc('month', now(), 'Asia/Kolkata');
    i_current_week_start := date_trunc('week', now(), 'Asia/Kolkata');

    if i_account_updated_at < i_current_month_start then
        i_reset_month := true;
    end if;

    if i_account_updated_at < i_current_week_start then
        i_reset_week := true;
    end if;

    select t1.amount, t1.createdAt, t2.type 
    into i_amount, i_createdAt, i_category_type
    from wallet.transaction t1 join wallet.category t2 
    on t1.categoryId = t2.id 
    where t1.id = p_id and t1.userId = p_userId for update;
    if not found then
        outcode := 1;
        return;
    end if;

    delete from wallet.transaction_update where transactionId = p_id and userId = p_userId;

    delete from wallet.transaction where id = p_id and userId = p_userId;

    update wallet.user set
        allIncome = allIncome - case when i_category_type = 1 then i_amount else 0 end,
        allExpense = allExpense - case when i_category_type = 2 then i_amount else 0 end,

        monthIncome = case
            when i_reset_month then 0
            else monthIncome - case when i_category_type = 1 and i_createdAt >= i_current_month_start then i_amount else 0 end
        end,
        monthExpense = case
            when i_reset_month then 0
            else monthExpense - case when i_category_type = 2 and i_createdAt >= i_current_month_start then i_amount else 0 end
        end,

        weekIncome = case
            when i_reset_week then 0
            else weekIncome - case when i_category_type = 1 and i_createdAt >= i_current_week_start then i_amount else 0 end
        end,
        weekExpense = case
            when i_reset_week then 0
            else weekExpense - case when i_category_type = 2 and i_createdAt >= i_current_week_start then i_amount else 0 end
        end,

        updatedAt = now()
    where id = p_userId;

    outcode := -1;
exception 
    when others then 
        raise warning 'DB Error: %', sqlerrm;
        outcode := 2;
end;
$$ language plpgsql security definer set search_path = '';

revoke insert, update, delete on table wallet.transaction from wallet_service;
revoke insert, update, delete on table wallet.transaction_update from wallet_service;

grant select on table wallet.transaction to wallet_service;
grant select on table wallet.transaction_update to wallet_service;

revoke execute on function
    wallet.edit_transaction(int, int, int, varchar, numeric),
    wallet.delete_transaction(int, int)
from public;

grant execute on function
    wallet.edit_transaction(int, int, int, varchar, numeric),
    wallet.delete_transaction(int, int)
to wallet_service;
