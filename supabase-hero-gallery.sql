-- Run in Supabase SQL editor if the main schema was already applied earlier.
create table if not exists public.hero_gallery_settings (
    id integer primary key check (id = 1),
    layout_key text not null default 'editorial'
        check (layout_key in ('editorial', 'balanced', 'featured-strip')),
    updated_at timestamptz not null default now()
);

create table if not exists public.hero_gallery_slots (
    slot_key text primary key
        check (slot_key in ('featured', 'secondary', 'tertiary', 'quaternary')),
    portfolio_item_id bigint references public.portfolio_items(id) on delete set null,
    sort_order integer not null check (sort_order between 1 and 4),
    updated_at timestamptz not null default now()
);

insert into public.hero_gallery_settings (id, layout_key)
values (1, 'editorial')
on conflict (id) do nothing;

insert into public.hero_gallery_slots (slot_key, portfolio_item_id, sort_order)
values
    ('featured', null, 1),
    ('secondary', null, 2),
    ('tertiary', null, 3),
    ('quaternary', null, 4)
on conflict (slot_key) do nothing;

alter table public.hero_gallery_settings enable row level security;
alter table public.hero_gallery_slots enable row level security;

drop policy if exists "hero settings are publicly readable" on public.hero_gallery_settings;
create policy "hero settings are publicly readable"
    on public.hero_gallery_settings for select using (true);

drop policy if exists "hero slots are publicly readable" on public.hero_gallery_slots;
create policy "hero slots are publicly readable"
    on public.hero_gallery_slots for select using (true);
