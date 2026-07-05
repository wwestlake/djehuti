-- Seed one working vendor at the Medieval start room so the new
-- buy/sell/give economy has something to interact with immediately.
-- Buys raw crafting materials cheap, sells the crafted torch back at a
-- small markup -- gives new players an obvious, understandable first
-- use for crowns without requiring a quest system.

INSERT INTO mud_vendors (room_id, name, greeting)
SELECT r.id, 'Yard Peddler Wenna', 'Wenna looks up from her cart. "Rags, oil, or a finished torch -- what''ll it be?"'
FROM mud_rooms r
WHERE r.slug = 'keep-gate'
ON CONFLICT DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Rag Strip', 'rag-strip', 'A torn strip of cloth, good for wrapping or burning.', true, 4, NULL, 0
FROM mud_vendors v WHERE v.name = 'Yard Peddler Wenna'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Lamp Oil', 'lamp-oil', 'A small flask of oil, enough to feed a torch or lamp.', true, 5, NULL, 1
FROM mud_vendors v WHERE v.name = 'Yard Peddler Wenna'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;

INSERT INTO mud_vendor_listings (vendor_id, item_name, item_slug, item_description, portable, buy_price, sell_price, position)
SELECT v.id, 'Torch', 'torch', 'A rough torch made from cloth and oil. It burns hot enough to light dark passages.', true, NULL, 12, 2
FROM mud_vendors v WHERE v.name = 'Yard Peddler Wenna'
ON CONFLICT (vendor_id, item_slug) DO NOTHING;
