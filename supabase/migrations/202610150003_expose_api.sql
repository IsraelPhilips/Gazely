-- The Edge Functions use service-role PostgREST in the gazely schema.
-- All client grants remain revoked and all gazely tables retain default-deny RLS.
alter role authenticator set pgrst.db_schemas = 'public, graphql_public, gazely';
notify pgrst, 'reload config';
notify pgrst, 'reload schema';
