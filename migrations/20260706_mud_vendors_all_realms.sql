-- Seed a working vendor at the start room of every realm that doesn't
-- have one yet (Medieval already has Yard Peddler Wenna). Sci-Fi has an
-- existing recipe (patch-cable) to sell back; the three newest realms
-- have no recipes yet, so those vendors sell raw supplies only for now.

INSERT INTO mud_vendors (room_id, name, greeting)
SELECT r.id, 'Dock Steward Ilo', 'Ilo barely looks up from the manifest. "Spool, cell, or you want that cable back? Name it."'
FROM mud_rooms r WHERE r.slug = 'transit-dock'
ON CONFLICT DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Wire Spool', 'wire-spool', 'A spool of salvaged conductive wire.', true, 4, NULL, 0
FROM mud_vendors v WHERE v.name = 'Dock Steward Ilo'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Capacitor Cell', 'capacitor-cell', 'A small charge cell, still holding a trickle of power.', true, 5, NULL, 1
FROM mud_vendors v WHERE v.name = 'Dock Steward Ilo'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Patch Cable', 'patch-cable', 'A crude data bridge assembled from scavenged wire and a charge cell.', true, NULL, 12, 2
FROM mud_vendors v WHERE v.name = 'Dock Steward Ilo'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendors (room_id, name, greeting)
SELECT r.id, 'Seam Peddler Vex', 'Vex doesn''t look fully solid in this light. "Cable, lantern -- whatever survived the last fold."'
FROM mud_rooms r WHERE r.slug = 'veil-first-tear'
ON CONFLICT DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Frayed Cable', 'frayed-cable', 'A length of cable pulled from somewhere the geometry no longer agrees with.', true, 4, NULL, 0
FROM mud_vendors v WHERE v.name = 'Seam Peddler Vex'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Static Lantern', 'static-lantern', 'A lantern that burns with a cold, flickering light.', true, 6, NULL, 1
FROM mud_vendors v WHERE v.name = 'Seam Peddler Vex'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendors (room_id, name, greeting)
SELECT r.id, 'Bark Trader Sil', 'Sil nods from beside a moss-lined cart. "Twine, moss, whatever the canopy gave up today."'
FROM mud_rooms r WHERE r.slug = 'march-greatroot-landing'
ON CONFLICT DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Dried Root Twine', 'dried-root-twine', 'Tough cord twisted from dried root fiber.', true, 4, NULL, 0
FROM mud_vendors v WHERE v.name = 'Bark Trader Sil'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Luminous Moss Vial', 'luminous-moss-vial', 'A corked vial holding a pinch of faintly glowing moss.', true, 5, NULL, 1
FROM mud_vendors v WHERE v.name = 'Bark Trader Sil'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendors (room_id, name, greeting)
SELECT r.id, 'Pressure Clerk Orin', 'Orin checks a gauge without turning around. "Sealant or fittings -- the hull won''t patch itself."'
FROM mud_rooms r WHERE r.slug = 'reach-first-airlock'
ON CONFLICT DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Sealant Patch', 'sealant-patch', 'A rubbery patch that hardens against a leak on contact.', true, 4, NULL, 0
FROM mud_vendors v WHERE v.name = 'Pressure Clerk Orin'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Brass Fitting', 'brass-fitting', 'A heavy brass fitting, dulled green from long submersion.', true, 5, NULL, 1
FROM mud_vendors v WHERE v.name = 'Pressure Clerk Orin'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;
