psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=readonly_password="$LIVEPOLL_READONLY_PASSWORD" --set=database_name="$POSTGRES_DB" <<'EOF'
SELECT format('CREATE USER livepoll_reader WITH PASSWORD %L', :'readonly_password') \gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO livepoll_reader', :'database_name') \gexec
GRANT USAGE ON SCHEMA public TO livepoll_reader;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO livepoll_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO livepoll_reader;
EOF
