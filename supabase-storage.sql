-- Run this if you already applied supabase-schema.sql earlier.
-- Creates the public storage bucket used for admin picture uploads.

insert into storage.buckets (id, name, public)
values ('portfolio', 'portfolio', true)
on conflict (id) do update set public = excluded.public;

drop policy if exists "Public read portfolio images" on storage.objects;
create policy "Public read portfolio images"
    on storage.objects for select
    using (bucket_id = 'portfolio');

drop policy if exists "Service role can manage portfolio images" on storage.objects;
create policy "Service role can manage portfolio images"
    on storage.objects for all
    using (bucket_id = 'portfolio')
    with check (bucket_id = 'portfolio');
