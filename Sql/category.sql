/*
    OutCodes:
        -1: Success

        0: Record exists

        1: Record not found

        2: Db Error

        3: Child records exists
*/

/*
    create table if not exists wallet.transaction(
        id int generated always as identity primary key,
        userId int not null,
        categoryId int not null,
        description varchar(255) not null,
        amount numeric(3,2) not null,
        createdAt timestamptz default now(),
        updatedAt timestamptz default now()
    );
*/

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
create index idx_wallet_category_userId_updatedAt on wallet.category(userId, updatedAt);

create or replace function wallet.edit_category(
    in p_id int,
    in p_userId int,
    in p_name varchar(100),
    in p_icon varchar(20),
    in p_type int,
    out outcode smallint
)
as $$
begin
    if p_id > 0 then
        perform 1 from wallet.category where id = p_id and userId = p_userId for update;
        if not found then
            outcode := 1;
            return;
        end if;

        perform 1 from wallet.transaction where categoryId = p_id and userId = p_userId;
        if found then
            outcode := 3;
            return;
        end if;
        
        update wallet.category set name = p_name, icon = p_icon, type = p_type::smallint, updatedAt = now()
        where id = p_id and userId = p_userId;

    else
        insert into wallet.category(userId, name, icon, type)
        values(p_userId, p_name, p_icon, p_type::smallint);

    end if;

    outcode := -1;

exception 
    when unique_violation then
        outcode := 0;
    when others then 
        raise warning 'DB Error: %', sqlerrm;
        outcode := 2;
end;
$$ language plpgsql;

create or replace function wallet.delete_category(
    in p_id int,
    in p_userId int,
    out outcode smallint
)as 
$$
begin
    perform 1 from wallet.category where id = p_id and userId = p_userId for update;
    if not found then
        outcode := 1;
        return;
    end if;

    perform 1 from wallet.transaction where categoryId = p_id and userId = p_userId;
    if found then
        outcode := 3;
        return;
    end if;

    delete from wallet.category where id = p_id and userId = p_userId;

    outcode := -1;

exception 
    when others then 
        raise warning 'DB Error: %', sqlerrm;
        outcode := 2;
end;
$$ language plpgsql security definer;

revoke insert, update, delete on table wallet.category from wallet_service;

grant select on table wallet.category to wallet_service;

grant execute on function
    wallet.edit_category(int,int,varchar,varchar,int),
    wallet.delete_category(int, int)
to wallet_service;