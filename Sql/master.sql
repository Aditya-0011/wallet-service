do $$
begin
    if not exists (select 1 from pg_roles where rolname = 'wallet_service') then
        create user wallet_service with password 'test@12345';
    end if;
end
$$;
revoke all on database infra from wallet_service;
grant connect on database infra to wallet_service;

create schema if not exists wallet;

grant usage on schema public, wallet to wallet_service;